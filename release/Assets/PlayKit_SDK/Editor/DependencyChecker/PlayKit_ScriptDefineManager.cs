using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
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

        static PlayKit_ScriptDefineManager()
        {
            EditorApplication.delayCall += UpdateScriptDefines;
        }

        /// <summary>
        /// Updates script define symbols based on detected dependencies.
        /// Call this after installing or removing dependencies.
        /// </summary>
        public static void UpdateScriptDefines()
        {
            bool hasUniTask = IsAssemblyLoaded("UniTask");
            bool hasNewtonsoft = IsAssemblyLoaded("Newtonsoft.Json") || IsAssemblyLoaded("Unity.Newtonsoft.Json");

            var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (targetGroup == BuildTargetGroup.Unknown)
            {
                targetGroup = BuildTargetGroup.Standalone;
            }

            string currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            var definesList = currentDefines.Split(';').Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

            bool changed = false;

            // Manage UNITASK define
            if (hasUniTask && !definesList.Contains(UNITASK_DEFINE))
            {
                definesList.Add(UNITASK_DEFINE);
                changed = true;
                Debug.Log($"[PlayKit SDK] Added {UNITASK_DEFINE} (UniTask detected)");
            }
            else if (!hasUniTask && definesList.Contains(UNITASK_DEFINE))
            {
                definesList.Remove(UNITASK_DEFINE);
                changed = true;
                Debug.Log($"[PlayKit SDK] Removed {UNITASK_DEFINE} (UniTask not found)");
            }

            // Manage NEWTONSOFT define
            if (hasNewtonsoft && !definesList.Contains(NEWTONSOFT_DEFINE))
            {
                definesList.Add(NEWTONSOFT_DEFINE);
                changed = true;
                Debug.Log($"[PlayKit SDK] Added {NEWTONSOFT_DEFINE} (Newtonsoft.Json detected)");
            }
            else if (!hasNewtonsoft && definesList.Contains(NEWTONSOFT_DEFINE))
            {
                definesList.Remove(NEWTONSOFT_DEFINE);
                changed = true;
                Debug.Log($"[PlayKit SDK] Removed {NEWTONSOFT_DEFINE} (Newtonsoft.Json not found)");
            }

            if (changed)
            {
                string newDefines = string.Join(";", definesList);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, newDefines);
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
    }
}
