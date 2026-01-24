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

        // All build target groups that should have script defines set
        private static readonly BuildTargetGroup[] TargetGroups = new[]
        {
            BuildTargetGroup.Standalone,
            BuildTargetGroup.Android,
            BuildTargetGroup.iOS,
            BuildTargetGroup.WebGL
        };

        static PlayKit_ScriptDefineManager()
        {
            EditorApplication.delayCall += UpdateScriptDefines;
        }

        private static void UpdateScriptDefines()
        {
            bool hasUniTask = IsAssemblyLoaded("UniTask");
            bool hasNewtonsoft = IsAssemblyLoaded("Newtonsoft.Json") || IsAssemblyLoaded("Unity.Newtonsoft.Json");

            // Update script defines for all target groups to ensure cross-platform compatibility
            foreach (var targetGroup in TargetGroups)
            {
                UpdateDefinesForTargetGroup(targetGroup, hasUniTask, hasNewtonsoft);
            }
        }

        private static void UpdateDefinesForTargetGroup(BuildTargetGroup targetGroup, bool hasUniTask, bool hasNewtonsoft)
        {
            string currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
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
                PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, newDefines);
                Debug.Log($"[PlayKit SDK] Updated script defines for {targetGroup}: UniTask={hasUniTask}, Newtonsoft={hasNewtonsoft}");
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
