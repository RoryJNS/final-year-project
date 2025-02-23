using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Linq;

public class DungeonGenerator : MonoBehaviour
{
    public static DungeonGenerator Instance { get; private set; }
    public MainRoom currentMainRoom; 

    [SerializeField] private int minMainRoomSize, maxMainRoomSize;
    [SerializeField] private float sideRoomProbability;
    [SerializeField] private GameObject enemyClusterParent, enemyClusterPrefab, chestPrefab;
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private TileBase dungeonTile;
    [SerializeField] private ObjectPooler pooler;
    [SerializeField] private NavMeshPlus.Components.NavMeshSurface navMeshSurface;
    [SerializeField] private int mainRoomCount = 5;
    [SerializeField] private HashSet<MainRoom> mainRooms = new();

    private readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
    private readonly Vector2Int sideRoomSize = new(10, 10);
    private readonly HashSet<Room> otherRooms = new();
    private Vector2Int endRoomCenter = Vector2Int.zero;

    [System.Serializable]
    public class Room
    {
        public Vector2Int center;
        public Vector2Int size;
        
        public Room(Vector2Int center, Vector2Int size)
        {
            this.center = center;
            this.size = size;
        }
    }

    [System.Serializable]
    public class MainRoom : Room
    {
        public int roomNumber, coverCount;
        public EnemyCluster enemyCluster;
        public HashSet<Vector2> enemyPositions = new();
        public HashSet<Vector2> coverPositions = new();
        public HashSet<Teleporter> teleporters = new();

        public MainRoom(Vector2Int center, Vector2Int size) : base(center, size) // Also call base constructor
        {
            coverCount = (size.x * size.y) / 60; // One piece of cover for every 60 tiles in the room, 4-5 average
        }

        public void LockRoom()
        {
            foreach (Teleporter teleporter in teleporters)
            {
                teleporter.Lock();
            }
        }

        public void RoomCleared()
        {
            ScoreSystem.Instance.RoomCleared();

            foreach (Teleporter teleporter in teleporters)
            {
                teleporter.Unlock();
            }
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); // Prevent duplicates
        }
    }

    private void Start()
    {
        GenerateDungeon();
        navMeshSurface.BuildNavMesh();
    }

    private void GenerateDungeon()
    {
        tilemap.ClearAllTiles();
        foreach (ObjectPooler.Pool pool in pooler.pools)
        {
            pooler.ClearPool(pool.tag);
        }
        mainRooms.Clear();
        otherRooms.Clear();
        GenerateMainRooms();
        GenerateSideRooms();
    }

    private void GenerateMainRooms()
    {
        Room startRoom = new(Vector2Int.zero, sideRoomSize);
        DrawRoom(startRoom);
        Vector2Int previousCenter = startRoom.center;

        for (int i = 1; i <= mainRoomCount; i++)
        {
            Vector2Int newRoomCenter;
            Vector2Int roomSize = GetRandomRoomSize(minMainRoomSize, maxMainRoomSize);

            do
            {
                Vector2Int direction = Directions[Random.Range(0, Directions.Length)];
                newRoomCenter = previousCenter + direction * (maxMainRoomSize + 10);
            } while (DoesRoomOverlap(newRoomCenter));

            MainRoom mainRoom = new(newRoomCenter, roomSize) { roomNumber = i };
            mainRooms.Add(mainRoom);
            DrawRoom(mainRoom);
            PopulateMainRoom(mainRoom);
            previousCenter = newRoomCenter;
        }

        Room previousRoom = startRoom;
        foreach (Room room in mainRooms)
        {
            DrawCorridor(previousRoom, room);
            previousRoom = room;
        }

        Vector2Int tempEndRoomCenter;
        do
        {
            tempEndRoomCenter = previousCenter + Directions[Random.Range(0, Directions.Length)] * (maxMainRoomSize + 10);
        } while (DoesRoomOverlap(tempEndRoomCenter));

        endRoomCenter = tempEndRoomCenter;
        Room endRoom = new(endRoomCenter, sideRoomSize);
        DrawRoom(endRoom);
        pooler.GetFromPool("Floor Exit", (Vector2)endRoomCenter, Quaternion.identity);
        DrawCorridor(previousRoom, endRoom);
    }

    private void GenerateSideRooms()
    {
        foreach (Room mainRoom in mainRooms)
        {
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int sideRoomCenter = mainRoom.center + direction * (maxMainRoomSize + 10);
                if (!DoesRoomOverlap(sideRoomCenter) && Random.value <= sideRoomProbability)
                {
                    Room sideRoom = new(sideRoomCenter, sideRoomSize);
                    otherRooms.Add(sideRoom);
                    DrawRoom(sideRoom);
                    pooler.GetFromPool("Chest", (Vector2)sideRoom.center, Quaternion.identity);
                    DrawCorridor(mainRoom, sideRoom);
                }
            }
        }
    }

    private Vector2Int GetRandomRoomSize(int minSize, int maxSize)
    {
        int width = Random.Range(minSize, maxSize);
        int height = Random.Range(minSize, maxSize);
        if (width % 2 != 0) width++;
        if (height % 2 != 0) height++; // Force dimensions to be even numbered so corridors don't look weird
        return new Vector2Int(width, height);
    }

    private bool DoesRoomOverlap(Vector2Int roomCenter)
    {
        return mainRooms.Any(room => room.center == roomCenter) || otherRooms.Any(room => room.center == roomCenter) 
            || roomCenter == Vector2Int.zero || (roomCenter == endRoomCenter); 
    }

    private void DrawRoom(Room room) // Draw the tiles
    {
        Vector2Int bottomLeft = new(room.center.x - room.size.x / 2, room.center.y - room.size.y / 2);
        List<Vector3Int> tilePositions = new();

        for (int x = 0; x < room.size.x; x++)
        {
            for (int y = 0; y < room.size.y; y++)
            {
                tilePositions.Add(new Vector3Int(bottomLeft.x + x, bottomLeft.y + y, 0));
            }
        }

        tilemap.SetTiles(tilePositions.ToArray(), Enumerable.Repeat(dungeonTile, tilePositions.Count).ToArray());
    }

    private void DrawCorridor(Room startRoom, Room endRoom) // Spawn teleporters and draw the exit tiles
    {
        Vector3Int teleportTherePos = new(
            startRoom.center.x + Mathf.Clamp(endRoom.center.x - startRoom.center.x, -startRoom.size.x / 2, startRoom.size.x / 2),
            startRoom.center.y + Mathf.Clamp(endRoom.center.y - startRoom.center.y, -startRoom.size.y / 2, startRoom.size.y / 2),
            0
        );

        Vector3Int teleportBackPos = new(
            endRoom.center.x + Mathf.Clamp(startRoom.center.x - endRoom.center.x, -endRoom.size.x / 2, endRoom.size.x / 2),
            endRoom.center.y + Mathf.Clamp(startRoom.center.y - endRoom.center.y, -endRoom.size.y / 2, endRoom.size.y / 2),
            0
        );

        pooler.GetFromPool("Teleporter", teleportTherePos, Quaternion.identity).TryGetComponent(out Teleporter teleporter1);
        pooler.GetFromPool("Teleporter", teleportBackPos, Quaternion.identity).TryGetComponent(out Teleporter teleporter2);
        teleporter1.SetDestination(teleporter2);
        teleporter2.SetDestination(teleporter1);

        if (startRoom is MainRoom mainStartRoom)
        {
            mainStartRoom.teleporters.Add(teleporter1); // All exits from a main room are tracked
        }
        if (endRoom is MainRoom mainEndRoom)
        {
            mainEndRoom.teleporters.Add(teleporter2); // All exits from a main room are tracked
            teleporter1.linkedMainRoom = mainEndRoom;
        }

        if (teleportTherePos.x != teleportBackPos.x) // Horizontal corridor, 2x4
        {
            for (int xOffset = -1; xOffset <= 0; xOffset++)
            {
                for (int yOffset = -2; yOffset <= 1; yOffset++)
                {
                    tilemap.SetTile(new Vector3Int(teleportTherePos.x + xOffset, teleportTherePos.y + yOffset, 0), dungeonTile);
                    tilemap.SetTile(new Vector3Int(teleportBackPos.x + xOffset, teleportBackPos.y + yOffset, 0), dungeonTile);
                }
            }
        }
        else // Vertical corridor, 4x2
        {
            for (int xOffset = -2; xOffset <= 1; xOffset++)
            {
                for (int yOffset = -1; yOffset <= 0; yOffset++)
                {
                    tilemap.SetTile(new Vector3Int(teleportTherePos.x + xOffset, teleportTherePos.y + yOffset, 0), dungeonTile);
                    tilemap.SetTile(new Vector3Int(teleportBackPos.x + xOffset, teleportBackPos.y + yOffset, 0), dungeonTile);
                }
            }
        }
    }

    private void PopulateMainRoom(MainRoom room) // Determine enemy positions and spawn cover
    {
        room.enemyCluster = Instantiate(enemyClusterPrefab, (Vector2)room.center, Quaternion.identity, enemyClusterParent.transform).GetComponent<EnemyCluster>();
        List<Vector2Int> availablePositions = new();

        // Generate grid positions
        for (int x = room.center.x - room.size.x / 4; x <= room.center.x + room.size.x / 4; x++)
        {
            for (int y = room.center.y - room.size.y / 4; y <= room.center.y + room.size.y / 4; y++)
            {
                availablePositions.Add(new Vector2Int(x, y));
            }
        }

        // Shuffle positions for randomness
        availablePositions = availablePositions.OrderBy(p => Random.value).ToList();

        // Place enemies first
        for (int i = 0; i < 5 && availablePositions.Count > 0; i++)
        {
            Vector2Int enemyPos = availablePositions.FirstOrDefault(pos =>
                room.enemyPositions.All(e => (e - pos).sqrMagnitude >= 9)); // At least 3 units apart

            if (enemyPos != default)
            {
                room.enemyPositions.Add(enemyPos);
                availablePositions.Remove(enemyPos);
            }
        }

        bool shouldRotate = false;

        // Place cover
        for (int i = 0; i < room.coverCount && availablePositions.Count > 0; i++)
        {
            Vector2Int coverPos = availablePositions.FirstOrDefault(pos =>
                room.enemyPositions.All(e => (e - pos).sqrMagnitude >= 3) &&  // Avoid enemy proximity
                room.coverPositions.All(c => (c - pos).sqrMagnitude >= 12)); // Keep cover pieces apart

            if (coverPos != default)
            {
                room.coverPositions.Add(coverPos);
                availablePositions.Remove(coverPos);

                Quaternion rotation = shouldRotate ? Quaternion.Euler(0, 0, 90) : Quaternion.identity;
                shouldRotate = !shouldRotate;
                pooler.GetFromPool("Cover", new Vector3(coverPos.x, coverPos.y, 0), rotation);
            }
        }
    }

    public void SpawnEnemies()
    {
        foreach (Vector2 position in currentMainRoom.enemyPositions)
        {
            GameObject newEnemy = pooler.GetFromPool("Rifle Enemy", position, Quaternion.identity);
            currentMainRoom.enemyCluster.InitialiseEnemy(newEnemy.GetComponent<Enemy>());
        }
    }

    public List<Enemy> FindStaggeredEnemies()
    {
        return currentMainRoom.enemyCluster.staggeredEnemies;
    }
}