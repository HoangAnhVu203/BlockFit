using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Block : MonoBehaviour
{
    private static Block activeDragBlock;
    private const int DefaultTraySortingOrder = 20;
    private const string ScrewObjectName = "Screws";
    private const string StrokeObjectName = "Stroke";
    private const string VisualObjectName = "Visual";
    private const int ShadowSortingOrderOffset = -11;
    private const float StrokeInsetPixels = 1f;
    private const float VisualJoinOverlapPixels = 1f;
    private const float FixedScrewReleaseDuration = 1f;
    private const float FixedScrewLiftDistance = 0.45f;
    private const float FixedScrewSpinDegrees = 720f;
    private const float TraySpawnJellyDuration = 0.65f;
    private const float TraySpawnRiseDistanceCells = 1.4f;
    private const float CompletedColorBounceDuration = 0.28f;
    private const float CompletedColorBounceHeightCells = 0.18f;

    private readonly List<ColoredCellData> cells = new List<ColoredCellData>();
    private readonly List<SpriteRenderer> screwRenderers = new List<SpriteRenderer>();

    private HolderBoardController holderBoard;
    private TrayBoardController trayBoard;
    private LevelManager levelManager;
    private GameObject singleBlockPrefab;
    private float cellSize = 1f;
    private int traySortingOrder = DefaultTraySortingOrder;
    private Vector3 dragOffset;
    private Vector3 trayWorldPosition;
    private Vector3 previousHolderWorldPosition;
    private Vector2Int previousHolderAnchor;
    private bool isDragging;
    private bool strokeVisible = true;
    private bool hasFixedScrews;
    private bool fixedScrewReleaseStarted;
    private Coroutine traySpawnJellyCoroutine;

    public string Id { get; private set; }
    public PieceState State { get; private set; } = PieceState.InTray;
    public bool IsMovable { get; private set; } = true;
    public Vector2Int HolderAnchor { get; private set; }
    public float CellSize => cellSize;
    public IReadOnlyList<ColoredCellData> Cells => cells;

    private enum StrokeDirection
    {
        Unknown,
        Left,
        Right,
        Up,
        Down
    }

    private void Update()
    {
        if (!CanUseInput())
        {
            CancelActiveDragIfNeeded();
            return;
        }

        if (WasPointerPressedThisFrame())
        {
            TryBeginInputSystemDrag();
        }

        if (activeDragBlock == this && isDragging)
        {
            transform.position = GetPointerWorldPosition() + dragOffset;
            UpdateHolderPreview();

            if (WasPointerReleasedThisFrame())
            {
                EndInputSystemDrag();
            }
        }
    }

    public void Initialize(
        LevelPieceData data,
        HolderBoardController holder,
        TrayBoardController tray,
        LevelManager owner,
        GameObject blockPrefab,
        int initialTraySortingOrder = DefaultTraySortingOrder)
    {
        Id = string.IsNullOrWhiteSpace(data.id) ? gameObject.name : data.id;
        holderBoard = holder;
        trayBoard = tray;
        levelManager = owner;
        singleBlockPrefab = blockPrefab;
        cellSize = GetPrefabCellSize(singleBlockPrefab);
        traySortingOrder = initialTraySortingOrder;
        State = PieceState.InTray;
        IsMovable = true;
        hasFixedScrews = false;
        fixedScrewReleaseStarted = false;

        cells.Clear();
        cells.AddRange(data.cells);

        name = $"Block_{Id}";
        BuildVisuals();
        MoveToTray(data.trayPosition);
    }

    public void InitializeFixed(
        FixedHolderBlockData data,
        HolderBoardController holder,
        LevelManager owner,
        GameObject blockPrefab)
    {
        Id = string.IsNullOrWhiteSpace(data.id) ? gameObject.name : data.id;
        holderBoard = holder;
        trayBoard = null;
        levelManager = owner;
        singleBlockPrefab = blockPrefab;
        cellSize = GetPrefabCellSize(singleBlockPrefab);
        State = PieceState.PlacedOnHolder;
        IsMovable = false;
        hasFixedScrews = true;
        fixedScrewReleaseStarted = false;

        cells.Clear();
        cells.AddRange(data.cells);

        name = $"FixedBlock_{Id}";
        BuildVisuals();
    }

    public void MoveToTray(Vector2Int trayPosition)
    {
        StopTraySpawnJelly(true);
        trayWorldPosition = trayBoard.GridToWorld(trayPosition);
        transform.position = trayWorldPosition;
        State = PieceState.InTray;
        SetBlockShadowVisible(true);
        SetSortingOrder(traySortingOrder);
    }

    public void PlayTraySpawnJelly(float delaySeconds)
    {
        if (!IsMovable || State != PieceState.InTray)
        {
            return;
        }

        StopTraySpawnJelly(false);
        traySpawnJellyCoroutine = StartCoroutine(PlayTraySpawnJellyRoutine(Mathf.Max(0f, delaySeconds), transform.localScale, transform.position));
    }

    public void SnapToHolder(Vector2Int anchor)
    {
        HolderAnchor = anchor;
        transform.position = GetHolderSnapWorldPosition(anchor);
        previousHolderAnchor = anchor;
        previousHolderWorldPosition = transform.position;
        State = PieceState.PlacedOnHolder;
        ApplyHolderPlacementShadowVisibility(anchor);
        SetSortingOrder(GetPlacedHolderSortingOrder());
        ApplyPlacedHolderCellSorting(anchor);
    }

    public Vector3 GetCellColliderWorldPosition(Vector3 rootWorldPosition, Vector2Int cellOffset)
    {
        return rootWorldPosition
            + new Vector3(cellOffset.x * cellSize, cellOffset.y * cellSize, 0f)
            + GetSingleBlockColliderWorldOffset();
    }

    private void BuildVisuals()
    {
        screwRenderers.Clear();
        HashSet<Vector2Int> occupiedOffsets = GetOccupiedOffsets();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        foreach (ColoredCellData cellData in cells)
        {
            GameObject cellObject = Instantiate(singleBlockPrefab, transform);
            cellObject.transform.localPosition = new Vector3(cellData.offset.x * cellSize, cellData.offset.y * cellSize, 0f);
            cellObject.transform.localRotation = singleBlockPrefab.transform.localRotation;
            cellObject.transform.localScale = singleBlockPrefab.transform.localScale;

            Cell cell = cellObject.GetComponent<Cell>();
            if (cell == null)
            {
                cell = cellObject.AddComponent<Cell>();
            }

            cell.Setup(cellData.offset, cellData.color, holderBoard.GetVisualColor(cellData.color), 20);
            ApplyBlockShadowColor(cellObject, holderBoard.GetShadowColor(cellData.color));
            ApplyVisualJoinOverlap(cellObject, cellData, occupiedOffsets);
            ConfigureStrokes(cellObject, cellData, occupiedOffsets, 20);
            ConfigureScrew(cellObject, hasFixedScrews, 20);
        }

        RemoveRootCollider();
    }

    private HashSet<Vector2Int> GetOccupiedOffsets()
    {
        HashSet<Vector2Int> occupiedOffsets = new HashSet<Vector2Int>();
        foreach (ColoredCellData cell in cells)
        {
            occupiedOffsets.Add(cell.offset);
        }

        return occupiedOffsets;
    }

    private void ConfigureStrokes(GameObject cellObject, ColoredCellData cellData, HashSet<Vector2Int> occupiedOffsets, int sortingOrder)
    {
        SpriteRenderer[] strokeRenderers = GetStrokeRenderers(cellObject);
        if (strokeRenderers.Length == 0)
        {
            return;
        }

        Color strokeColor = holderBoard.GetStrokeColor(cellData.color);
        bool hasLeftConnection = occupiedOffsets.Contains(cellData.offset + Vector2Int.left);
        bool hasRightConnection = occupiedOffsets.Contains(cellData.offset + Vector2Int.right);
        bool hasUpConnection = occupiedOffsets.Contains(cellData.offset + Vector2Int.up);
        bool hasDownConnection = occupiedOffsets.Contains(cellData.offset + Vector2Int.down);

        foreach (SpriteRenderer strokeRenderer in strokeRenderers)
        {
            if (strokeRenderer == null)
            {
                continue;
            }

            StrokeDirection direction = ResolveStrokeDirection(strokeRenderer);
            ApplyStrokeInset(strokeRenderer, direction);
            strokeRenderer.color = strokeColor;
            strokeRenderer.enabled = strokeVisible && ShouldShowStroke(direction, hasLeftConnection, hasRightConnection, hasUpConnection, hasDownConnection);
            strokeRenderer.sortingOrder = GetRendererSortingOrder(strokeRenderer, sortingOrder);
        }
    }

    private SpriteRenderer[] GetStrokeRenderers(GameObject cellObject)
    {
        List<SpriteRenderer> strokeRenderers = new List<SpriteRenderer>();
        SpriteRenderer[] renderers = cellObject.GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            if (spriteRenderer != null && IsStrokeRenderer(spriteRenderer))
            {
                strokeRenderers.Add(spriteRenderer);
            }
        }

        return strokeRenderers.ToArray();
    }

    private bool IsStrokeRenderer(SpriteRenderer spriteRenderer)
    {
        return spriteRenderer.name.ToLowerInvariant().Contains(StrokeObjectName.ToLowerInvariant());
    }

    private StrokeDirection ResolveStrokeDirection(SpriteRenderer spriteRenderer)
    {
        string rendererName = spriteRenderer.name.ToLowerInvariant();
        string objectName = spriteRenderer.gameObject.name.ToLowerInvariant();
        string spriteName = spriteRenderer.sprite == null ? string.Empty : spriteRenderer.sprite.name.ToLowerInvariant();
        string lookupName = $"{rendererName} {objectName} {spriteName}";

        if (lookupName.Contains("left") || IsSpriteName(spriteName, "4"))
        {
            return StrokeDirection.Left;
        }

        if (lookupName.Contains("right") || IsSpriteName(spriteName, "3"))
        {
            return StrokeDirection.Right;
        }

        if (lookupName.Contains("top") || lookupName.Contains("up") || IsSpriteName(spriteName, "2"))
        {
            return StrokeDirection.Up;
        }

        if (lookupName.Contains("bottom") || lookupName.Contains("down") || IsSpriteName(spriteName, "5"))
        {
            return StrokeDirection.Down;
        }

        return StrokeDirection.Unknown;
    }

    private bool IsSpriteName(string spriteName, string expectedName)
    {
        return spriteName == expectedName || spriteName.StartsWith($"{expectedName}_");
    }

    private bool ShouldShowStroke(
        StrokeDirection direction,
        bool hasLeftConnection,
        bool hasRightConnection,
        bool hasUpConnection,
        bool hasDownConnection)
    {
        switch (direction)
        {
            case StrokeDirection.Left:
                return !hasLeftConnection;
            case StrokeDirection.Right:
                return !hasRightConnection;
            case StrokeDirection.Up:
                return !hasUpConnection;
            case StrokeDirection.Down:
                return !hasDownConnection;
            default:
                return true;
        }
    }

    private void ApplyVisualJoinOverlap(GameObject cellObject, ColoredCellData cellData, HashSet<Vector2Int> occupiedOffsets)
    {
        Transform visualTransform = cellObject.transform.Find(VisualObjectName);
        SpriteRenderer visualRenderer = visualTransform == null ? null : visualTransform.GetComponent<SpriteRenderer>();
        if (visualRenderer == null || visualRenderer.sprite == null)
        {
            return;
        }

        Vector3 baseLocalPosition = GetPrefabChildLocalPosition(VisualObjectName, visualTransform.localPosition);
        Vector3 baseLocalScale = GetPrefabChildLocalScale(VisualObjectName, visualTransform.localScale);
        float overlapUnits = GetSpritePixelUnits(visualRenderer, VisualJoinOverlapPixels);

        float leftOverlap = occupiedOffsets.Contains(cellData.offset + Vector2Int.left) ? overlapUnits : 0f;
        float rightOverlap = occupiedOffsets.Contains(cellData.offset + Vector2Int.right) ? overlapUnits : 0f;
        float upOverlap = occupiedOffsets.Contains(cellData.offset + Vector2Int.up) ? overlapUnits : 0f;
        float downOverlap = occupiedOffsets.Contains(cellData.offset + Vector2Int.down) ? overlapUnits : 0f;

        float baseWidth = visualRenderer.sprite.bounds.size.x * Mathf.Abs(baseLocalScale.x);
        float baseHeight = visualRenderer.sprite.bounds.size.y * Mathf.Abs(baseLocalScale.y);
        float scaleXMultiplier = Mathf.Approximately(baseWidth, 0f) ? 1f : (baseWidth + leftOverlap + rightOverlap) / baseWidth;
        float scaleYMultiplier = Mathf.Approximately(baseHeight, 0f) ? 1f : (baseHeight + upOverlap + downOverlap) / baseHeight;

        visualTransform.localPosition = baseLocalPosition + new Vector3(
            (rightOverlap - leftOverlap) * 0.5f,
            (upOverlap - downOverlap) * 0.5f,
            0f
        );
        visualTransform.localScale = new Vector3(
            baseLocalScale.x * scaleXMultiplier,
            baseLocalScale.y * scaleYMultiplier,
            baseLocalScale.z
        );
    }

    private void ApplyStrokeInset(SpriteRenderer strokeRenderer, StrokeDirection direction)
    {
        if (strokeRenderer == null)
        {
            return;
        }

        Vector3 baseLocalPosition = GetPrefabStrokeLocalPosition(direction, strokeRenderer.transform.localPosition);
        float insetUnits = GetSpritePixelUnits(strokeRenderer, StrokeInsetPixels);

        switch (direction)
        {
            case StrokeDirection.Left:
                strokeRenderer.transform.localPosition = baseLocalPosition + Vector3.right * insetUnits;
                break;
            case StrokeDirection.Right:
                strokeRenderer.transform.localPosition = baseLocalPosition + Vector3.left * insetUnits;
                break;
            case StrokeDirection.Up:
                strokeRenderer.transform.localPosition = baseLocalPosition + Vector3.down * insetUnits;
                break;
            case StrokeDirection.Down:
                strokeRenderer.transform.localPosition = baseLocalPosition + Vector3.up * insetUnits;
                break;
            default:
                strokeRenderer.transform.localPosition = baseLocalPosition;
                break;
        }
    }

    private Vector3 GetPrefabStrokeLocalPosition(StrokeDirection direction, Vector3 fallbackLocalPosition)
    {
        if (singleBlockPrefab == null)
        {
            return fallbackLocalPosition;
        }

        foreach (SpriteRenderer prefabStrokeRenderer in GetStrokeRenderers(singleBlockPrefab))
        {
            if (prefabStrokeRenderer != null && ResolveStrokeDirection(prefabStrokeRenderer) == direction)
            {
                return prefabStrokeRenderer.transform.localPosition;
            }
        }

        return fallbackLocalPosition;
    }

    private Vector3 GetPrefabChildLocalPosition(string childName, Vector3 fallbackLocalPosition)
    {
        if (singleBlockPrefab == null)
        {
            return fallbackLocalPosition;
        }

        Transform childTransform = singleBlockPrefab.transform.Find(childName);
        return childTransform == null ? fallbackLocalPosition : childTransform.localPosition;
    }

    private Vector3 GetPrefabChildLocalScale(string childName, Vector3 fallbackLocalScale)
    {
        if (singleBlockPrefab == null)
        {
            return fallbackLocalScale;
        }

        Transform childTransform = singleBlockPrefab.transform.Find(childName);
        return childTransform == null ? fallbackLocalScale : childTransform.localScale;
    }

    private float GetSpritePixelUnits(SpriteRenderer spriteRenderer, float pixels)
    {
        if (spriteRenderer.sprite == null || Mathf.Approximately(spriteRenderer.sprite.pixelsPerUnit, 0f))
        {
            return 0f;
        }

        return Mathf.Max(0f, pixels) / spriteRenderer.sprite.pixelsPerUnit;
    }

    private Vector3 GetHolderSnapWorldPosition(Vector2Int anchor)
    {
        if (holderBoard == null)
        {
            return transform.position;
        }

        return holderBoard.GridToWorld(anchor) + holderBoard.CellColliderWorldOffset - GetSingleBlockColliderWorldOffset();
    }

    private Vector3 GetSingleBlockColliderWorldOffset()
    {
        if (singleBlockPrefab == null)
        {
            return Vector3.zero;
        }

        BoxCollider2D boxCollider = singleBlockPrefab.GetComponent<BoxCollider2D>();
        if (boxCollider == null)
        {
            return Vector3.zero;
        }

        Vector3 prefabScale = singleBlockPrefab.transform.localScale;
        return new Vector3(
            boxCollider.offset.x * prefabScale.x,
            boxCollider.offset.y * prefabScale.y,
            0f
        );
    }

    private void ConfigureScrew(GameObject cellObject, bool visible, int sortingOrder)
    {
        Transform screwTransform = cellObject.transform.Find(ScrewObjectName);
        if (screwTransform == null)
        {
            return;
        }

        SpriteRenderer[] renderers = screwTransform.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            spriteRenderer.enabled = visible;
            spriteRenderer.sortingOrder = GetRendererSortingOrder(spriteRenderer, sortingOrder);

            Color color = spriteRenderer.color;
            color.a = 1f;
            spriteRenderer.color = color;

            if (visible)
            {
                screwRenderers.Add(spriteRenderer);
            }
        }

        screwTransform.gameObject.SetActive(visible);
    }

    private void ApplyBlockShadowColor(GameObject cellObject, Color color)
    {
        Transform shadowTransform = cellObject.transform.Find("Shadow");
        SpriteRenderer shadowRenderer = shadowTransform == null ? null : shadowTransform.GetComponent<SpriteRenderer>();

        if (shadowRenderer != null)
        {
            shadowRenderer.color = color;
        }
    }

    private void SetBlockShadowVisible(bool visible)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            if (spriteRenderer.name == "Shadow")
            {
                spriteRenderer.enabled = visible;
            }
        }
    }

    private void ApplyHolderPlacementShadowVisibility(Vector2Int anchor)
    {
        Cell[] childCells = GetComponentsInChildren<Cell>(true);
        foreach (Cell cell in childCells)
        {
            Vector2Int holderPosition = anchor + cell.GridPosition;
            bool shouldHideShadow = holderBoard.DoesCellBottomTouchShadowBlockingWall(holderPosition);
            SetCellShadowVisible(cell.gameObject, !shouldHideShadow);
        }
    }

    private void SetCellShadowVisible(GameObject cellObject, bool visible)
    {
        Transform shadowTransform = cellObject.transform.Find("Shadow");
        SpriteRenderer shadowRenderer = shadowTransform == null ? null : shadowTransform.GetComponent<SpriteRenderer>();

        if (shadowRenderer != null)
        {
            shadowRenderer.enabled = visible;
        }
    }

    public void SetStrokeVisible(bool visible)
    {
        strokeVisible = visible;

        HashSet<Vector2Int> occupiedOffsets = GetOccupiedOffsets();
        Cell[] childCells = GetComponentsInChildren<Cell>(true);
        foreach (Cell cell in childCells)
        {
            if (cell == null)
            {
                continue;
            }

            ColoredCellData cellData = new ColoredCellData
            {
                offset = cell.GridPosition,
                color = cell.ColorId
            };
            ConfigureStrokes(cell.gameObject, cellData, occupiedOffsets, GetBaseSortingOrderForState());
        }

        if (State == PieceState.PlacedOnHolder)
        {
            ApplyPlacedHolderCellSorting(HolderAnchor);
        }
    }

    public void PlayFixedScrewReleaseIfNeeded()
    {
        if (!hasFixedScrews || fixedScrewReleaseStarted)
        {
            return;
        }

        fixedScrewReleaseStarted = true;
        StartCoroutine(ReleaseFixedScrews());
    }

    public void PlayCompletedColorBounce(BlockColorId colorId)
    {
        if (State != PieceState.PlacedOnHolder)
        {
            return;
        }

        foreach (Cell cell in GetComponentsInChildren<Cell>(true))
        {
            if (cell.ColorId == colorId)
            {
                PlayCellBounce(cell, 0f, CompletedColorBounceDuration, cellSize * CompletedColorBounceHeightCells);
            }
        }
    }

    public void CollectPlacedCells(List<Cell> placedCells)
    {
        if (State != PieceState.PlacedOnHolder || placedCells == null)
        {
            return;
        }

        foreach (Cell cell in GetComponentsInChildren<Cell>(true))
        {
            placedCells.Add(cell);
        }
    }

    public void DetachCompletedColorCells(BlockColorId colorId, Transform detachedParent, List<Cell> detachedCells)
    {
        if (State != PieceState.PlacedOnHolder)
        {
            return;
        }

        bool detachedAnyCell = false;
        Cell[] childCells = GetComponentsInChildren<Cell>(true);
        foreach (Cell cell in childCells)
        {
            if (cell == null || cell.ColorId != colorId)
            {
                continue;
            }

            SetCellStrokesVisible(cell.gameObject, false, GetBaseSortingOrderForState());
            cell.transform.SetParent(detachedParent, true);
            detachedAnyCell = true;

            if (detachedCells != null && !detachedCells.Contains(cell))
            {
                detachedCells.Add(cell);
            }
        }

        if (detachedAnyCell)
        {
            RefreshStrokeVisibility();
        }
    }

    private void SetCellStrokesVisible(GameObject cellObject, bool visible, int sortingOrder)
    {
        foreach (SpriteRenderer strokeRenderer in GetStrokeRenderers(cellObject))
        {
            if (strokeRenderer == null)
            {
                continue;
            }

            strokeRenderer.enabled = visible;
            strokeRenderer.sortingOrder = GetRendererSortingOrder(strokeRenderer, sortingOrder);
        }
    }

    private void RefreshStrokeVisibility()
    {
        SetStrokeVisible(strokeVisible);
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

    private IEnumerator PlayTraySpawnJellyRoutine(float delaySeconds, Vector3 targetScale, Vector3 targetPosition)
    {
        Vector3 localVisualCenter = GetLocalVisualCenter();
        Vector3 targetCenter = GetWorldVisualCenter(targetPosition, targetScale, localVisualCenter);
        Vector3 startCenter = targetCenter + Vector3.down * cellSize * TraySpawnRiseDistanceCells;

        transform.localScale = Vector3.zero;
        transform.position = GetRootPositionForVisualCenter(startCenter, transform.localScale, localVisualCenter);

        if (delaySeconds > 0f)
        {
            yield return new WaitForSeconds(delaySeconds);
        }

        float elapsed = 0f;
        while (elapsed < TraySpawnJellyDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / TraySpawnJellyDuration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            Vector3 currentScale = Vector3.Scale(targetScale, GetJellySpawnScale(easedProgress));
            Vector3 currentCenter = Vector3.LerpUnclamped(startCenter, targetCenter, GetJellyRiseProgress(progress));

            transform.localScale = currentScale;
            transform.position = GetRootPositionForVisualCenter(currentCenter, currentScale, localVisualCenter);
            yield return null;
        }

        transform.position = targetPosition;
        transform.localScale = targetScale;
        traySpawnJellyCoroutine = null;
    }

    private Vector3 GetLocalVisualCenter()
    {
        if (cells.Count == 0)
        {
            return Vector3.zero;
        }

        Vector2Int min = cells[0].offset;
        Vector2Int max = cells[0].offset;

        foreach (ColoredCellData cell in cells)
        {
            min.x = Mathf.Min(min.x, cell.offset.x);
            min.y = Mathf.Min(min.y, cell.offset.y);
            max.x = Mathf.Max(max.x, cell.offset.x);
            max.y = Mathf.Max(max.y, cell.offset.y);
        }

        return new Vector3((min.x + max.x) * 0.5f * cellSize, (min.y + max.y) * 0.5f * cellSize, 0f);
    }

    private Vector3 GetWorldVisualCenter(Vector3 rootPosition, Vector3 rootScale, Vector3 localVisualCenter)
    {
        return rootPosition + Vector3.Scale(localVisualCenter, rootScale);
    }

    private Vector3 GetRootPositionForVisualCenter(Vector3 visualCenter, Vector3 rootScale, Vector3 localVisualCenter)
    {
        return visualCenter - Vector3.Scale(localVisualCenter, rootScale);
    }

    private float GetJellyRiseProgress(float progress)
    {
        if (progress < 0.7f)
        {
            return Mathf.SmoothStep(0f, 1.03f, progress / 0.7f);
        }

        float settleProgress = (progress - 0.7f) / 0.3f;
        return Mathf.Lerp(1.03f, 1f, Mathf.SmoothStep(0f, 1f, settleProgress));
    }

    private Vector3 GetJellySpawnScale(float progress)
    {
        if (progress < 0.32f)
        {
            float segmentProgress = progress / 0.32f;
            float scale = Mathf.SmoothStep(0f, 1.08f, segmentProgress);
            return new Vector3(scale * 1.04f, scale * 0.96f, 1f);
        }

        if (progress < 0.56f)
        {
            float segmentProgress = (progress - 0.32f) / 0.24f;
            return Vector3.Lerp(new Vector3(1.04f, 0.96f, 1f), new Vector3(0.97f, 1.04f, 1f), Mathf.SmoothStep(0f, 1f, segmentProgress));
        }

        if (progress < 0.78f)
        {
            float segmentProgress = (progress - 0.56f) / 0.22f;
            return Vector3.Lerp(new Vector3(0.97f, 1.04f, 1f), new Vector3(1.02f, 0.99f, 1f), Mathf.SmoothStep(0f, 1f, segmentProgress));
        }

        float finalProgress = (progress - 0.78f) / 0.22f;
        return Vector3.Lerp(new Vector3(1.02f, 0.99f, 1f), Vector3.one, Mathf.SmoothStep(0f, 1f, finalProgress));
    }

    private void StopTraySpawnJelly(bool resetScale)
    {
        if (traySpawnJellyCoroutine != null)
        {
            StopCoroutine(traySpawnJellyCoroutine);
            traySpawnJellyCoroutine = null;
        }

        if (resetScale)
        {
            transform.localScale = Vector3.one;
            transform.position = trayWorldPosition;
        }
    }

    private IEnumerator ReleaseFixedScrews()
    {
        List<SpriteRenderer> activeScrews = new List<SpriteRenderer>();
        foreach (SpriteRenderer screwRenderer in screwRenderers)
        {
            if (screwRenderer != null)
            {
                activeScrews.Add(screwRenderer);
            }
        }

        if (activeScrews.Count == 0)
        {
            yield break;
        }

        Vector3[] startPositions = new Vector3[activeScrews.Count];
        Quaternion[] startRotations = new Quaternion[activeScrews.Count];
        Color[] startColors = new Color[activeScrews.Count];

        for (int i = 0; i < activeScrews.Count; i++)
        {
            Transform screwTransform = activeScrews[i].transform;
            startPositions[i] = screwTransform.localPosition;
            startRotations[i] = screwTransform.localRotation;
            startColors[i] = activeScrews[i].color;
        }

        float elapsed = 0f;
        while (elapsed < FixedScrewReleaseDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / FixedScrewReleaseDuration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

            for (int i = 0; i < activeScrews.Count; i++)
            {
                SpriteRenderer screwRenderer = activeScrews[i];
                if (screwRenderer == null)
                {
                    continue;
                }

                Transform screwTransform = screwRenderer.transform;
                screwTransform.localPosition = startPositions[i] + Vector3.up * FixedScrewLiftDistance * easedProgress;
                screwTransform.localRotation = startRotations[i] * Quaternion.Euler(0f, 0f, FixedScrewSpinDegrees * easedProgress);

                Color color = startColors[i];
                color.a = Mathf.Lerp(startColors[i].a, 0f, progress);
                screwRenderer.color = color;
            }

            yield return null;
        }

        foreach (SpriteRenderer screwRenderer in activeScrews)
        {
            if (screwRenderer == null)
            {
                continue;
            }

            screwRenderer.enabled = false;
            screwRenderer.gameObject.SetActive(false);
        }
    }

    private void RemoveRootCollider()
    {
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            Destroy(boxCollider);
        }
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

    private void TryBeginInputSystemDrag()
    {
        if (activeDragBlock != null || !CanDragCurrentState() || GetTopBlockUnderPointer() != this)
        {
            return;
        }

        isDragging = true;
        activeDragBlock = this;
        StopTraySpawnJelly(true);

        if (State == PieceState.InTray)
        {
            State = PieceState.DraggingFromTray;
        }
        else
        {
            State = PieceState.DraggingFromHolder;
            previousHolderAnchor = HolderAnchor;
            previousHolderWorldPosition = transform.position;
        }

        dragOffset = transform.position - GetPointerWorldPosition();
        SetBlockShadowVisible(true);
        SetStrokeVisible(true);
        SetSortingOrder(100);
        UpdateHolderPreview();
    }

    private void EndInputSystemDrag()
    {
        if (!isDragging || activeDragBlock != this)
        {
            return;
        }

        isDragging = false;
        activeDragBlock = null;
        holderBoard.ClearPreview();

        if (holderBoard.TryFindBestPlacement(this, transform.position, out Vector2Int anchor))
        {
            holderBoard.PlaceBlock(this, anchor);
            levelManager.HandleBlockPlaced(this);
            return;
        }

        ReturnToLastValidPosition();
    }

    private bool CanUseInput()
    {
        return GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Gameplay;
    }

    private void CancelActiveDragIfNeeded()
    {
        if (activeDragBlock != this || !isDragging)
        {
            return;
        }

        isDragging = false;
        activeDragBlock = null;
        holderBoard.ClearPreview();
        ReturnToLastValidPosition();
    }

    private bool CanDragCurrentState()
    {
        return IsMovable && (State == PieceState.InTray || State == PieceState.PlacedOnHolder);
    }

    private void ReturnToLastValidPosition()
    {
        if (State == PieceState.DraggingFromHolder)
        {
            HolderAnchor = previousHolderAnchor;
            transform.position = previousHolderWorldPosition;
            State = PieceState.PlacedOnHolder;
            ApplyHolderPlacementShadowVisibility(previousHolderAnchor);
            SetSortingOrder(GetPlacedHolderSortingOrder());
            ApplyPlacedHolderCellSorting(previousHolderAnchor);
            levelManager.RefreshCompletedBlockStrokes();
            return;
        }

        transform.position = trayWorldPosition;
        State = PieceState.InTray;
        SetBlockShadowVisible(true);
        SetSortingOrder(traySortingOrder);
    }

    private void UpdateHolderPreview()
    {
        holderBoard.PreviewPlacement(this, transform.position);
    }

    private Vector3 GetPointerWorldPosition()
    {
        Camera camera = Camera.main;
        Vector2 screenPoint = GetPointerScreenPosition();
        Vector3 screenPosition = new Vector3(screenPoint.x, screenPoint.y, 0f);
        screenPosition.z = camera == null ? 0f : -camera.transform.position.z;
        Vector3 worldPosition = camera == null ? screenPosition : camera.ScreenToWorldPoint(screenPosition);
        worldPosition.z = transform.position.z;
        return worldPosition;
    }

    private Vector2 GetPointerScreenPosition()
    {
        if (Pointer.current != null)
        {
            return Pointer.current.position.ReadValue();
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            return Touchscreen.current.primaryTouch.position.ReadValue();
        }

        return Vector2.zero;
    }

    private bool WasPointerPressedThisFrame()
    {
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            return true;
        }

        return Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
    }

    private bool WasPointerReleasedThisFrame()
    {
        if (Pointer.current != null && Pointer.current.press.wasReleasedThisFrame)
        {
            return true;
        }

        return Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
    }

    private Block GetTopBlockUnderPointer()
    {
        Vector3 pointerWorldPosition = GetPointerWorldPosition();
        Collider2D[] hits = Physics2D.OverlapPointAll(pointerWorldPosition);
        Block topBlock = null;
        int topSortingOrder = int.MinValue;

        foreach (Collider2D hit in hits)
        {
            Block block = hit.GetComponentInParent<Block>();
            if (block == null || !block.CanDragCurrentState())
            {
                continue;
            }

            int sortingOrder = block.GetHighestSortingOrder();
            if (topBlock == null || sortingOrder > topSortingOrder)
            {
                topBlock = block;
                topSortingOrder = sortingOrder;
            }
        }

        return topBlock;
    }

    private int GetHighestSortingOrder()
    {
        int highestSortingOrder = int.MinValue;
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();

        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            highestSortingOrder = Mathf.Max(highestSortingOrder, spriteRenderer.sortingOrder);
        }

        return highestSortingOrder == int.MinValue ? 0 : highestSortingOrder;
    }

    private void SetSortingOrder(int sortingOrder)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            spriteRenderer.sortingOrder = GetRendererSortingOrder(spriteRenderer, sortingOrder);
        }
    }

    private void ApplyPlacedHolderCellSorting(Vector2Int anchor)
    {
        if (holderBoard == null)
        {
            return;
        }

        Cell[] childCells = GetComponentsInChildren<Cell>(true);
        foreach (Cell cell in childCells)
        {
            if (cell == null)
            {
                continue;
            }

            Vector2Int holderPosition = anchor + cell.GridPosition;
            SpriteRenderer[] renderers = cell.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer spriteRenderer in renderers)
            {
                if (spriteRenderer == null)
                {
                    continue;
                }

                spriteRenderer.sortingOrder = GetPlacedHolderRendererSortingOrder(spriteRenderer, holderPosition);
            }
        }
    }

    private int GetPlacedHolderRendererSortingOrder(SpriteRenderer spriteRenderer, Vector2Int holderPosition)
    {
        if (spriteRenderer.name == "Shadow")
        {
            return holderBoard.GetPlacedCellShadowSortingOrder(holderPosition);
        }

        if (spriteRenderer.name == VisualObjectName)
        {
            return holderBoard.GetPlacedCellVisualSortingOrder(holderPosition);
        }

        if (IsStrokeRenderer(spriteRenderer))
        {
            return holderBoard.GetPlacedCellStrokeSortingOrder(holderPosition);
        }

        if (spriteRenderer.name == ScrewObjectName)
        {
            return holderBoard.GetPlacedCellScrewSortingOrder(holderPosition);
        }

        return holderBoard.GetPlacedCellVisualSortingOrder(holderPosition);
    }

    private int GetBaseSortingOrderForState()
    {
        switch (State)
        {
            case PieceState.InTray:
            case PieceState.DraggingFromTray:
                return traySortingOrder;
            case PieceState.DraggingFromHolder:
                return 100;
            case PieceState.PlacedOnHolder:
                return GetPlacedHolderSortingOrder();
            default:
                return 20;
        }
    }

    private int GetPlacedHolderSortingOrder()
    {
        return holderBoard == null ? 35 : holderBoard.PlacedBlockSortingOrder;
    }

    private int GetRendererSortingOrder(SpriteRenderer spriteRenderer, int baseSortingOrder)
    {
        if (spriteRenderer.name == "Shadow")
        {
            if (State == PieceState.PlacedOnHolder && holderBoard != null)
            {
                return holderBoard.PlacedBlockShadowSortingOrder;
            }

            return baseSortingOrder + ShadowSortingOrderOffset;
        }

        if (IsStrokeRenderer(spriteRenderer))
        {
            return baseSortingOrder + 3;
        }

        if (spriteRenderer.name == "Visual")
        {
            return baseSortingOrder + 2;
        }

        if (spriteRenderer.name == ScrewObjectName)
        {
            return baseSortingOrder + 4;
        }

        return baseSortingOrder;
    }

}
