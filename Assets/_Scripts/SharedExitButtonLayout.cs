using UnityEngine;

/// <summary>
/// Single source of truth for EXIT placement so the menu flow and OpenSaber HUD line up on screen.
/// </summary>
public static class SharedExitButtonLayout
{
    public const float TopOffsetY = 0f;
    public const float Width = 200f;
    public const float Height = 68f;
    public const float UniformScale = 0.82f;
    public const float ForwardLocalZ = -0.15f;

    public static readonly Vector2 SizeDelta = new Vector2(Width, Height);

    /// <summary>Match <see cref="GameplayDebugHud"/> overlay scaler so menu and gameplay scale the same way.</summary>
    public const float ReferenceResolutionX = 1920f;
    public const float ReferenceResolutionY = 1080f;
    public const float MatchWidthOrHeight = 0.5f;
}
