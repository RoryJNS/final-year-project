using UnityEngine;

public class EnemyCluster : MonoBehaviour
{
    public DifficultyManager.DifficultyChromosome difficulty;
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
            int idx = DifficultyManager.Instance.population.IndexOf(difficulty);
            if (idx == DifficultyManager.Instance.population.Count - 1) { SoundManager.FadeOutMusic(); } // Fade out music after finishing the last room in the level
            DifficultyManager.Instance.population[idx].EvaluateFairness();
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