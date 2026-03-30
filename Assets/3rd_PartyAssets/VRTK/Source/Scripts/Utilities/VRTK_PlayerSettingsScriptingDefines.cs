#if UNITY_EDITOR
namespace VRTK
{
    using UnityEditor;
#if UNITY_2021_2_OR_NEWER
    using UnityEditor.Build;
#endif

    /// <summary>
    /// Wraps PlayerSettings scripting define APIs for Unity 2021+ (NamedBuildTarget) vs legacy ForGroup APIs.
    /// </summary>
    public static class VRTK_PlayerSettingsScriptingDefines
    {
        public static string GetScriptingDefineSymbolsForGroup(BuildTargetGroup targetGroup)
        {
#if UNITY_2021_2_OR_NEWER
            return PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(targetGroup));
#else
            return PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
#endif
        }

        public static void SetScriptingDefineSymbolsForGroup(BuildTargetGroup targetGroup, string defines)
        {
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(targetGroup), defines);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, defines);
#endif
        }
    }
}
#endif
