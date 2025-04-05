using UnityEngine;
using System.Collections.Generic;

public class GeneticAlgorithm : MonoBehaviour
{
    public static GeneticAlgorithm Instance { get; private set; }

    [System.Serializable]
    public class DifficultyChromosome
    {
        public int enemyCount, health;
        public float attackRangeModifier, accuracyModifier, damageModifier;
        public float expectedPerformance;
        public float fairness;

        public DifficultyChromosome(int enemyCount, int health, float attackRangeModifier, float accuracyModifier, float damageModifier)
        {
            this.enemyCount = enemyCount;
            this.health = health;
            this.attackRangeModifier = attackRangeModifier;
            this.accuracyModifier = accuracyModifier;
            this.damageModifier = damageModifier;
            expectedPerformance = this.enemyCount * 50f; // Ideally, each enemy does 50 damage to the player
        }

        public DifficultyChromosome Clone()
        {
            return new DifficultyChromosome(enemyCount, health, attackRangeModifier, accuracyModifier, damageModifier);
        }

        public void EvaluateFairness()
        {
            float playerPerformance = Instance.playerAttack.EvaluatePerformance();
            float rawFairness = playerPerformance / expectedPerformance;

            // Normalise fairness to between 0 and 1
            float k = 2.5f; // Sensitivity factor, higher values mean a stricter fairness scaling
            fairness = 1 / (1 + k * Mathf.Abs(rawFairness - 1));
            Debug.Log("Performance: " + playerPerformance + ", raw fairness: " + rawFairness + ", normalised fairness: " + fairness);
        }
    }

    [System.Serializable]
    public class EnemyWeaponStats
    {
        public int damage, ammoPerClip;
        public float fireRate, reloadSpeed, attackRange, accuracy;
    }

    public EnemyWeaponStats[] enemyWeaponStats;
    public List<DifficultyChromosome> population;
    [SerializeField] private PlayerAttack playerAttack;
    [SerializeField] private int elitismCount;

    [Range(0f, 1f)] public float mutationRate, crossoverRate;

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

    public void InitialisePopulation()
    {
        population = new List<DifficultyChromosome>();

        for (int i = 0; i < 5; i++)
        {
            int enemyCount = Random.Range(5, 9); // 5-8 enemies
            int health = Random.Range(100, 300);
            float attackRangeModifier = Random.Range(0.7f, 1.4f);
            float accuracyModifier = Random.Range(0.5f, 1.5f);
            float damageModifier = Random.Range(0.7f, 1.4f);

            population.Add(new DifficultyChromosome(enemyCount, health, attackRangeModifier, accuracyModifier, damageModifier));
        }
    }

    public void GenerateNewPopulation()
    {
        List<DifficultyChromosome> newPopulation = new();

        // Elitism: Retain the best-performing chromosomes
        population.Sort((a, b) => b.fairness.CompareTo(a.fairness));
        for (int i = 0; i < elitismCount; i++)
        {
            newPopulation.Add(population[i].Clone()); // Use Clone() to avoid modifying originals
            newPopulation[i].fairness = 0; // Reset fairness cloned from the original
        }

        // Crossover to generate new individuals
        while (newPopulation.Count < population.Count)
        {
            DifficultyChromosome parent1 = population[Random.Range(0, elitismCount)];
            DifficultyChromosome parent2 = population[Random.Range(0, elitismCount)];
            DifficultyChromosome child;

            if (Random.value < crossoverRate)
            {
                child = Crossover(parent1, parent2);
            }
            else
            {
                // No crossover – clone one parent
                child = parent1.Clone();
            }

            if (Random.value < mutationRate)
            {
                child = Mutate(child);
            }

            newPopulation.Add(child);
        }

        population = newPopulation;
    }

    private DifficultyChromosome Crossover(DifficultyChromosome parent1, DifficultyChromosome parent2)
    {
        /*
        int crossoverPoint = Random.Range(1, 4); // Single point crossover
        return new DifficultyChromosome(
            crossoverPoint < 1 ? parent1.enemyCount : parent2.enemyCount,
            crossoverPoint < 2 ? parent1.health : parent2.health,
            crossoverPoint < 3 ? parent1.attackRangeModifier : parent2.attackRangeModifier,
            crossoverPoint < 4 ? parent1.accuracyModifier : parent2.accuracyModifier,
        );
        */

        return new DifficultyChromosome(
            Mathf.RoundToInt((parent1.enemyCount + parent2.enemyCount) / 2f),
            Mathf.RoundToInt((parent1.health + parent2.health) / 2f),
            (parent1.attackRangeModifier + parent2.attackRangeModifier) / 2f,
            (parent1.accuracyModifier + parent2.accuracyModifier) / 2f,
            (parent1.damageModifier + parent2.damageModifier) / 2f
        );
    }

    private DifficultyChromosome Mutate(DifficultyChromosome child)
    {
        int gene = Random.Range(0, 5); // Pick a random gene to apply some mutation to

        switch (gene)
        {
            case 0: child.enemyCount = Mathf.Max(3, child.enemyCount + Random.Range(-1, 1)); break;
            case 1: child.health = Mathf.Max(50, child.health + Random.Range(-50, 50)); break;
            case 2: child.attackRangeModifier = Mathf.Max(0.1f, child.attackRangeModifier + Random.Range(-0.5f, + 0.5f)); break;
            case 3: child.accuracyModifier = Mathf.Max(0.1f, child.accuracyModifier + Random.Range(-0.5f, + 0.5f)); break;
            case 4: child.damageModifier = Mathf.Max(0.1f, child.damageModifier + Random.Range(-0.5f, +0.5f)); break;
        }

        child.expectedPerformance = child.enemyCount * 50f; // Recalculate in case enemy count changed
        return child;
    }
}