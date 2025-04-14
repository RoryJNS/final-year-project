using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Analytics;

public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }
    [SerializeField] private bool isInitialised;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private async void Start()
    {
        await UnityServices.InitializeAsync();
        AnalyticsService.Instance.StartDataCollection();
        isInitialised = true;
    }

    public void LevelCompleted(
            float percentHealthArmourRemaining,
            float fairestEnemyHealth,
            float fairestAttackRangeModifier,
            float fairestAccuracyModifier,
            float fairestDamageModifier,
            float averageFairness,
            float bestFairness)
    {
        if (!isInitialised) { return; }

        CustomEvent customEvent = new("level_completed")
        {
            { "dynamic_difficulty", DifficultyManager.Instance.dynamic },
            { "level_number", Mathf.FloorToInt(ScoreSystem.Instance.roomsCleared / 5f) },
            { "average_fairness", averageFairness },
            { "highest_fairness", bestFairness },
            { "fairest_enemy_health", fairestEnemyHealth },
            { "fairest_attack_range_modifier", fairestAttackRangeModifier },
            { "fairest_accuracy_modifier", fairestAccuracyModifier },
            { "fairest_damage_modifier", fairestDamageModifier },
            { "percent_health_armour_remaining", percentHealthArmourRemaining }
        };

        AnalyticsService.Instance.RecordEvent(customEvent);
    }

    public void RunEnded(bool isPlayerAlive) 
    {
        if (!isInitialised) { return; }

        CustomEvent customEvent = new ("run_completed")
        {
            { "dynamic_difficulty", DifficultyManager.Instance.dynamic },
            { "run_number", PlayerPrefs.GetInt("RunNumber") },
            { "succeeded", isPlayerAlive },
            { "rooms_completed", ScoreSystem.Instance.roomsCleared },
            { "levels_completed", Mathf.FloorToInt(ScoreSystem.Instance.roomsCleared / 5f) },
            { "run_score", ScoreSystem.Instance.totalScore }
        };

        AnalyticsService.Instance.RecordEvent(customEvent);
    }
}