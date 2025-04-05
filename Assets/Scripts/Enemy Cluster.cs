using UnityEngine;

public class EnemyCluster : MonoBehaviour
{
    public GeneticAlgorithm.DifficultyChromosome difficulty;
    public Vector3 investigatePos;
    public float investigateTimer; // How long since investigatePos was updated

    private void Update()
    {
        investigateTimer += Time.deltaTime;
    }

    public void InitialiseEnemy(Enemy enemy)
    {
        enemy.enabled = true;
        DungeonGenerator.Instance.enemies.Add(enemy);
        enemy.ResetEnemy();
        enemy.SetCluster(this, difficulty); // Assign enemy to this cluster and apply this cluster's difficulty
    }

    public void RemoveEnemy(Enemy enemy)
    {
        DungeonGenerator.Instance.enemies.Remove(enemy);
        if (DungeonGenerator.Instance.enemies.Count == 0)
        {
            int idx = GeneticAlgorithm.Instance.population.IndexOf(difficulty);
            GeneticAlgorithm.Instance.population[idx].EvaluateFairness();
            ScoreSystem.Instance.RoomCleared();
            DungeonGenerator.Instance.currentMainRoom.RoomCleared();
        }
    }

    public void Alert(Vector3 position)
    {
        investigatePos = position;
        investigateTimer = 0;
    }
}