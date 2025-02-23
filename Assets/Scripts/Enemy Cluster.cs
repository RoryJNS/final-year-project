using UnityEngine;
using System.Collections.Generic;

public class EnemyCluster : MonoBehaviour
{
    [SerializeField] private float aggressionLevel, healthModifier, damageModifier;
    [SerializeField] private List<Enemy> enemies = new();
    public List<Enemy> staggeredEnemies = new();

    public Vector3 investigatePos;
    public float investigateTimer; // How long since investigatePos was updated

    private void Update()
    {
        investigateTimer += Time.deltaTime;
    }

    public void InitialiseEnemy(Enemy enemy)
    {
        enemy.enabled = true;
        enemies.Add(enemy);
        enemy.SetCluster(this);
        enemy.ResetEnemy();
    }

    public void RemoveEnemy(Enemy enemy)
    {
        enemies.Remove(enemy);
        if (enemies.Count == 0)
        {
            DungeonGenerator.Instance.currentMainRoom.RoomCleared();
        }
    }

    public void Alert(Vector3 position)
    {
        investigatePos = position;
        investigateTimer = 0;
    }
}