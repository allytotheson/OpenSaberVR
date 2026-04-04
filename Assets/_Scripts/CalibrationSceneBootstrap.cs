using UnityEngine;

/// <summary>
/// Place on an empty GameObject in the Calibration scene.
/// Ensures <see cref="ImuCalibrationController"/> exists and basic scene setup is done.
/// Also works when the scene is created programmatically (no pre-authored scene file needed).
/// </summary>
public class CalibrationSceneBootstrap : MonoBehaviour
{
    void Awake()
    {
        GameplayCameraEnsurer.Ensure();

        if (Object.FindAnyObjectByType<ImuCalibrationController>() == null)
            gameObject.AddComponent<ImuCalibrationController>();
    }

    /// <summary>
    /// Creates the Calibration scene at runtime if no authored scene exists.
    /// Called by <see cref="MainMenu"/> before loading.
    /// </summary>
    public static void EnsureCalibrationScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("Calibration");
        if (scene.IsValid() && scene.isLoaded)
            return;

        scene = UnityEngine.SceneManagement.SceneManager.CreateScene("Calibration");
        var go = new GameObject("CalibrationBootstrap");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<CalibrationSceneBootstrap>();
    }
}
