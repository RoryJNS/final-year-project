using UnityEngine;
using System.Collections.Generic;

public class ObjectPooler : MonoBehaviour
{
    public static ObjectPooler Instance { get; private set; }

    [System.Serializable]
    public class Pool
    {
        public string tag;           // Tag to identify the pool
        public GameObject prefab;    // Prefab to instantiate
        public int size;             // Initial pool size
    }

    public List<Pool> pools;
    private Dictionary<string, Queue<GameObject>> poolDictionary;

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

        poolDictionary = new Dictionary<string, Queue<GameObject>>();

        // Initialise pools
        foreach (Pool pool in pools)
        {
            Queue<GameObject> objectPool = new();
            GameObject poolParent = new(pool.tag + " Pool");
            poolParent.transform.SetParent(transform); // Set parent to ObjectPooler GameObject

            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab, poolParent.transform);
                obj.SetActive(false); // Deactivate object initially
                objectPool.Enqueue(obj);
            }

            poolDictionary.Add(pool.tag, objectPool);
        }
    }

    public GameObject GetFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.TryGetValue(tag, out Queue<GameObject> objectPool))
        {
            Debug.LogWarning($"Pool with tag {tag} does not exist.");
            return null;
        }

        GameObject objectToReuse = null;

        for (int i = 0; i < objectPool.Count; i++)
        {
            objectToReuse = objectPool.Dequeue();
            if (!objectToReuse.activeInHierarchy)
                break;
            objectPool.Enqueue(objectToReuse); // Re-enqueue active objects
            objectToReuse = null;
        }

        if (objectToReuse == null)
        {
            Pool pool = pools.Find(p => p.tag == tag);
            GameObject poolParent = GameObject.Find(pool.tag + " Pool");
            objectToReuse = Instantiate(pool.prefab, poolParent.transform);
            pool.size++;
        }

        objectToReuse.SetActive(true);
        objectToReuse.transform.SetPositionAndRotation(position, rotation);
        objectPool.Enqueue(objectToReuse); // Always re-enqueue used objects
        return objectToReuse;
    }

    // Method to return an object to the pool
    public void ReturnToPool(GameObject obj)
    {
        obj.SetActive(false);
    }

    // New method to deactivate all objects in a pool
    public void ClearPool(string tag)
    {
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"Pool with tag {tag} does not exist.");
            return;
        }

        // Get the pool's queue and deactivate all its objects
        Queue<GameObject> poolQueue = poolDictionary[tag];
        foreach (GameObject obj in poolQueue)
        {
            obj.SetActive(false);
        }
    }

    public void ClearAllPools()
    {
        foreach (Pool pool in pools)
        {
            ClearPool(pool.tag);
        }
    }
}