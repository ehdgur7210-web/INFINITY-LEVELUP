using UnityEngine;
using UnityEngine.Tilemaps;

public class InfiniteTilemapScroller : MonoBehaviour
{
    [Header("Tilemap Settings")]
    public Tilemap tilemap;
    public Grid grid;

    [Header("Scroll Settings")]
    public float scrollSpeed = 2f;
    public Vector2 scrollDirection = Vector2.left;

    [Header("Chunk Settings")]
    public int chunkWidth = 20;  // 타일 너비
    public int chunkHeight = 10; // 타일 높이
    public int visibleChunks = 3; // 화면에 보이는 청크 수

    [Header("References")]
    public Transform player;
    public TileBase[] groundTiles;
    public TileBase[] obstacleTiles;

    private Vector3 lastPlayerPosition;
    private int currentChunkIndex = 0;

    void Start()
    {
        if (tilemap == null)
            tilemap = GetComponent<Tilemap>();

        if (grid == null)
            grid = GetComponentInParent<Grid>();

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        lastPlayerPosition = player != null ? player.position : Vector3.zero;

        // 초기 청크 생성
        GenerateInitialChunks();
    }

    void Update()
    {
        ScrollTilemap();

        if (player != null)
        {
            CheckChunkGeneration();
        }
    }

    void ScrollTilemap()
    {
        transform.Translate(scrollDirection.normalized * scrollSpeed * Time.deltaTime);
    }

    void GenerateInitialChunks()
    {
        for (int i = 0; i < visibleChunks; i++)
        {
            GenerateChunk(i);
        }
    }

    void CheckChunkGeneration()
    {
        float distanceMoved = Vector3.Distance(player.position, lastPlayerPosition);

        if (distanceMoved > chunkWidth / 2f)
        {
            currentChunkIndex++;
            GenerateChunk(currentChunkIndex + visibleChunks - 1);
            RemoveOldChunk(currentChunkIndex - 1);
            lastPlayerPosition = player.position;
        }
    }

    void GenerateChunk(int chunkIndex)
    {
        int startX = chunkIndex * chunkWidth;

        for (int x = 0; x < chunkWidth; x++)
        {
            for (int y = 0; y < chunkHeight; y++)
            {
                Vector3Int tilePosition = new Vector3Int(startX + x, y, 0);

                // 바닥 타일
                if (y == 0 && groundTiles.Length > 0)
                {
                    TileBase tile = groundTiles[Random.Range(0, groundTiles.Length)];
                    tilemap.SetTile(tilePosition, tile);
                }
                // 장애물 (10% 확률)
                else if (y > 0 && y < 3 && Random.value < 0.1f && obstacleTiles.Length > 0)
                {
                    TileBase tile = obstacleTiles[Random.Range(0, obstacleTiles.Length)];
                    tilemap.SetTile(tilePosition, tile);
                }
            }
        }

        Debug.Log($"Generated chunk {chunkIndex} at x: {startX}");
    }

    void RemoveOldChunk(int chunkIndex)
    {
        if (chunkIndex < 0) return;

        int startX = chunkIndex * chunkWidth;

        for (int x = 0; x < chunkWidth; x++)
        {
            for (int y = 0; y < chunkHeight; y++)
            {
                Vector3Int tilePosition = new Vector3Int(startX + x, y, 0);
                tilemap.SetTile(tilePosition, null);
            }
        }

        Debug.Log($"Removed chunk {chunkIndex}");
    }
}
