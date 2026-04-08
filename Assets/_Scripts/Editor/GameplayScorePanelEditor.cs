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
                "Move this HUD in the Scene or Game view using this object’s Rect Transform: Anchored Position (and anchors). " +
                "The parent GameplayScoreHud canvas root is full-screen for Screen Space — Overlay; Unity often shows its Rect Transform as driven or with odd scale. " +
                "Do not try to position the HUD from the parent — use ScorePanel (this object) instead.",
                MessageType.Info);
        }

        DrawDefaultInspector();
    }
}
#endif
