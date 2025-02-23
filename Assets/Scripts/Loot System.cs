using UnityEngine;
using System.Collections.Generic;

public class LootSystem : MonoBehaviour
{
    public static LootSystem Instance { get; private set; }
    public List<LootTable> lootTables = new(); // Stores all loot tables
    [SerializeField] ObjectPooler pooler;

    [System.Serializable]
    public class LootTable
    {
        public string tableName; // Name of the loot table (e.g., "Goblin", "Chest", etc.)
        public LootEntry[] lootEntries;
    }

    [System.Serializable]
    public class LootEntry
    {
        public GameObject itemPrefab;
        [Range(0f, 1f)] public float dropChance;
        public int minAmount = 1, maxAmount = 3;
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

    public void DropLoot(string tableName, Vector3 position)
    {
        LootTable table = lootTables.Find(t => t.tableName == tableName);
        if (table == null)
        {
            Debug.LogWarning($"No loot table found for: {tableName}");
            return;
        }

        float roll = Random.value;
        float cumulativeChance = 0f;

        foreach (var entry in table.lootEntries)
        {
            cumulativeChance += entry.dropChance;
            if (roll <= cumulativeChance)
            {
                int amount = Random.Range(entry.minAmount, entry.maxAmount);
                GameObject loot = pooler.GetFromPool(entry.itemPrefab.name, position, Quaternion.identity);
                if (loot.TryGetComponent<ResourcePickup>(out var resourcePickup))
                {
                    resourcePickup.amount = amount;
                }
                return; // Only drop one item per call
            }
        }
    }
}