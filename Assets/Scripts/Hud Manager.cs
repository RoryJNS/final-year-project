using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using System.Collections;
using TMPro;

public class HudManager : MonoBehaviour
{
    public static HudManager Instance { get; private set; }

    [SerializeField] private bool isGamepad;
    [SerializeField] private float mouseSensitivity = 1.0f, controllerSensitivity = 50f, pulseSpeed = 5.0f;
    [SerializeField] private Image reticle;
    [SerializeField] private Sprite melee, rifle, smg, shotgun, rpg;
    [SerializeField] private Slider progressWheel, healthBar;
    [SerializeField] private Slider[] armourBar;
    [SerializeField] private Transform followsCursor;
    [SerializeField] private CinemachineCamera virtualCamera;

    [SerializeField] private RectTransform levelClearText;
    [SerializeField] private GameObject levelResultsPanel;
    [SerializeField] private TextMeshProUGUI killText, comboText, varietyText, levelScoreText, totalScoreText;

    private Coroutine zoomCoroutine, levelResults;
    private Controls controls;
    private Vector2 lookInput;
    private int maxArmourPerBar;

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

        controls = new Controls();
        controls.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
    }

    private void OnEnable()
    {
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Disable();
    }

    private void Start()
    {
        //Cursor.visible = false;
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
        float size = Mathf.Lerp(0.53f, 0.6f, (Mathf.Sin(Time.time * pulseSpeed) + 1) / 2);

        // Make the reticle and progress wheel pulse
        followsCursor.localScale = new Vector3(size, size, 1);

        // Move reticle and progress wheel to where the player is aiming
        if (isGamepad)
        {
            Vector2 stickDelta = lookInput;
            Vector3 currentPosition = followsCursor.position;
            currentPosition += controllerSensitivity * Time.deltaTime * (Vector3)stickDelta;
            followsCursor.position = currentPosition;
        }
        else
        {
            followsCursor.position = Mouse.current.position.ReadValue() * mouseSensitivity;
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
            case 4:
                reticle.sprite = rpg;
                break;
        }
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

    public void LevelResults(int killScore, int comboScore, int varietyScore, int roomScore)
    {
        levelResults = StartCoroutine(ShowLevelResults(killScore, comboScore, varietyScore, roomScore));
    }

    private IEnumerator ShowLevelResults(int killScore, int comboScore, int varietyScore, int roomScore)
    {
        // Fade out top-right score text
        yield return FadeText(totalScoreText, 1, 0, 0.3f);

        float duration = 0.2f;
        Vector2 startPos = new (-Screen.width * 2, levelClearText.anchoredPosition.y);
        Vector2 centerPos = new (0, levelClearText.anchoredPosition.y);
        Vector2 endPos = new (Screen.width * 2, levelClearText.anchoredPosition.y);
        levelClearText.anchoredPosition = startPos;
        levelClearText.gameObject.SetActive(true);
        yield return AnimateSlide(levelClearText, startPos, centerPos, 0.5f);
        yield return new WaitForSeconds(1);
        yield return AnimateSlide(levelClearText, centerPos, endPos, 0.5f);
        levelClearText.gameObject.SetActive(false);

        // Scale in panel
        killText.text = comboText.text = varietyText.text = totalScoreText.text = "";
        levelResultsPanel.SetActive(true);
        yield return AnimateScale(levelResultsPanel.transform, new(1, 0, 1), new(1, 1, 1), duration);

        // Count-up animations for each score
        yield return AnimateCountUp(killText, killScore, .5f);
        yield return AnimateCountUp(comboText, comboScore, .5f);
        yield return AnimateCountUp(varietyText, varietyScore, .5f);
        yield return AnimateCountUp(totalScoreText, roomScore, 1f);
        yield return new WaitForSeconds(4f);

        // Scale out panel
        yield return AnimateScale(levelResultsPanel.transform, new(1, 1, 1), new(1, 0, 1), duration);
        levelResultsPanel.SetActive(false);

        // Fade in top-right score text again
        yield return FadeText(totalScoreText, 0, 1, 0.3f);
    }

    public void ForceHideResults()
    {
        if (levelResults != null)
        {
            StopCoroutine(levelResults);
            levelResults = null;
            levelClearText.gameObject.SetActive(false);
            levelResultsPanel.SetActive(false);
            StartCoroutine(FadeText(totalScoreText, 0, 1, 0.3f));
        }
    }

    private IEnumerator AnimateCountUp(TextMeshProUGUI textElement, int targetValue, float duration)
    {
        float elapsed = 0;
        int startValue = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            int currentValue = Mathf.RoundToInt(Mathf.Lerp(startValue, targetValue, elapsed / duration));
            textElement.text = currentValue.ToString();
            yield return null;
        }
        textElement.text = targetValue.ToString(); // Ensure exact final value
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

    public void OnDeviceChange(PlayerInput pi)
    {
        isGamepad = pi.currentControlScheme.Equals("Gamepad");
    }
}