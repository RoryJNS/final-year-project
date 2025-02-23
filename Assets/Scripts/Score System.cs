using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using System.Linq;

public class ScoreSystem : MonoBehaviour
{
    public static ScoreSystem Instance { get; private set; }

    [SerializeField] private int killScore, comboScore, varietyScore, roomScore, maxRoomScore, totalScore, currentCombo;
    [SerializeField] private Transform playerPos;
    [SerializeField] private TextMeshProUGUI totalScoreText;
    [SerializeField] private ObjectPooler pooler;

    private readonly Dictionary<PlayerAttack.WeaponType, float> attackFrequency = new();
    private Coroutine comboResetCoroutine;

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
        HudManager.Instance.ForceHideResults();
        killScore = comboScore = varietyScore = roomScore = currentCombo = 0;
        attackFrequency.Clear();
        maxRoomScore = (DungeonGenerator.Instance.currentMainRoom.enemyPositions.Count * 125) + 250 + 1000;
    }

    public void RegisterHit(PlayerAttack.WeaponType weaponType, float firerate)
    {
        if (attackFrequency.TryGetValue(weaponType, out float value)) { attackFrequency[weaponType] = value + firerate; }
        else { attackFrequency[weaponType] = firerate; } // Effectively tracks the amount of time spent hitting shots with that weapon
    }

    public void RegisterKill(Transform enemyPos, bool wasFinisher)
    {
        int scoreToAdd = wasFinisher ? 125 : 100;
        killScore += scoreToAdd;
        if (wasFinisher) { RegisterHit(PlayerAttack.WeaponType.Melee, 1); } // Finishers count as melee hits

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
        yield return new WaitForSeconds(3);
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
        roomScore += score;
        totalScore += score;
        totalScoreText.text = totalScore.ToString();
        GameObject popup = pooler.GetFromPool("Score Popup", target.position, Quaternion.identity);
        popup.GetComponent<MeshRenderer>().sortingLayerName = "UI";
        popup.GetComponent<MeshRenderer>().sortingOrder = 100;
        popup.GetComponent<TextMesh>().text = comboEnding ? $"{currentCombo}X COMBO\n+{score}" : $"+{score}";
        yield return new WaitForSeconds(1);
        popup.SetActive(false);
    }

    public void RoomCleared()
    {
        float totalFrequency = attackFrequency.Values.Sum();
        if (totalFrequency == 0) return;

        float hhi = attackFrequency.Values.Sum(frequency =>
        {
            float proportion = frequency / totalFrequency;
            return proportion * proportion;
        });

        varietyScore = Mathf.RoundToInt((1 - hhi) * 1000);
        roomScore += varietyScore;
        totalScore += varietyScore;
        totalScoreText.text = totalScore.ToString();

        // Ensure any active combo is finalised before updating the HUD
        if (currentCombo > 1)
        {
            int comboBonus = currentCombo * 50;
            comboScore += comboBonus;
            StartCoroutine(ScorePopup(comboBonus, playerPos, true));
            currentCombo = 0;
        }

        HudManager.Instance.LevelResults(killScore, comboScore, varietyScore, roomScore);
    }
}