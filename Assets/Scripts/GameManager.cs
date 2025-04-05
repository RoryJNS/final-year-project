using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private CanvasGroup fadeCanvas;
    [SerializeField] private GameObject levelResults;
    [SerializeField] private CinemachineRotationComposer composer;
    [SerializeField] private PlayerController playerController;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        GeneticAlgorithm.Instance.InitialisePopulation();
        DungeonGenerator.Instance.GenerateDungeon();
        StartCoroutine(FadeCanvas(0, 1f));
    }

    public void EnterNextLevel()
    {
        StartCoroutine(LoadNextLevel());
    }

    private IEnumerator LoadNextLevel()
    {
        playerController.SetMovementLocked(true);
        yield return FadeCanvas(1f, 1f);
        Vector2 originalDamping = composer.Damping;
        composer.Damping = Vector2.zero;
        playerController.gameObject.transform.position = Vector2.zero;
        ObjectPooler.Instance.ClearAllPools();
        GeneticAlgorithm.Instance.GenerateNewPopulation();
        DungeonGenerator.Instance.GenerateDungeon();
        DungeonGenerator.Instance.currentMainRoom.roomNumber = -1;
        ScoreSystem.Instance.ProceedToNextLevel();
        levelResults.SetActive(false);
        fadeCanvas.gameObject.SetActive(false);
        composer.Damping = originalDamping;
        playerController.SetMovementLocked(false);
        PlayerController.PlayerInput.SwitchCurrentActionMap("Player");
        Cursor.visible = false;
        yield return FadeCanvas(0f, 1f);
    }

    public void ExitToMenu()
    {
        StartCoroutine(FadeToBlackAndLoadScene(0));
    }

    public void StartNewRun()
    {
        StartCoroutine(FadeToBlackAndLoadScene(1));
    }

    private IEnumerator FadeToBlackAndLoadScene(int sceneIndex)
    {
        yield return FadeCanvas(1f, 1f);
        ScoreSystem.Instance.SaveHighScore();
        SceneManager.LoadScene(sceneIndex);
    }

    public IEnumerator FadeCanvas(float targetAlpha, float duration)
    {
        fadeCanvas.gameObject.SetActive(true);
        float startAlpha = fadeCanvas.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            fadeCanvas.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
            yield return null;
        }

        fadeCanvas.alpha = targetAlpha;
    }

    public IEnumerator OnPlayerDeath()
    {
        yield return FadeCanvas(1, 2); // Fade to black
        ScoreSystem.Instance.OnRunEnded(); // Show results for the run
    }
}