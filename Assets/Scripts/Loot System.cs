using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LootSystem : MonoBehaviour
{
    public static LootSystem Instance { get; private set; }
    [SerializeField] private LootEntry[] baseLootTable;
    [SerializeField] private PlayerAttack playerAttack;
    [SerializeField] private List<DebugLootWeight> debugLootWeights = new();

    [System.Serializable]
    public class LootEntry
    {
        public GameObject prefab;
        public float baseDropRate;
        public int minAmount, maxAmount;
    }

    [System.Serializable]
    public class DebugLootWeight
    {
        public string itemName;
        public float weight;
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public GameObject DropLoot(Vector3 position, string enemyType = null)
    {
        List<(LootEntry item, float weight)> adjustedLoot = new(); // Clear old weights first

        var (healthRatio, armourRatio, weaponType, rifleAmmoRatio, smgAmmoRatio, shotgunAmmoRatio) = playerAttack.GetInventory(); // Get inventory info

        // Calculate a new weight for each item based on need
        foreach (var item in baseLootTable)
        {
            float weight = item.baseDropRate;

            // Restrict loot by enemyType
            if (!string.IsNullOrEmpty(enemyType))
            {
                if ((item.prefab.name == "Rifle Ammo" && enemyType != "Rifle") ||
                    (item.prefab.name == "SMG Ammo" && enemyType != "SMG") ||
                    (item.prefab.name == "Shotgun Ammo" && enemyType != "Shotgun"))
                {
                    continue;
                }

                else if (item.prefab.CompareTag("Weapon") && item.prefab.name != enemyType)
                {
                    continue;
                }

                else if (item.prefab.CompareTag("Armour"))
                {
                    continue; // Enemies don't drop armour unless via a finisher
                }
            }

            switch (item.prefab.name)
            {
                case "Health":
                    float healthNeed = 1f - healthRatio;
                    weight *= Mathf.Lerp(0, 2.0f, healthNeed);
                    break;

                case "Armour":
                    float armourNeed = 1f - armourRatio;
                    weight *= Mathf.Lerp(0, 2.0f, armourNeed);
                    break;

                case "Rifle Ammo":
                    weight *= Mathf.Lerp(0.5f, 2.0f, 1f - rifleAmmoRatio);
                    break;

                case "SMG Ammo":
                    weight *= Mathf.Lerp(0.5f, 2.0f, 1f - smgAmmoRatio);
                    break;

                case "Shotgun Ammo":
                    weight *= Mathf.Lerp(0.5f, 2.0f, 1f - shotgunAmmoRatio);
                    break;
            }

            if (item.prefab.name == weaponType)
                weight *= 0.5f;
            else if (item.prefab.CompareTag("Weapon"))
                weight *= 2.5f;

            adjustedLoot.Add((item, weight));

        }

        debugLootWeights.Clear();
        foreach (var (item, weight) in adjustedLoot)
        {
            debugLootWeights.Add(new DebugLootWeight
            {
                itemName = item.prefab.name,
                weight = weight
            });
        }

        // Weighted random selection
        float total = adjustedLoot.Sum(entry => entry.weight);
        float rand = Random.Range(0, total);
        float running = 0;

        foreach (var (item, weight) in adjustedLoot)
        {
            running += weight;
            if (rand <= running)
            {
                // Use object pooling to spawn
                GameObject loot = ObjectPooler.Instance.GetFromPool(item.prefab.name, position, Quaternion.identity);

                // Apply amount if applicable
                if (loot.TryGetComponent<ResourcePickup>(out var pickup))
                {
                    int amount = Random.Range(item.minAmount, item.maxAmount + 1);
                    pickup.amount = amount;
                }

                return loot;
            }
        }

        return null;
    }
}