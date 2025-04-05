using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;

public class HudManager : MonoBehaviour
{
    [SerializeField] private float mouseSensitivity, controllerSensitivity, aimAssistRange, pulseSpeed;
    [SerializeField] private Image reticle;
    [SerializeField] private Sprite melee, rifle, smg, shotgun;
    [SerializeField] private Slider progressWheel, healthBar;
    [SerializeField] private Slider[] armourBar;
    [SerializeField] private Transform followsCursor;
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private float lockOnRadius;
    [SerializeField] private Texture2D customCursor;

    [SerializeField] private RectTransform killMarker, roomClearText, levelClearText;
    [SerializeField] private GameObject roomResultsPanel, pauseMenu, optionsMenu;
    [SerializeField] private CanvasGroup levelResultsPanel, runResultsPanel;
    [SerializeField] Slider mouseSensSlider, controllerSensSlider, controllerDeadzoneSlider, aimAssistSlider, sfxSlider, musicSlider;

    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI killText, comboText, varietyText, roomScoreText;
    [SerializeField] private TextMeshProUGUI levelKillScoreText, levelComboScoreText, levelVarietyScoreText, levelScoreText, totalScoreText;
    [SerializeField] private TextMeshProUGUI killCountText, roomsClearedText, finalScoreText;

    [SerializeField] private GameObject pauseMenuFirst, levelResultsFirst, runResultsFirst;

    private Coroutine zoomCoroutine, roomResults;
    private Vector2 lookInput;
    private int maxArmourPerBar;
    private bool isPaused;

    private void Start()
    {
        Cursor.visible = false;
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

        ApplyOptions();
    }

    private void OnEnable()
    {
        if (PlayerController.PlayerInput)
        {
            PlayerController.PlayerInput.actions["Look"].performed += ctx => lookInput = ctx.ReadValue<Vector2>();
            PlayerController.PlayerInput.actions["Pause"].performed += ctx => TogglePause();
            PlayerController.PlayerInput.actions["MenuClose"].performed += ctx => MenuClosed();
        }
    }

    private void OnDisable()
    {
        if (PlayerController.PlayerInput)
        {
            PlayerController.PlayerInput.actions["Look"].performed -= ctx => lookInput = ctx.ReadValue<Vector2>();
            PlayerController.PlayerInput.actions["Pause"].performed -= ctx => TogglePause();
            PlayerController.PlayerInput.actions["MenuClose"].performed -= ctx => MenuClosed();
        }
    }

    public void InitialiseHealthAndArmour(int maxHealth, int maxArmour)
    {
        healthBar.maxValue = maxHealth;
        maxArmourPerBar = maxArmour / armourBar.Length;

        foreach (var slider in armourBar)
        {
            slider.maxValue = maxArmourPerBar;
        }

        UpdateHealthArmour(maxHealth, maxArmour);
    }

    private void Update()
    {
        // Calculate the size using Mathf.Sin for a smoother pulse effect
        float size = Mathf.Lerp(0.55f, 0.6f, (Mathf.Sin(Time.time * pulseSpeed) + 1) / 2);

        // Make the reticle and progress wheel pulse
        followsCursor.localScale = new Vector3(size, size, 1);

        if (Gamepad.current != null && Gamepad.current.rightStick.ReadValue().magnitude > 0.2f)
        {
            followsCursor.position += controllerSensitivity * Time.deltaTime * (Vector3) lookInput;
            if (Gamepad.current.rightStick.ReadValue().magnitude < 1) { ApplyAimAssist(); };
        }
        else if (Mouse.current != null && Mouse.current.delta.ReadValue().magnitude > 0.1f)
        {
            followsCursor.position += (Vector3)(Mouse.current.delta.ReadValue() * mouseSensitivity);
            if (Mouse.current.delta.ReadValue().magnitude < 15) { ApplyAimAssist(); };
        }

        // Clamp the reticle within screen bounds
        Vector3 clampedPosition = followsCursor.position;
        clampedPosition.x = Mathf.Clamp(clampedPosition.x, 0, Screen.width);
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, 0, Screen.height);
        followsCursor.position = clampedPosition;
    }

    private void ApplyAimAssist()
    {
        Transform closestEnemy = null;
        float closestDistance = aimAssistRange;
        Vector2 cursorWorldPos = Camera.main.ScreenToWorldPoint(followsCursor.position);

        foreach (var enemy in DungeonGenerator.Instance.enemies)
        {
            float distance = Vector2.Distance(cursorWorldPos, enemy.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy.transform;
            }
        }

        if (closestEnemy != null)
        {
            followsCursor.position = Camera.main.WorldToScreenPoint(closestEnemy.position);
        }
    }

    public void UpdateReticle(int currentWeapon)
    {
        switch (currentWeapon)
        {
            case 0:
                reticle.sprite = melee;
                break;
            case 1:
                reticle.sprite = rifle;
                break;
            case 2:
                reticle.sprite = smg;
                break;
            case 3:
                reticle.sprite = shotgun;
                break;
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        Cursor.visible = isPaused;
        Time.timeScale = isPaused ? 0 : 1;

        if (levelResultsPanel != null && levelResultsPanel.isActiveAndEnabled) return;
        if (runResultsPanel != null && runResultsPanel.isActiveAndEnabled) return;
        if (levelClearText != null && levelClearText.gameObject.activeSelf) return;
        if (pauseMenu != null) pauseMenu.SetActive(isPaused);
        if (followsCursor != null) followsCursor.gameObject.SetActive(!isPaused);

        if (isPaused && pauseMenuFirst != null)
        {
            PlayerController.PlayerInput.SwitchCurrentActionMap("UI");
            EventSystem.current.SetSelectedGameObject(pauseMenuFirst);
        }
        else
        {
            PlayerController.PlayerInput.SwitchCurrentActionMap("Player");
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public void MenuClosed()
    {
        if (pauseMenu != null && pauseMenu.activeSelf) { TogglePause(); }
        else if (optionsMenu != null && optionsMenu.activeSelf)
        {
            optionsMenu.SetActive(false);
            pauseMenu.SetActive(true);
            EventSystem.current.SetSelectedGameObject(pauseMenuFirst);
        }
    }

    public void UpdateOptions()
    {
        PlayerPrefs.SetFloat("MouseSensitivity", 1f + (mouseSensSlider.value * 0.4f)); // Maps 0-10 to 1-5, 0.4 increments
        PlayerPrefs.SetInt("ControllerSensitivity", (int)(1000 + (controllerSensSlider.value * 200))); // Maps 0-10 to 1000 to 3000
        PlayerPrefs.SetFloat("ControllerDeadzone", controllerDeadzoneSlider.value * 0.05f); // Maps 0-10 to 0 to 0.5
        PlayerPrefs.SetFloat("AimAssistStrength", aimAssistSlider.value * 0.2f); // Maps 0-10 to 0-2
        PlayerPrefs.SetFloat("sfxVolume", sfxSlider.value * 0.1f);
        PlayerPrefs.SetFloat("MusicVolume", musicSlider.value * 0.1f);
        ApplyOptions();
    }

    public void ApplyOptions()
    {
        mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity");
        controllerSensitivity = PlayerPrefs.GetInt("ControllerSensitivity");
        PlayerController.Instance.controllerDeadzone = PlayerPrefs.GetFloat("ControllerDeadzone");
        aimAssistRange = PlayerPrefs.GetFloat("AimAssistStrength");
    }

    public void SetProgressWheel(float value)
    {
        progressWheel.value = value;
    }

    public void UpdateHealthArmour(int health, int armour)
    {
        healthBar.value = health;

        for (int i = 0; i < armourBar.Length; i++)
        {
            if (armour > maxArmourPerBar)
            {
                armourBar[i].value = maxArmourPerBar;
                armour -= maxArmourPerBar;
            }
            else
            {
                armourBar[i].value = armour;
                armour = 0;
            }
        }
    }

    public void SetTransparent(bool transparent)
    {
        Color newColor = reticle.color;
        newColor.a = transparent ? 0.5f : 1f;
        reticle.color = newColor;
    }

    public void ZoomCamera(float targetSize, float duration)
    {
        if (zoomCoroutine != null)
            StopCoroutine(zoomCoroutine);

        zoomCoroutine = StartCoroutine(ZoomCoroutine(targetSize, duration));
    }

    private IEnumerator ZoomCoroutine(float targetSize, float duration)
    {
        float startSize = virtualCamera.Lens.OrthographicSize;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            virtualCamera.Lens.OrthographicSize = Mathf.Lerp(startSize, targetSize, elapsed / duration);
            yield return null;
        }

        virtualCamera.Lens.OrthographicSize = targetSize;
    }

    public void ShowKillMarker()
    {
        StartCoroutine(ScaleKillMarker());
    }

    private IEnumerator ScaleKillMarker()
    {
        killMarker.localScale = Vector3.zero;
        Vector3 targetScale = new(4f, 4f, 1f);
        float elapsed = 0f;

        while (elapsed < .3f)
        {
            elapsed += Time.deltaTime;
            killMarker.localScale = Vector3.Lerp(Vector3.zero, targetScale, elapsed / .3f);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < .3f)
        {
            elapsed += Time.deltaTime;
            killMarker.localScale = Vector3.Lerp(targetScale, Vector3.zero, elapsed / .3f);
            yield return null;
        }
    }

    public void RoomResults(int killScore, int comboScore, int varietyScore, int roomScore)
    {
        roomResults = StartCoroutine(ShowRoomResults(killScore, comboScore, varietyScore, roomScore));
    }

    private IEnumerator ShowRoomResults(int killScore, int comboScore, int varietyScore, int roomScore)
    {
        // Fade out top-right score text
        yield return FadeText(scoreText, 1, 0, 0.3f);
        float duration = 0.2f;
        Vector2 startPos = new (-Screen.width * 2, roomClearText.anchoredPosition.y);
        Vector2 centerPos = new (0, roomClearText.anchoredPosition.y);
        Vector2 endPos = new (Screen.width * 2, roomClearText.anchoredPosition.y);
        roomClearText.anchoredPosition = startPos;
        roomClearText.gameObject.SetActive(true);
        yield return AnimateSlide(roomClearText, startPos, centerPos, 0.5f);
        yield return new WaitForSeconds(1);
        yield return AnimateSlide(roomClearText, centerPos, endPos, 0.5f);
        roomClearText.gameObject.SetActive(false);

        // Scale in panel
        killText.text = comboText.text = varietyText.text = roomScoreText.text = "";
        roomResultsPanel.SetActive(true);
        yield return AnimateScale(roomResultsPanel.transform, new(1, 0, 1), new(1, 1, 1), duration);

        // Count-up animations for each score
        yield return AnimateCountUp(killText, killScore, .5f);
        yield return AnimateCountUp(comboText, comboScore, .5f);
        yield return AnimateCountUp(varietyText, varietyScore, .5f);
        yield return AnimateCountUp(roomScoreText, roomScore, 1f);
        yield return new WaitForSeconds(4f);

        // Scale out panel
        yield return AnimateScale(roomResultsPanel.transform, new(1, 1, 1), new(1, 0, 1), duration);
        roomResultsPanel.SetActive(false);

        // Fade in top-right score text again
        yield return FadeText(scoreText, 0, 1, 0.3f);
    }

    public void ForceHideResults()
    {
        if (roomResults != null)
        {
            StopCoroutine(roomResults);
            roomResults = null;
            roomClearText.gameObject.SetActive(false);
            roomResultsPanel.SetActive(false);
            StartCoroutine(FadeText(scoreText, 0, 1, 0.3f));
        }
    }

    public void LevelResults(int levelKillScore, int levelComboScore, int levelVarietyScore, int levelScore, int totalScore)
    {
        StartCoroutine(ShowLevelResults(levelKillScore, levelComboScore, levelVarietyScore, levelScore, totalScore));
    }

    private IEnumerator ShowLevelResults(int levelKillScore, int levelComboScore, int levelVarietyScore, int levelScore, int totalScore)
    {
        PlayerController.PlayerInput.SwitchCurrentActionMap("UI");
        EventSystem.current.SetSelectedGameObject(levelResultsFirst);
        Vector2 startPos = new(-Screen.width * 2, levelClearText.anchoredPosition.y);
        Vector2 centerPos = new(0, levelClearText.anchoredPosition.y);
        Vector2 endPos = new(Screen.width * 2, levelClearText.anchoredPosition.y);
        levelClearText.anchoredPosition = startPos;
        levelClearText.gameObject.SetActive(true);
        yield return AnimateSlide(levelClearText, startPos, centerPos, 0.5f);
        yield return new WaitForSeconds(1);
        yield return AnimateSlide(levelClearText, centerPos, endPos, 0.5f);
        levelClearText.gameObject.SetActive(false);

        // Fade in the panel
        yield return FadeInCanvasGroup(levelResultsPanel, 0.5f);
        Cursor.visible = true;

        yield return AnimateCountUp(levelKillScoreText, levelKillScore, .5f);
        yield return AnimateCountUp(levelComboScoreText, levelComboScore, .5f);
        yield return AnimateCountUp(levelVarietyScoreText, levelVarietyScore, .5f);
        yield return AnimateCountUp(levelScoreText, levelScore, 1f);
        yield return AnimateCountUp(totalScoreText, totalScore, 1f);
    }

    public IEnumerator FadeInCanvasGroup(CanvasGroup canvasGroup, float duration)
    {
        canvasGroup.gameObject.SetActive(true);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0, 1, elapsed / duration);
            yield return null;
        }
    }

    public IEnumerator OnRunEnded(int kills, int rooms, int finalScore)
    {
        PlayerController.PlayerInput.SwitchCurrentActionMap("UI");
        EventSystem.current.SetSelectedGameObject(runResultsFirst);
        yield return FadeInCanvasGroup(runResultsPanel, 0.5f);
        yield return GameManager.Instance.FadeCanvas(0, 0.5f);
        Cursor.visible = true;
        yield return AnimateCountUp(killCountText, kills, 0.5f, "x");
        yield return AnimateCountUp(roomsClearedText, rooms, 0.5f, "x");
        yield return AnimateCountUp(finalScoreText, finalScore, 3f);
    }

    private IEnumerator AnimateCountUp(TextMeshProUGUI textElement, int targetValue, float duration, string prefix="")
    {
        float elapsed = 0;
        int startValue = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            int currentValue = Mathf.RoundToInt(Mathf.Lerp(startValue, targetValue, elapsed / duration));
            textElement.text = prefix + currentValue.ToString();
            yield return null;
        }
    }

    private IEnumerator AnimateSlide(RectTransform element, Vector2 start, Vector2 end, float duration)
    {
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            element.anchoredPosition = Vector2.Lerp(start, end, elapsed / duration);
            yield return null;
        }
        element.anchoredPosition = end;
    }

    private IEnumerator AnimateScale(Transform element, Vector3 start, Vector3 end, float duration)
    {
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            element.localScale = Vector3.Lerp(start, end, elapsed / duration);
            yield return null;
        }
        element.localScale = end;
    }

    private IEnumerator FadeText(TextMeshProUGUI textElement, float startAlpha, float endAlpha, float duration)
    {
        float elapsed = 0;
        Color color = textElement.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            textElement.color = color;
            yield return null;
        }

        color.a = endAlpha;
        textElement.color = color;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 worldCursorPos = Camera.main.ScreenToWorldPoint(followsCursor.position);
        worldCursorPos.z = 0f; // Ensure it's on the 2D plane
        Gizmos.DrawWireSphere(worldCursorPos, aimAssistRange);
    }
}