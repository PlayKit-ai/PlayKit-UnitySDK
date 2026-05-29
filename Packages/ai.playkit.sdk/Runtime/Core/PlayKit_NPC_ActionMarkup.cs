using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayKit_SDK.Provider.AI;
using PlayKit_SDK.Public;

namespace PlayKit_SDK
{
    /// <summary>
    /// Helpers for "markup mode" NPC actions: actions are embedded inline in the model's reply text
    /// as <c>[[{"action":"name","args":{...}}]]</c> markup, so the action and dialogue are returned
    /// together in a single response (no tool-call round-trip).
    ///
    /// Responsibilities:
    /// - <see cref="BuildSystemInstruction"/>: produce the system-prompt rulebook describing the markup
    ///   format and the available actions (name, description, parameters).
    /// - <see cref="Parse"/>: split a complete reply into clean display text + the actions it triggered.
    /// - <see cref="StreamFilter"/>: a stateful filter that strips markup from a streamed reply on the fly.
    ///
    /// The scanner is deliberately tolerant of common LLM mis-formatting (markdown code fences, "smart"
    /// quotation marks). The prompt also explicitly asks the model to avoid those.
    /// </summary>
    internal static class NpcActionMarkup
    {
        public const string OpenToken = "[[";
        public const string CloseToken = "]]";

        // ===== System prompt =====

        /// <summary>
        /// Build the system-prompt section that teaches the model the markup format and lists the
        /// available actions. Returns an empty string if there are no actions.
        ///
        /// The format/rules are written in English (treated as a code-ish contract, like the other
        /// system prompts in this SDK); the action and parameter descriptions are passed through in
        /// whatever language the developer authored them in.
        /// </summary>
        public static string BuildSystemInstruction(IReadOnlyList<NpcAction> actions)
        {
            if (actions == null || actions.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("## Actions you can trigger");
            sb.AppendLine("You may trigger the actions listed below from inside your reply. To trigger one, insert a marker into your reply text using exactly this format:");
            sb.AppendLine("[[{\"action\": \"actionName\", \"args\": { \"paramName\": value }}]]");
            sb.AppendLine("Rules:");
            sb.AppendLine("- The marker is parsed and executed by the game and is removed before the player sees the reply, so it may appear anywhere in the text.");
            sb.AppendLine("- If an action has no parameters, write [[{\"action\": \"actionName\"}]] (args may be omitted).");
            sb.AppendLine("- Do NOT output any marker when no action is needed.");
            sb.AppendLine("- A single reply may contain multiple markers.");
            sb.AppendLine("- Everything outside the markers must be a natural, in-character reply (and stay in the same language you'd normally reply in).");
            sb.AppendLine("- The marker content must be valid JSON: use straight ASCII double quotes (\"), no trailing commas. Do NOT wrap the marker (or the reply) in a markdown code fence, and do NOT HTML-escape or backslash-escape the [[ ]] brackets.");
            sb.AppendLine("- Only use the action names and parameter names listed below.");
            sb.AppendLine();
            sb.AppendLine("Available actions:");
            foreach (var a in actions)
            {
                if (a == null || string.IsNullOrEmpty(a.actionName)) continue;
                sb.Append("- ").Append(a.actionName);
                if (!string.IsNullOrEmpty(a.description))
                    sb.Append(" — ").Append(OneLine(a.description));
                sb.AppendLine();

                if (a.parameters != null && a.parameters.Count > 0)
                {
                    foreach (var p in a.parameters)
                    {
                        if (p == null || string.IsNullOrEmpty(p.name)) continue;
                        sb.Append("    - ").Append(p.name)
                          .Append(" (").Append(TypeName(p.type))
                          .Append(p.required ? ", required" : ", optional").Append(')');
                        if (!string.IsNullOrEmpty(p.description))
                            sb.Append(" — ").Append(OneLine(p.description));
                        if (p.type == NpcActionParamType.StringEnum && p.enumOptions != null && p.enumOptions.Length > 0)
                            sb.Append(" [one of: ").Append(string.Join(", ", p.enumOptions)).Append(']');
                        sb.AppendLine();
                    }
                }
            }
            sb.AppendLine();
            sb.AppendLine("Example — if the player says \"I want to buy a sword\", you might reply:");
            sb.AppendLine("\"Of course, this fine steel sword suits you well. [[{\"action\": \"buyItem\", \"args\": {\"itemId\": \"steel_sword\"}}]] Anything else?\"");
            sb.AppendLine("(This is only an illustration of the marker syntax — use the real action names and parameters from the list above.)");
            return sb.ToString();
        }

        private static string OneLine(string s) => s.Replace("\r", " ").Replace("\n", " ").Trim();

        private static string TypeName(NpcActionParamType t)
        {
            switch (t)
            {
                case NpcActionParamType.Number: return "number";
                case NpcActionParamType.Boolean: return "boolean";
                case NpcActionParamType.StringEnum: return "string (enum)";
                default: return "string";
            }
        }

        // ===== Parsing =====

        /// <summary>Result of parsing a complete reply.</summary>
        public sealed class Result
        {
            /// <summary>Reply text with all action markup removed (trimmed).</summary>
            public string CleanText = string.Empty;
            /// <summary>Actions triggered by the reply, as synthetic <see cref="ChatToolCall"/> (with empty Id).</summary>
            public readonly List<ChatToolCall> ToolCalls = new List<ChatToolCall>();
        }

        /// <summary>
        /// Parse a complete model reply: extract <c>[[...]]</c> action markup and return the clean text
        /// plus the triggered actions. Markup that isn't valid JSON is left in the text as-is. A single
        /// surrounding markdown code fence (```/```json) is stripped first.
        /// </summary>
        public static Result Parse(string raw)
        {
            var result = new Result();
            if (string.IsNullOrEmpty(raw)) { result.CleanText = raw ?? string.Empty; return result; }

            raw = StripSurroundingCodeFence(raw);

            var clean = new StringBuilder(raw.Length);
            int pos = 0;
            while (pos < raw.Length)
            {
                int open = raw.IndexOf(OpenToken, pos, StringComparison.Ordinal);
                if (open < 0)
                {
                    clean.Append(raw, pos, raw.Length - pos);
                    break;
                }
                clean.Append(raw, pos, open - pos);

                var status = TryReadMarkup(raw, open, out int endExclusive, out string json);
                if (status == ScanStatus.Incomplete)
                {
                    // Unterminated markup at end of reply — keep the rest as literal text.
                    clean.Append(raw, open, raw.Length - open);
                    break;
                }
                if (status == ScanStatus.Complete && TryParseObject(json, out var obj))
                {
                    var call = BuildToolCall(obj);
                    if (call != null) result.ToolCalls.Add(call);
                    // Stripped from the visible text whether or not it named a usable action.
                }
                else
                {
                    // NotMarkup, or markup whose body isn't valid JSON — emit consumed chars literally.
                    clean.Append(raw, open, endExclusive - open);
                }
                pos = endExclusive;
            }

            result.CleanText = clean.ToString().Trim();
            return result;
        }

        /// <summary>
        /// If the whole string is wrapped in a single markdown code fence (```...``` or ```json ... ```),
        /// return the inner content; otherwise return the string unchanged.
        /// </summary>
        private static string StripSurroundingCodeFence(string s)
        {
            string trimmed = s.Trim();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return s;
            if (!trimmed.EndsWith("```", StringComparison.Ordinal)) return s;
            if (trimmed.Length < 6) return s;

            int firstNewline = trimmed.IndexOf('\n');
            if (firstNewline < 0) return s; // single-line "```...```" — leave it alone
            // Drop the opening fence line (e.g. "```json") and the trailing "```".
            int innerStart = firstNewline + 1;
            int innerEnd = trimmed.Length - 3;
            if (innerEnd <= innerStart) return s;
            return trimmed.Substring(innerStart, innerEnd - innerStart);
        }

        private static bool TryParseObject(string json, out JObject obj)
        {
            obj = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                obj = JObject.Parse(json);
                return true;
            }
            catch
            {
                // Fallback: tolerate "smart" double quotes that some models emit instead of ASCII ".
                try
                {
                    obj = JObject.Parse(json.Replace('“', '"').Replace('”', '"'));
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Build a synthetic <see cref="ChatToolCall"/> from a parsed markup object.
        /// Accepts <c>{"action":"name","args":{...}}</c> as well as flat <c>{"action":"name","p1":..,"p2":..}</c>;
        /// also tolerates "call" as an alias for "action" and "arguments"/"params"/"parameters" for "args".
        /// Returns null if there is no action name. The Id is left empty so the call is fire-and-forget
        /// (<see cref="PlayKit_NPC.ReportActionResult"/> is a no-op for empty ids).
        /// </summary>
        private static ChatToolCall BuildToolCall(JObject obj)
        {
            if (obj == null) return null;
            var name = obj.Value<string>("action") ?? obj.Value<string>("call");
            if (string.IsNullOrWhiteSpace(name)) return null;

            JObject args;
            var argsToken = obj["args"] ?? obj["arguments"] ?? obj["params"] ?? obj["parameters"];
            if (argsToken is JObject argObj)
            {
                args = argObj;
            }
            else
            {
                args = new JObject();
                foreach (var prop in obj.Properties())
                {
                    switch (prop.Name)
                    {
                        case "action":
                        case "call":
                        case "args":
                        case "arguments":
                        case "params":
                        case "parameters":
                            continue;
                        default:
                            args[prop.Name] = prop.Value;
                            break;
                    }
                }
            }

            return new ChatToolCall
            {
                Id = string.Empty,
                Type = "function",
                Function = new ChatToolCallFunction
                {
                    Name = name.Trim(),
                    Arguments = args.ToString(Formatting.None)
                }
            };
        }

        // ===== Low-level scanner =====

        private enum ScanStatus
        {
            /// <summary>A complete <c>[[ {object} ]]</c> markup was read.</summary>
            Complete,
            /// <summary>Looks like markup but the input ends before it closes — need more data.</summary>
            Incomplete,
            /// <summary>The <c>[[</c> at this position is not action markup (not a JSON object inside).</summary>
            NotMarkup
        }

        private static bool IsQuote(char c) => c == '"' || c == '“' || c == '”';

        /// <summary>
        /// Try to read action markup starting at <paramref name="start"/>, where
        /// <c>s[start..start+1] == "[["</c>.
        /// - <see cref="ScanStatus.Complete"/>: <paramref name="endExclusive"/> is the index just past the
        ///   closing <c>]]</c>, <paramref name="json"/> is the inner <c>{...}</c> text.
        /// - <see cref="ScanStatus.NotMarkup"/>: <paramref name="endExclusive"/> == start + 1 (treat the first
        ///   <c>[</c> as literal text; the second <c>[</c> may start a real markup).
        /// - <see cref="ScanStatus.Incomplete"/>: <paramref name="endExclusive"/> == start (caller should wait
        ///   for more data, or, at end of input, treat the rest as literal text).
        /// Brace matching is string-aware so nested arrays/objects (including stray <c>]]</c> inside JSON
        /// strings or arrays) don't terminate the markup early.
        /// </summary>
        private static ScanStatus TryReadMarkup(string s, int start, out int endExclusive, out string json)
        {
            endExclusive = start;
            json = null;

            int i = start + OpenToken.Length; // skip "[["

            // Optional whitespace before the JSON object.
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i >= s.Length) { endExclusive = start; return ScanStatus.Incomplete; }
            if (s[i] != '{') { endExclusive = start + 1; return ScanStatus.NotMarkup; }

            int objStart = i;
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            int objEndExclusive = -1;
            for (; i < s.Length; i++)
            {
                char c = s[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (IsQuote(c)) inString = false;
                    continue;
                }
                if (IsQuote(c)) { inString = true; continue; }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) { objEndExclusive = i + 1; i++; break; }
                }
            }
            if (objEndExclusive < 0) { endExclusive = start; return ScanStatus.Incomplete; } // unterminated object

            // Optional whitespace, then the closing "]]".
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i >= s.Length) { endExclusive = start; return ScanStatus.Incomplete; }
            if (s[i] != ']') { endExclusive = start + 1; return ScanStatus.NotMarkup; }
            if (i + 1 >= s.Length) { endExclusive = start; return ScanStatus.Incomplete; }
            if (s[i + 1] != ']') { endExclusive = start + 1; return ScanStatus.NotMarkup; }

            endExclusive = i + 2;
            json = s.Substring(objStart, objEndExclusive - objStart);
            return ScanStatus.Complete;
        }

        // ===== Streaming filter =====

        /// <summary>
        /// Strips <c>[[...]]</c> action markup from a streamed reply, chunk by chunk, so the markup is
        /// never shown to the player. Get the authoritative action list by calling
        /// <see cref="NpcActionMarkup.Parse"/> on the full reply once streaming completes.
        /// Note: the sum of the cleaned chunks may differ from <c>Parse(fullReply).CleanText</c> by
        /// leading/trailing whitespace around stripped markup — treat the value passed to <c>onComplete</c>
        /// as canonical.
        /// </summary>
        public sealed class StreamFilter
        {
            private readonly StringBuilder _pending = new StringBuilder();
            private bool _leading = true; // still skipping leading whitespace

            /// <summary>Feed one streamed chunk; returns the cleaned text to surface to the consumer (may be empty).</summary>
            public string PushChunk(string chunk)
            {
                if (string.IsNullOrEmpty(chunk)) return string.Empty;
                _pending.Append(chunk);

                string src = _pending.ToString();
                var emit = new StringBuilder();
                int pos = 0;

                while (true)
                {
                    int open = src.IndexOf(OpenToken, pos, StringComparison.Ordinal);
                    if (open < 0)
                    {
                        int end = src.Length;
                        // Hold a trailing '[' — it might become "[[" with the next chunk.
                        if (end > pos && src[end - 1] == '[') end--;
                        AppendVisible(emit, src, pos, end - pos);
                        pos = end;
                        break;
                    }

                    AppendVisible(emit, src, pos, open - pos);
                    pos = open;

                    var status = TryReadMarkup(src, open, out int endExclusive, out string json);
                    if (status == ScanStatus.Incomplete)
                    {
                        // Keep everything from `open` onward; wait for more data.
                        break;
                    }
                    if (status == ScanStatus.Complete && TryParseObject(json, out _))
                    {
                        // Strip it from the visible text. (Actions are dispatched from the final Parse.)
                    }
                    else
                    {
                        // NotMarkup, or markup whose body isn't valid JSON — show consumed chars literally.
                        AppendVisible(emit, src, open, endExclusive - open);
                    }
                    pos = endExclusive;
                }

                _pending.Clear();
                _pending.Append(src, pos, src.Length - pos);
                return emit.ToString();
            }

            /// <summary>Call when the stream ends; returns any remaining held text (including unterminated markup) as-is.</summary>
            public string Flush()
            {
                string rest = _pending.ToString();
                _pending.Clear();
                if (_leading) rest = rest.TrimStart();
                return rest;
            }

            private void AppendVisible(StringBuilder emit, string src, int start, int count)
            {
                if (count <= 0) return;
                if (_leading)
                {
                    int i = start;
                    int end = start + count;
                    while (i < end && char.IsWhiteSpace(src[i])) i++;
                    if (i >= end) return; // still all whitespace
                    emit.Append(src, i, end - i);
                    _leading = false;
                }
                else
                {
                    emit.Append(src, start, count);
                }
            }
        }
    }
}
