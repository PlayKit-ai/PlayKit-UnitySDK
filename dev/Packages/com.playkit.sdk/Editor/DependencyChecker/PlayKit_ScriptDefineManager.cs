using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;

namespace PlayKit_SDK.Editor
{
    /// <summary>
    /// Automatically manages script define symbols based on detected dependencies.
    /// Adds PLAYKIT_UNITASK_SUPPORT when UniTask is detected (regardless of installation method).
    /// Adds PLAYKIT_NEWTONSOFT_SUPPORT when Newtonsoft.Json is detected.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayKit_ScriptDefineManager
    {
        private const string UNITASK_DEFINE = "PLAYKIT_UNITASK_SUPPORT";
        private const string NEWTONSOFT_DEFINE = "PLAYKIT_NEWTONSOFT_SUPPORT";

        private static readonly NamedBuildTarget[] TargetGroups;

        static PlayKit_ScriptDefineManager()
        {
            TargetGroups = GetSupportedBuildTargets();
            EditorApplication.delayCall += UpdateScriptDefines;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        private static NamedBuildTarget[] GetSupportedBuildTargets()
        {
            var targets = new List<NamedBuildTarget>
            {
                NamedBuildTarget.Standalone,
                NamedBuildTarget.Server,
                NamedBuildTarget.Android,
                NamedBuildTarget.iOS,
                NamedBuildTarget.WebGL,
                NamedBuildTarget.tvOS,
            };

            // VisionOS - only available when the visionOS build support module is installed
            try
            {
                var visionOS = (BuildTargetGroup)Enum.Parse(typeof(BuildTargetGroup), "VisionOS");
                targets.Add(NamedBuildTarget.FromBuildTargetGroup(visionOS));
            }
            catch
            {
                // VisionOS not available in this Unity version
            }

            return targets.ToArray();
        }

        private static void OnCompilationFinished(object obj)
        {
            // Delay check after compilation to ensure assemblies are loaded
            EditorApplication.delayCall += UpdateScriptDefines;
        }

        /// <summary>
        /// Updates script define symbols based on detected dependencies.
        /// </summary>
        public static void UpdateScriptDefines()
        {
            bool hasUniTask = IsAssemblyLoaded("UniTask");
            bool hasNewtonsoft = IsAssemblyLoaded("Newtonsoft.Json") || IsAssemblyLoaded("Unity.Newtonsoft.Json");

            // Update script defines for all target groups to ensure cross-platform compatibility
            foreach (var target in TargetGroups)
            {
                try
                {
                    UpdateDefinesForTarget(target, hasUniTask, hasNewtonsoft);
                }
                catch
                {
                    // Skip targets whose build support module is not installed
                }
            }
        }

        private static void UpdateDefinesForTarget(NamedBuildTarget target, bool hasUniTask, bool hasNewtonsoft)
        {
            string currentDefines = PlayerSettings.GetScriptingDefineSymbols(target);
            var definesList = currentDefines.Split(';').Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

            bool changed = false;

            // Manage UNITASK define
            if (hasUniTask && !definesList.Contains(UNITASK_DEFINE))
            {
                definesList.Add(UNITASK_DEFINE);
                changed = true;
            }
            else if (!hasUniTask && definesList.Contains(UNITASK_DEFINE))
            {
                definesList.Remove(UNITASK_DEFINE);
                changed = true;
            }

            // Manage NEWTONSOFT define
            if (hasNewtonsoft && !definesList.Contains(NEWTONSOFT_DEFINE))
            {
                definesList.Add(NEWTONSOFT_DEFINE);
                changed = true;
            }
            else if (!hasNewtonsoft && definesList.Contains(NEWTONSOFT_DEFINE))
            {
                definesList.Remove(NEWTONSOFT_DEFINE);
                changed = true;
            }

            if (changed)
            {
                string newDefines = string.Join(";", definesList);
                PlayerSettings.SetScriptingDefineSymbols(target, newDefines);
                Debug.Log($"[PlayKit SDK] Updated script defines for {target}: UniTask={hasUniTask}, Newtonsoft={hasNewtonsoft}");
            }
        }

        private static bool IsAssemblyLoaded(string assemblyName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == assemblyName)
                {
                    return true;
                }
            }
            return false;
        }

        [MenuItem("PlayKit SDK/Refresh Script Defines")]
        public static void RefreshScriptDefinesManual()
        {
            UpdateScriptDefines();
            Debug.Log("[PlayKit SDK] Script defines refreshed.");
        }
    }
}
