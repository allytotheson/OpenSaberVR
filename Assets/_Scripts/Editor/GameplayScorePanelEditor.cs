#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameplayScorePanel))]
public sealed class GameplayScorePanelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var panel = (GameplayScorePanel)target;
        if (panel.IsSceneAuthored)
        {
            EditorGUILayout.HelpBox(
                "This component lives on the GameplayScoreHud root canvas.\n\n" +
                "The root canvas Rect Transform is ALWAYS locked by Unity (\"Some values driven by Canvas\") " +
                "for Screen Space – Overlay — this is normal and expected.\n\n" +
                "To reposition the HUD panels:\n" +
                "  • Expand GameplayScoreHud in the Hierarchy\n" +
                "  • Select LeftPanel  → adjust Anchored Position X / Y\n" +
                "  • Select RightPanel → adjust Anchored Position X / Y",
                MessageType.Info);
        }

        DrawDefaultInspector();
    }
}
#endif
