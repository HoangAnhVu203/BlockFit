using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private HolderBoardController holderBoard;
    [SerializeField] private TrayBoardController trayBoard;
    [SerializeField] private GameObject singleBlockPrefab;

    [Header("Level Prefabs")]
    [SerializeField] private List<LevelPrefab> levelPrefabs = new List<LevelPrefab>();
    [SerializeField] private int startingLevelIndex;

    [Header("Level Data")]
    [SerializeField] private Vector2Int holderSize = new Vector2Int(7, 7);
    [SerializeField] private Vector2Int traySize = new Vector2Int(7, 3);
    [SerializeField] private List<HolderCellData> holderCells = new List<HolderCellData>();
    [SerializeField] private List<FixedHolderBlockData> fixedBlocks = new List<FixedHolderBlockData>();
    [SerializeField] private List<PieceWaveData> waves = new List<PieceWaveData>();

    private readonly List<Block> allBlocks = new List<Block>();
    private readonly List<Block> currentWaveBlocks = new List<Block>();
    private int currentWaveIndex;
    private int activeLevelIndex = -1;
    private bool allWavesSpawned;

    public int CurrentLevelNumber
    {
        get
        {
            if (levelPrefabs.Count == 0)
            {
                return Mathf.Max(1, startingLevelIndex + 1);
            }

            int currentLevelIndex = activeLevelIndex >= 0 ? activeLevelIndex : startingLevelIndex;
            currentLevelIndex = Mathf.Clamp(currentLevelIndex, 0, levelPrefabs.Count - 1);
            return currentLevelIndex + 1;
        }
    }

    public bool HasNextLevel
    {
        get
        {
            return GetPlayableLevelCount() > 0;
        }
    }

    public void StartLevel()
    {
        LoadConfiguredLevelPrefabIfAvailable();
        ClearSpawnedBlocks();

        if (holderBoard == null || trayBoard == null || singleBlockPrefab == null)
        {
            Debug.LogError("LevelManager is missing holderBoard, trayBoard, or singleBlockPrefab.");
            return;
        }

        float prefabCellSize = GetPrefabCellSize(singleBlockPrefab);
        holderBoard.SetBaseCellSize(prefabCellSize);
        trayBoard.SetBaseCellSize(prefabCellSize);

        holderBoard.BuildHolder(holderSize, holderCells);
        trayBoard.BuildTray(traySize);
        SpawnFixedBlocks();
        RefreshCompletedBlockShapeSprites();

        currentWaveIndex = 0;
        allWavesSpawned = waves.Count == 0;

        if (waves.Count > 0)
        {
            SpawnWave(currentWaveIndex);
        }
        else if (holderBoard.IsComplete() && GameManager.Instance != null)
        {
            GameManager.Instance.Win();
        }
    }

    public void PlayLevel(int levelIndex)
    {
        if (!LoadLevel(levelIndex))
        {
            return;
        }

        StartLevel();
    }

    public bool LoadLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= levelPrefabs.Count || levelPrefabs[levelIndex] == null)
        {
            Debug.LogWarning($"LevelManager cannot load level index {levelIndex}.");
            return false;
        }

        ApplyLevelPrefab(levelPrefabs[levelIndex]);
        activeLevelIndex = levelIndex;
        return true;
    }

    public bool LoadNextLevel()
    {
        if (levelPrefabs.Count == 0)
        {
            return false;
        }

        int currentLevelIndex = activeLevelIndex >= 0 ? activeLevelIndex : Mathf.Clamp(startingLevelIndex, 0, levelPrefabs.Count - 1);
        for (int offset = 1; offset <= levelPrefabs.Count; offset++)
        {
            int nextLevelIndex = (currentLevelIndex + offset) % levelPrefabs.Count;
            if (levelPrefabs[nextLevelIndex] != null)
            {
                return LoadLevel(nextLevelIndex);
            }
        }

        return false;
    }

    public void HandleBlockPlaced(Block block)
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Gameplay)
        {
            return;
        }

        RefreshCompletedBlockShapeSprites();

        if (holderBoard.IsComplete() && allWavesSpawned)
        {
            GameManager.Instance.Win();
            return;
        }

        if (IsCurrentWavePlaced())
        {
            SpawnNextWave();
        }
    }

    private void LoadConfiguredLevelPrefabIfAvailable()
    {
        if (levelPrefabs.Count == 0)
        {
            return;
        }

        int levelIndex = activeLevelIndex >= 0 ? activeLevelIndex : startingLevelIndex;
        levelIndex = Mathf.Clamp(levelIndex, 0, levelPrefabs.Count - 1);

        if (levelPrefabs[levelIndex] != null)
        {
            ApplyLevelPrefab(levelPrefabs[levelIndex]);
            activeLevelIndex = levelIndex;
        }
    }

    private int GetPlayableLevelCount()
    {
        int count = 0;
        foreach (LevelPrefab levelPrefab in levelPrefabs)
        {
            if (levelPrefab != null)
            {
                count++;
            }
        }

        return count;
    }

    private void ApplyLevelPrefab(LevelPrefab levelPrefab)
    {
        holderSize = levelPrefab.HolderSize;
        traySize = levelPrefab.TraySize;
        holderCells = LevelDataUtility.CloneHolderCells(levelPrefab.HolderCells);
        fixedBlocks = LevelDataUtility.CloneFixedBlocks(levelPrefab.FixedBlocks);
        waves = LevelDataUtility.CloneWaves(levelPrefab.Waves);
    }

    private void SpawnNextWave()
    {
        currentWaveIndex++;

        if (currentWaveIndex >= waves.Count)
        {
            allWavesSpawned = true;

            if (holderBoard.IsComplete() && GameManager.Instance != null)
            {
                GameManager.Instance.Win();
            }

            return;
        }

        SpawnWave(currentWaveIndex);
    }

    private void SpawnWave(int waveIndex)
    {
        currentWaveBlocks.Clear();

        PieceWaveData wave = waves[waveIndex];
        foreach (LevelPieceData pieceData in wave.pieces)
        {
            GameObject blockObject = new GameObject(string.IsNullOrWhiteSpace(pieceData.id) ? "Block" : $"Block_{pieceData.id}");
            blockObject.transform.SetParent(transform, true);

            Block block = blockObject.AddComponent<Block>();
            block.Initialize(pieceData, holderBoard, trayBoard, this, singleBlockPrefab);

            allBlocks.Add(block);
            currentWaveBlocks.Add(block);
        }
    }

    private void SpawnFixedBlocks()
    {
        foreach (FixedHolderBlockData fixedBlockData in fixedBlocks)
        {
            GameObject blockObject = new GameObject(string.IsNullOrWhiteSpace(fixedBlockData.id) ? "FixedBlock" : $"FixedBlock_{fixedBlockData.id}");
            blockObject.transform.SetParent(transform, true);

            Block block = blockObject.AddComponent<Block>();
            block.InitializeFixed(fixedBlockData, holderBoard, this, singleBlockPrefab);

            if (!holderBoard.CanPlace(block, fixedBlockData.holderAnchor))
            {
                Debug.LogWarning($"{block.name} cannot be placed at {fixedBlockData.holderAnchor}. Check fixed block colors and holder cells.");
                Destroy(blockObject);
                continue;
            }

            holderBoard.PlaceBlock(block, fixedBlockData.holderAnchor);
            allBlocks.Add(block);
        }
    }

    public void RefreshCompletedBlockShapeSprites()
    {
        foreach (Block block in allBlocks)
        {
            if (block == null)
            {
                continue;
            }

            bool shouldHideShapeSprite = block.State == PieceState.PlacedOnHolder && holderBoard.AreBlockColorsComplete(block);
            block.SetShapeSpriteVisible(!shouldHideShapeSprite);
        }
    }

    private bool IsCurrentWavePlaced()
    {
        if (currentWaveBlocks.Count == 0)
        {
            return false;
        }

        foreach (Block block in currentWaveBlocks)
        {
            if (block.State != PieceState.PlacedOnHolder)
            {
                return false;
            }
        }

        return true;
    }

    private void ClearSpawnedBlocks()
    {
        foreach (Block block in allBlocks)
        {
            if (block != null)
            {
                Destroy(block.gameObject);
            }
        }

        allBlocks.Clear();
        currentWaveBlocks.Clear();
    }

    private float GetPrefabCellSize(GameObject prefab)
    {
        SpriteRenderer spriteRenderer = prefab.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
            Vector3 prefabScale = prefab.transform.localScale;
            return Mathf.Max(spriteSize.x * Mathf.Abs(prefabScale.x), spriteSize.y * Mathf.Abs(prefabScale.y));
        }

        BoxCollider2D boxCollider = prefab.GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            Vector2 colliderSize = boxCollider.size;
            Vector3 prefabScale = prefab.transform.localScale;
            return Mathf.Max(colliderSize.x * Mathf.Abs(prefabScale.x), colliderSize.y * Mathf.Abs(prefabScale.y));
        }

        return 1f;
    }
}
