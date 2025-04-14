using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using System.Linq;

public class ScoreSystem : MonoBehaviour
{
    public static ScoreSystem Instance { get; private set; }
    public int killCount, totalScore, roomsCleared;

    [SerializeField] private int killScore, comboScore, varietyScore, roomScore, currentCombo;
    [SerializeField] private Transform playerPos;
    [SerializeField] private TextMeshProUGUI totalScoreText;
    [SerializeField] private Color highScoreColor;
    [SerializeField] private HudManager hudManager;
    [SerializeField] private PlayerAttack playerAttack;

    private Coroutine comboResetCoroutine;
    private readonly Dictionary<PlayerAttack.WeaponType, float> attackFrequency = new();
    private readonly List<RoomScoreData> roomScores = new();

    private class RoomScoreData
    {
        public int KillScore { get; }
        public int ComboScore { get; }
        public int AggressivenessBonus { get; }
        public int VarietyScore { get; }
        public int RoomScore { get; }

        public RoomScoreData(int killScore, int comboScore, int aggressivenessBonus, int varietyScore, int roomScore)
        {
            KillScore = killScore;
            ComboScore = comboScore;
            AggressivenessBonus = aggressivenessBonus;
            VarietyScore = varietyScore;
            RoomScore = roomScore;
        }
    }

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

    public void ProceedToNextRoom()
    {
        hudManager.ForceHideResults();
        attackFrequency.Clear();
    }

    public void ProceedToNextLevel()
    {
        roomScores.Clear();
    }

    public void RegisterHit(PlayerAttack.WeaponType weaponType, float firerate)
    {
        if (attackFrequency.TryGetValue(weaponType, out float value)) { attackFrequency[weaponType] = value + firerate; }
        else { attackFrequency[weaponType] = firerate; } // Effectively tracks the amount of time spent hitting shots with that weapon
    }

    public void RegisterKill(Transform enemyPos, bool wasFinisher)
    {
        if (wasFinisher) { RegisterHit(PlayerAttack.WeaponType.Melee, 1); } // Finishers count as melee hits
        else { hudManager.ShowKillMarker(); }

        killCount++;
        int scoreToAdd = wasFinisher ? 125 : 100;
        killScore += scoreToAdd;
        currentCombo++;
        StartCoroutine(ScorePopup(scoreToAdd, enemyPos, false));

        if (comboResetCoroutine != null)
        {
            StopCoroutine(comboResetCoroutine);
        }

        comboResetCoroutine = StartCoroutine(ResetCombo());
    }

    private IEnumerator ResetCombo()
    {
        yield return new WaitForSeconds(6);
        if (currentCombo > 1)
        {
            int scoreToAdd = currentCombo * 50;
            comboScore += scoreToAdd;
            StartCoroutine(ScorePopup(scoreToAdd, playerPos, true));
        }
        currentCombo = 0;
        comboResetCoroutine = null;
    }

    private IEnumerator ScorePopup(int score, Transform target, bool comboEnding)
    {
        SoundManager.PlaySound(SoundManager.SoundType.SCOREPOPUP);
        roomScore += score;
        totalScore += score;
        totalScoreText.text = totalScore.ToString();
        if (totalScore > PlayerPrefs.GetInt("HighScore", 0))
        {
            totalScoreText.color = highScoreColor;
        }
        GameObject popup = ObjectPooler.Instance.GetFromPool("Score Popup", target.position, Quaternion.identity);
        popup.GetComponent<MeshRenderer>().sortingLayerName = "UI";
        popup.GetComponent<MeshRenderer>().sortingOrder = 100;
        popup.GetComponent<TextMesh>().text = comboEnding ? $"{currentCombo}X COMBO\n+{score}" : $"+{score}";
        yield return new WaitForSeconds(1);
        popup.SetActive(false);
    }

    public void RoomCleared()
    {
        roomsCleared++;
        int aggressivenessScore = Mathf.RoundToInt(playerAttack.aggression * 800);
        float totalFrequency = attackFrequency.Values.Sum();

        if (totalFrequency == 0) return;

        float hhi = attackFrequency.Values.Sum(frequency =>
        {
            float proportion = frequency / totalFrequency;
            return proportion * proportion;
        });

        varietyScore = Mathf.RoundToInt((1 - hhi) * 1000);
        roomScore += aggressivenessScore + varietyScore;
        totalScore += aggressivenessScore + varietyScore;
        totalScoreText.text = totalScore.ToString();

        if (totalScore > PlayerPrefs.GetInt("HighScore", 0))
        {
            totalScoreText.color = highScoreColor;
        }

        // Ensure any active combo is finalised before updating the HUD
        if (currentCombo > 1)
        {
            int comboBonus = currentCombo * 50;
            comboScore += comboBonus;
            StartCoroutine(ScorePopup(comboBonus, playerPos, true));
            currentCombo = 0;
        }

        hudManager.RoomResults(killScore, comboScore, aggressivenessScore, varietyScore, roomScore);
        roomScores.Add(new RoomScoreData(killScore, comboScore, aggressivenessScore, varietyScore, roomScore));
        killScore = comboScore = varietyScore = roomScore = currentCombo = 0;
    }

    public void LevelCleared()
    {
        int levelKillScore = roomScores.Sum(room => room.KillScore);
        int levelComboScore = roomScores.Sum(room => room.ComboScore);
        int levelAggressivenessScore = roomScores.Sum(room => room.AggressivenessBonus);
        int levelVarietyScore = roomScores.Sum(room => room.VarietyScore);
        int levelScore = roomScores.Sum(room => room.RoomScore);
        hudManager.LevelResults(levelKillScore, levelComboScore, levelAggressivenessScore, levelVarietyScore, levelScore, totalScore);
    }

    public void OnRunEnded(bool playerIsAlive)
    {
        PlayerPrefs.SetInt("RunNumber", PlayerPrefs.GetInt("RunNumber", 0) + 1);
        if (AnalyticsManager.Instance != null) { AnalyticsManager.Instance.RunEnded(playerIsAlive); }
        StartCoroutine(hudManager.OnRunEnded(killCount, roomsCleared, totalScore));
    }

    public void SaveHighScore()
    {
        int currentHighScore = PlayerPrefs.GetInt("HighScore", 0);
        if (totalScore > currentHighScore)
        {
            PlayerPrefs.SetInt("HighScore", totalScore);
            PlayerPrefs.Save();
        }
    }
}