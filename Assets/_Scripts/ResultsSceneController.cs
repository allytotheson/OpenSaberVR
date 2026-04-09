using UnityEngine;

/// <summary>
/// Optional: open <c>Results.unity</c> directly in the editor. Normal play uses <see cref="ScoreboardOverlayUI"/>
/// on the OpenSaber scene after a run via <see cref="PostGameScoreboardFlow"/> (normal play).
/// </summary>
public class ResultsSceneController : MonoBehaviour
{
    bool _scoreboardBrowseOnly;

    void Awake()
    {
        GameplayCameraEnsurer.Ensure();

        bool browseOnly;
        int finalScore;
        int cutScore;
        int bonusScore;
        string songName;
        string difficulty;
        int highlightRank;

        if (ResultsSession.TryConsume(out browseOnly, out finalScore, out cutScore, out bonusScore, out songName, out difficulty,
                out highlightRank))
        {
            _scoreboardBrowseOnly = browseOnly;
        }
        else
        {
            browseOnly = true;
            _scoreboardBrowseOnly = true;
            finalScore = 0;
            cutScore = 0;
            bonusScore = 0;
            songName = "—";
            difficulty = "";
            highlightRank = -1;
        }

        ScoreboardOverlayUI.Build(transform, browseOnly, finalScore, cutScore, bonusScore, songName, difficulty, highlightRank,
            GoToMenu);
    }

    void GoToMenu()
    {
        var sh = Object.FindAnyObjectByType<SceneHandling>();
        if (sh != null)
        {
            sh.StartCoroutine(ReturnToMenuViaSceneHandling(sh));
        }
        else
        {
            if (!_scoreboardBrowseOnly && !UnityEngine.SceneManagement.SceneManager.GetSceneByName("Menu").isLoaded)
                UnityEngine.SceneManagement.SceneManager.LoadScene("Menu", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            TryUnloadScene("Results");
        }
    }

    System.Collections.IEnumerator ReturnToMenuViaSceneHandling(SceneHandling sh)
    {
        if (_scoreboardBrowseOnly)
        {
            TryUnloadScene("Results");
            yield break;
        }

        while (sh.IsSceneLoaded("OpenSaber"))
            yield return sh.UnloadScene("OpenSaber");

        if (!sh.IsSceneLoaded("Menu"))
            yield return sh.LoadScene("Menu", UnityEngine.SceneManagement.LoadSceneMode.Additive);

        TryUnloadScene("Results");

        var mainMenu = Object.FindAnyObjectByType<MainMenu>();
        if (mainMenu != null)
            mainMenu.RestoreUiAfterLeavingGameplay();
    }

    static void TryUnloadScene(string name)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(name);
        if (scene.IsValid() && scene.isLoaded)
            UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(name);
    }
}
