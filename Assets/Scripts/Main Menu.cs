using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private CanvasGroup fadeCanvas;
    [SerializeField] private GameObject mainMenu, optionsMenu;
    [SerializeField] private TMPro.TextMeshProUGUI highScoreText;
    [SerializeField] private Texture2D customCursor;
    [SerializeField] Slider mouseSensSlider, controllerSensSlider, controllerDeadzoneSlider, aimAssistSlider, sfxSlider, musicSlider;

    private void Start()
    {
        highScoreText.text = PlayerPrefs.GetInt("HighScore", 0).ToString();
        StartCoroutine(FadeCanvas(0, 1f));
        Cursor.visible = true;
        Cursor.SetCursor(customCursor, Vector2.zero, CursorMode.Auto);

        mouseSensSlider.value = (PlayerPrefs.GetFloat("MouseSensitivity", 3f) - 1f) / 0.4f;
        controllerSensSlider.value = (PlayerPrefs.GetInt("ControllerSensitivity", 2000) - 1000) / 200;
        controllerDeadzoneSlider.value = PlayerPrefs.GetFloat("ControllerDeadzone", 0.1f) / 0.05f;
        aimAssistSlider.value = PlayerPrefs.GetFloat("AimAssistStrength", 1) / 0.2f;
        sfxSlider.value = PlayerPrefs.GetFloat("sfxVolume", 1) / 0.1f;
        musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1) / 0.1f;

        // Add listeners to sliders after they are initialised
        mouseSensSlider.onValueChanged.AddListener(delegate { UpdateOptions(); });
        controllerSensSlider.onValueChanged.AddListener(delegate { UpdateOptions(); });
        controllerDeadzoneSlider.onValueChanged.AddListener(delegate { UpdateOptions(); });
        aimAssistSlider.onValueChanged.AddListener(delegate { UpdateOptions(); });
        sfxSlider.onValueChanged.AddListener(delegate { UpdateOptions(); });
        musicSlider.onValueChanged.AddListener(delegate { UpdateOptions(); });
    }

    public void UpdateOptions()
    {
        PlayerPrefs.SetFloat("MouseSensitivity", 1f + (mouseSensSlider.value * 0.4f)); // Maps 0-10 to 1-5, 0.4 increments
        PlayerPrefs.SetInt("ControllerSensitivity", (int)(1000 + (controllerSensSlider.value * 200))); // Maps 0-10 to 1000 to 3000
        PlayerPrefs.SetFloat("ControllerDeadzone", controllerDeadzoneSlider.value * 0.05f); // Maps 0-10 to 0 to 0.5
        PlayerPrefs.SetFloat("AimAssistStrength", aimAssistSlider.value * 0.2f); // Maps 0-10 to 0-2
        PlayerPrefs.SetFloat("sfxVolume", sfxSlider.value * 0.1f);
        PlayerPrefs.SetFloat("MusicVolume", musicSlider.value * 0.1f);
    }

    public void MenuClosed()
    {
        Debug.Log("menu closed");
        optionsMenu.SetActive(false);
        mainMenu.SetActive(true);
    }

    public void EnterDungeon()
    {
        StartCoroutine(FadeAndLoad());
    }

    private IEnumerator FadeAndLoad()
    {
        yield return StartCoroutine(FadeCanvas(1, 1f));
        SceneManager.LoadScene(1);
    }

    private IEnumerator FadeCanvas(float targetAlpha, float duration)
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

    public void ExitGame()
    {
        Application.Quit();
    }
}