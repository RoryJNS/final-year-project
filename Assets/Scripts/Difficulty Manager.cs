using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }

    [System.Serializable]
    public class DifficultyChromosome
    {
        public int health;
        public float attackRangeModifier, accuracyModifier, damageModifier;
        public float fairness;

        public DifficultyChromosome(int health, float attackRangeModifier, float accuracyModifier, float damageModifier)
        {
            this.health = health;
            this.attackRangeModifier = attackRangeModifier;
            this.accuracyModifier = accuracyModifier;
            this.damageModifier = damageModifier;
        }

        public DifficultyChromosome Clone()
        {
            return new DifficultyChromosome(health, attackRangeModifier, accuracyModifier, damageModifier);
        }

        public void EvaluateFairness()
        {
            (float rawEnemyEffectiveness, float aggression) = Instance.playerAttack.GetPerformanceData();

            // Default expected damage taken in the room is 360 (60% of max health/armour), optimal combatTime is 20 seconds
            // Player will likely average 3 executions per room (300 armour regained) so will be in a deficit over time

            // Expected damage taken is higher for aggressive players, lower for defensive ones
            float expectedEffectiveness = Mathf.Lerp(0.3f, 0.9f, aggression); // 30% for defensive, 90% for aggressive, 60% for perfectly balanced

            // fairness = Mathf.Clamp01(1 - Mathf.Abs(rawEnemyEffectiveness - expectedEffectiveness) / expectedEffectiveness); // Normalise distance from expected effectiveness
            // fairness = 1 - Mathf.Abs(rawEnemyEffectiveness - expectedEffectiveness) / Mathf.Max(rawEnemyEffectiveness, expectedEffectiveness);

            float difference = Mathf.Abs(rawEnemyEffectiveness - expectedEffectiveness);
            float scalingFactor = 1.5f; // Tweak this for how quickly fairness drops
            fairness = Mathf.Exp(-difference * scalingFactor); // = 1 when difference is 0, 0.5 when difference is 0.33, 0 when difference is 0.66 or greater
            // Debug.Log("Aggressiveness: " + aggression + ", actual/expected effectiveness: " + rawEnemyEffectiveness + "/" + expectedEffectiveness + ", fairness: " + fairness);
        }
    }

    [System.Serializable]
    public class EnemyWeaponStats
    {
        public int damage, ammoPerClip;
        public float fireRate, reloadSpeed, attackRange, accuracy;
    }

    public EnemyWeaponStats[] enemyWeaponStats;
    public List<DifficultyChromosome> initialDynamicPopulation, staticPopulation;
    public List<DifficultyChromosome> population;
    public bool dynamic;
    [SerializeField] private PlayerAttack playerAttack;

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

    public void InitialiseDifficulties()
    {
        if (dynamic) { population = initialDynamicPopulation; }
        else { population = staticPopulation; }
    }

    public void SendLevelData()
    {
        population.Sort((a, b) => b.fairness.CompareTo(a.fairness));
        AnalyticsManager.Instance.LevelCompleted(
            playerAttack.percent_health_armour_remaining, 
            population[0].health, 
            population[0].attackRangeModifier, 
            population[0].accuracyModifier, 
            population[0].damageModifier, 
            population.Average(c => c.fairness), 
            population[0].fairness);
    }

    public void GenerateNewDifficulties()
    {
        List<DifficultyChromosome> newPopulation = new();

        // Elitism: Retain the best-performing chromosome
        population.Sort((a, b) => b.fairness.CompareTo(a.fairness));
        newPopulation.Add(population[0].Clone()); // Use Clone() to avoid modifying original
        newPopulation[0].fairness = 0; // Reset fairness cloned from the original

        while (newPopulation.Count < population.Count)
        {
            DifficultyChromosome child = Mutate(population[0].Clone(), newPopulation.Count-1);
            newPopulation.Add(child);
        }

        population = newPopulation;
        Shuffle(population);
    }

    public static void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randIndex = Random.Range(i, list.Count);
            (list[randIndex], list[i]) = (list[i], list[randIndex]);
        }
    }

    private DifficultyChromosome Mutate(DifficultyChromosome child, int geneIndex)
    {
        switch (geneIndex)
        {
            case 0: child.health = Mathf.Max(50, child.health + Random.Range(-50, 51)); break;
            case 1: child.attackRangeModifier = Mathf.Max(0.1f, child.attackRangeModifier + Random.Range(-0.5f, + 0.5f)); break;
            case 2: child.accuracyModifier = Mathf.Max(0.1f, child.accuracyModifier + Random.Range(-0.5f, + 0.5f)); break;
            case 3: child.damageModifier = Mathf.Max(0.1f, child.damageModifier + Random.Range(-0.5f, +0.5f)); break;
        }

        return child;
    }
}