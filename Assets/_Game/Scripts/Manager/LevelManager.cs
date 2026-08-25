using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    private const string SavedLevelIndexKey = "BlockFit.CurrentLevelIndex";
    private const int TrayBlockSortingOrderBase = 20;
    private const int TrayBlockSortingOrderStep = 4;
    private const float WaveBlockSpawnJellyDelay = 0.06f;
    private const float LevelCompleteRippleStartDelay = 0.2f;
    private const float LevelCompleteRippleEndDelay = 0.12f;
    private const float LevelCompleteCellRippleDuration = 0.34f;
    private const float LevelCompleteCellRippleHeightCells = 0.24f;
    private const float LevelCompleteCellRippleDelaySpan = 0.5f;

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
    private readonly List<Cell> detachedCompletedCells = new List<Cell>();
    private readonly List<Cell> newlyDetachedCells = new List<Cell>();
    private readonly List<Cell> levelCompleteRippleCells = new List<Cell>();
    private readonly HashSet<BlockColorId> completedColorIds = new HashSet<BlockColorId>();
    private int currentWaveIndex;
    private int activeLevelIndex = -1;
    private bool allWavesSpawned;
    private bool levelCompleteSequenceStarted;

    public HolderBoardController HolderBoard => holderBoard;

    private void OnValidate()
    {
        holderSize = HolderBoardController.ClampHolderSize(holderSize);
    }

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
        RefreshCompletedBlockStrokes();
        completedColorIds.Clear();
        RefreshCompletedColors(false);
        levelCompleteSequenceStarted = false;

        currentWaveIndex = 0;
        allWavesSpawned = waves.Count == 0;

        if (waves.Count > 0)
        {
            SpawnWave(currentWaveIndex);
        }
        else if (holderBoard.IsComplete())
        {
            PlayLevelCompleteSequenceIfNeeded();
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
        SaveCurrentLevelIndex();
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

        RefreshCompletedBlockStrokes();
        RefreshCompletedColors(true);

        if (holderBoard.IsComplete() && allWavesSpawned)
        {
            PlayLevelCompleteSequenceIfNeeded();
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

        int levelIndex = activeLevelIndex >= 0 ? activeLevelIndex : GetSavedLevelIndex();
        levelIndex = Mathf.Clamp(levelIndex, 0, levelPrefabs.Count - 1);

        if (levelPrefabs[levelIndex] == null)
        {
            levelIndex = FindNextPlayableLevelIndex(levelIndex);
        }

        if (levelIndex < 0 || levelPrefabs[levelIndex] == null)
        {
            return;
        }

        ApplyLevelPrefab(levelPrefabs[levelIndex]);
        activeLevelIndex = levelIndex;
        SaveCurrentLevelIndex();
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

    private int GetSavedLevelIndex()
    {
        int defaultLevelIndex = Mathf.Clamp(startingLevelIndex, 0, Mathf.Max(0, levelPrefabs.Count - 1));
        return PlayerPrefs.GetInt(SavedLevelIndexKey, defaultLevelIndex);
    }

    private void SaveCurrentLevelIndex()
    {
        if (activeLevelIndex < 0)
        {
            return;
        }

        PlayerPrefs.SetInt(SavedLevelIndexKey, activeLevelIndex);
        PlayerPrefs.Save();
    }

    private int FindNextPlayableLevelIndex(int fromLevelIndex)
    {
        if (levelPrefabs.Count == 0)
        {
            return -1;
        }

        int startIndex = Mathf.Clamp(fromLevelIndex, 0, levelPrefabs.Count - 1);
        for (int offset = 0; offset < levelPrefabs.Count; offset++)
        {
            int levelIndex = (startIndex + offset) % levelPrefabs.Count;
            if (levelPrefabs[levelIndex] != null)
            {
                return levelIndex;
            }
        }

        return -1;
    }

    private void ApplyLevelPrefab(LevelPrefab levelPrefab)
    {
        holderSize = HolderBoardController.ClampHolderSize(levelPrefab.HolderSize);
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

            if (holderBoard.IsComplete())
            {
                PlayLevelCompleteSequenceIfNeeded();
            }

            return;
        }

        SpawnWave(currentWaveIndex);
    }

    private void SpawnWave(int waveIndex)
    {
        currentWaveBlocks.Clear();

        PieceWaveData wave = waves[waveIndex];
        for (int pieceIndex = 0; pieceIndex < wave.pieces.Count; pieceIndex++)
        {
            LevelPieceData pieceData = wave.pieces[pieceIndex];
            GameObject blockObject = new GameObject(string.IsNullOrWhiteSpace(pieceData.id) ? "Block" : $"Block_{pieceData.id}");
            blockObject.transform.SetParent(transform, true);

            Block block = blockObject.AddComponent<Block>();
            int traySortingOrder = TrayBlockSortingOrderBase + pieceIndex * TrayBlockSortingOrderStep;
            block.Initialize(pieceData, holderBoard, trayBoard, this, singleBlockPrefab, traySortingOrder);
            block.PlayTraySpawnJelly(pieceIndex * WaveBlockSpawnJellyDelay);

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

    public void RefreshCompletedBlockStrokes()
    {
        foreach (Block block in allBlocks)
        {
            if (block == null)
            {
                continue;
            }

            bool shouldHideStroke = block.State == PieceState.PlacedOnHolder && holderBoard.AreBlockColorsComplete(block);
            block.SetStrokeVisible(!shouldHideStroke);

            if (shouldHideStroke)
            {
                block.PlayFixedScrewReleaseIfNeeded();
            }
        }
    }

    private void RefreshCompletedColors(bool playBounce)
    {
        foreach (BlockColorId colorId in System.Enum.GetValues(typeof(BlockColorId)))
        {
            if (colorId == BlockColorId.None || completedColorIds.Contains(colorId) || !holderBoard.IsColorComplete(colorId))
            {
                continue;
            }

            completedColorIds.Add(colorId);

            if (playBounce)
            {
                DetachCompletedColorCells(colorId);
                PlayCompletedColorBounce();
            }
        }
    }

    private void DetachCompletedColorCells(BlockColorId colorId)
    {
        newlyDetachedCells.Clear();

        foreach (Block block in allBlocks)
        {
            if (block != null)
            {
                block.DetachCompletedColorCells(colorId, transform, newlyDetachedCells);
            }
        }

        foreach (Cell cell in newlyDetachedCells)
        {
            if (cell != null && !detachedCompletedCells.Contains(cell))
            {
                detachedCompletedCells.Add(cell);
            }
        }
    }

    private void PlayCompletedColorBounce()
    {
        foreach (Cell cell in newlyDetachedCells)
        {
            if (cell != null)
            {
                PlayCellBounce(cell, 0f, 0.28f, holderBoard.CellSize * 0.18f);
            }
        }
    }

    private void PlayLevelCompleteSequenceIfNeeded()
    {
        if (levelCompleteSequenceStarted)
        {
            return;
        }

        levelCompleteSequenceStarted = true;
        StartCoroutine(PlayLevelCompleteSequence());
    }

    private IEnumerator PlayLevelCompleteSequence()
    {
        yield return new WaitForSeconds(LevelCompleteRippleStartDelay);

        levelCompleteRippleCells.Clear();
        AddDetachedCompletedCellsToRipple();

        foreach (Block block in allBlocks)
        {
            if (block != null)
            {
                block.CollectPlacedCells(levelCompleteRippleCells);
            }
        }

        float waitDuration = PlayLevelCompleteCellRipple();
        if (waitDuration > 0f)
        {
            yield return new WaitForSeconds(waitDuration + LevelCompleteRippleEndDelay);
        }

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Gameplay)
        {
            GameManager.Instance.Win();
        }
    }

    private float PlayLevelCompleteCellRipple()
    {
        if (levelCompleteRippleCells.Count == 0)
        {
            return 0f;
        }

        float minWorldX = levelCompleteRippleCells[0].transform.position.x;
        float maxWorldX = minWorldX;

        foreach (Cell cell in levelCompleteRippleCells)
        {
            if (cell == null)
            {
                continue;
            }

            float worldX = cell.transform.position.x;
            minWorldX = Mathf.Min(minWorldX, worldX);
            maxWorldX = Mathf.Max(maxWorldX, worldX);
        }

        float longestDelay = 0f;
        foreach (Cell cell in levelCompleteRippleCells)
        {
            if (cell == null)
            {
                continue;
            }

            float normalizedX = Mathf.Approximately(minWorldX, maxWorldX) ? 0f : Mathf.InverseLerp(minWorldX, maxWorldX, cell.transform.position.x);
            float delay = normalizedX * LevelCompleteCellRippleDelaySpan;
            longestDelay = Mathf.Max(longestDelay, delay);
            PlayCellBounce(cell, delay, LevelCompleteCellRippleDuration, holderBoard.CellSize * LevelCompleteCellRippleHeightCells);
        }

        return longestDelay + LevelCompleteCellRippleDuration;
    }

    private void AddDetachedCompletedCellsToRipple()
    {
        for (int i = detachedCompletedCells.Count - 1; i >= 0; i--)
        {
            Cell cell = detachedCompletedCells[i];
            if (cell == null)
            {
                detachedCompletedCells.RemoveAt(i);
                continue;
            }

            levelCompleteRippleCells.Add(cell);
        }
    }

    private void PlayCellBounce(Cell cell, float delaySeconds, float duration, float height)
    {
        if (cell == null)
        {
            return;
        }

        CellBounceAnimator bounceAnimator = cell.GetComponent<CellBounceAnimator>();
        if (bounceAnimator == null)
        {
            bounceAnimator = cell.gameObject.AddComponent<CellBounceAnimator>();
        }

        bounceAnimator.Play(delaySeconds, duration, height, holderBoard.RaisedCellSortingOrder);
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

        foreach (Cell cell in detachedCompletedCells)
        {
            if (cell != null)
            {
                Destroy(cell.gameObject);
            }
        }

        detachedCompletedCells.Clear();
        newlyDetachedCells.Clear();
        levelCompleteRippleCells.Clear();
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
