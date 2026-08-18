using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Block : MonoBehaviour
{
    private static Block activeDragBlock;
    private const int ShapeSpritePixelsPerCell = 32;
    private const int ShapeSpritePaddingPixels = 1;
    private const float ShapeSpriteDarkenAmount = 0.18f;
    private const float ShadowDarkenAmount = 0.28f;
    private const string ScrewObjectName = "Screws";
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
    private Vector3 dragOffset;
    private Vector3 trayWorldPosition;
    private Vector3 previousHolderWorldPosition;
    private Vector2Int previousHolderAnchor;
    private bool isDragging;
    private bool shapeSpriteVisible = true;
    private bool hasFixedScrews;
    private bool fixedScrewReleaseStarted;
    private Coroutine traySpawnJellyCoroutine;
    private Texture2D shapeTexture;
    private Sprite shapeSprite;
    private SpriteRenderer shapeSpriteRenderer;

    public string Id { get; private set; }
    public PieceState State { get; private set; } = PieceState.InTray;
    public bool IsMovable { get; private set; } = true;
    public Vector2Int HolderAnchor { get; private set; }
    public float CellSize => cellSize;
    public IReadOnlyList<ColoredCellData> Cells => cells;

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
        GameObject blockPrefab)
    {
        Id = string.IsNullOrWhiteSpace(data.id) ? gameObject.name : data.id;
        holderBoard = holder;
        trayBoard = tray;
        levelManager = owner;
        singleBlockPrefab = blockPrefab;
        cellSize = GetPrefabCellSize(singleBlockPrefab);
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
        SetSortingOrder(20);
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
        transform.position = holderBoard.GridToWorld(anchor);
        previousHolderAnchor = anchor;
        previousHolderWorldPosition = transform.position;
        State = PieceState.PlacedOnHolder;
        ApplyHolderPlacementShadowVisibility(anchor);
        SetSortingOrder(35);
    }

    private void BuildVisuals()
    {
        screwRenderers.Clear();

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

            cell.Setup(cellData.offset, cellData.color, holderBoard.GetColor(cellData.color), 20);
            ApplyBlockShadowColor(cellObject, holderBoard.GetColor(cellData.color));
            ConfigureScrew(cellObject, hasFixedScrews, 20);
        }

        BuildShapeSprite();
        RemoveRootCollider();
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

    private void BuildShapeSprite()
    {
        DestroyGeneratedShapeSprite();

        if (cells.Count == 0)
        {
            return;
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

        int widthCells = max.x - min.x + 1;
        int heightCells = max.y - min.y + 1;
        int textureWidth = widthCells * ShapeSpritePixelsPerCell + ShapeSpritePaddingPixels * 2;
        int textureHeight = heightCells * ShapeSpritePixelsPerCell + ShapeSpritePaddingPixels * 2;

        shapeTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        shapeTexture.filterMode = FilterMode.Point;
        shapeTexture.wrapMode = TextureWrapMode.Clamp;

        Color[] clearPixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < clearPixels.Length; i++)
        {
            clearPixels[i] = Color.clear;
        }

        shapeTexture.SetPixels(clearPixels);

        foreach (ColoredCellData cell in cells)
        {
            Color cellColor = Darken(holderBoard.GetColor(cell.color), ShapeSpriteDarkenAmount);
            int startX = (cell.offset.x - min.x) * ShapeSpritePixelsPerCell;
            int startY = (cell.offset.y - min.y) * ShapeSpritePixelsPerCell;
            int fillSize = ShapeSpritePixelsPerCell + ShapeSpritePaddingPixels * 2;

            FillShapeTextureRect(startX, startY, fillSize, fillSize, cellColor, textureWidth, textureHeight);
        }

        shapeTexture.Apply();

        Vector2 pivot = new Vector2(
            (ShapeSpritePaddingPixels + (-min.x + 0.5f) * ShapeSpritePixelsPerCell) / textureWidth,
            (ShapeSpritePaddingPixels + (-min.y + 0.5f) * ShapeSpritePixelsPerCell) / textureHeight
        );
        float pixelsPerUnit = ShapeSpritePixelsPerCell / cellSize;
        shapeSprite = Sprite.Create(shapeTexture, new Rect(0f, 0f, textureWidth, textureHeight), pivot, pixelsPerUnit);
        shapeSprite.name = $"{name}_ShapeSprite";

        GameObject shapeObject = new GameObject("ShapeSprite");
        shapeObject.transform.SetParent(transform, false);
        shapeSpriteRenderer = shapeObject.AddComponent<SpriteRenderer>();
        shapeSpriteRenderer.sprite = shapeSprite;
        shapeSpriteRenderer.color = Color.white;
        shapeSpriteRenderer.enabled = shapeSpriteVisible;
        shapeSpriteRenderer.sortingOrder = 21;
    }

    private void ApplyBlockShadowColor(GameObject cellObject, Color color)
    {
        Transform shadowTransform = cellObject.transform.Find("Shadow");
        SpriteRenderer shadowRenderer = shadowTransform == null ? null : shadowTransform.GetComponent<SpriteRenderer>();

        if (shadowRenderer != null)
        {
            shadowRenderer.color = Darken(color, ShadowDarkenAmount);
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

    public void SetShapeSpriteVisible(bool visible)
    {
        shapeSpriteVisible = visible;

        if (shapeSpriteRenderer != null)
        {
            shapeSpriteRenderer.enabled = visible;
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
                StartCoroutine(BounceCell(cell.transform, 0f, CompletedColorBounceDuration, CompletedColorBounceHeightCells));
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

            cell.transform.SetParent(detachedParent, true);
            detachedAnyCell = true;

            if (detachedCells != null && !detachedCells.Contains(cell))
            {
                detachedCells.Add(cell);
            }
        }

        if (detachedAnyCell)
        {
            SetShapeSpriteVisible(false);
        }
    }

    private IEnumerator BounceCell(Transform cellTransform, float delaySeconds, float duration, float heightInCells)
    {
        if (cellTransform == null)
        {
            yield break;
        }

        if (delaySeconds > 0f)
        {
            yield return new WaitForSeconds(delaySeconds);
        }

        Vector3 startPosition = cellTransform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (cellTransform == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float height = Mathf.Sin(progress * Mathf.PI) * cellSize * heightInCells;
            cellTransform.localPosition = startPosition + Vector3.up * height;
            yield return null;
        }

        if (cellTransform != null)
        {
            cellTransform.localPosition = startPosition;
        }
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

    private void FillShapeTextureRect(int startX, int startY, int width, int height, Color color, int textureWidth, int textureHeight)
    {
        int endX = Mathf.Min(startX + width, textureWidth);
        int endY = Mathf.Min(startY + height, textureHeight);

        for (int y = Mathf.Max(startY, 0); y < endY; y++)
        {
            for (int x = Mathf.Max(startX, 0); x < endX; x++)
            {
                shapeTexture.SetPixel(x, y, color);
            }
        }
    }

    private void DestroyGeneratedShapeSprite()
    {
        if (shapeSpriteRenderer != null)
        {
            Destroy(shapeSpriteRenderer.gameObject);
            shapeSpriteRenderer = null;
        }

        if (shapeSprite != null)
        {
            Destroy(shapeSprite);
            shapeSprite = null;
        }

        if (shapeTexture != null)
        {
            Destroy(shapeTexture);
            shapeTexture = null;
        }
    }

    private void OnDestroy()
    {
        DestroyGeneratedShapeSprite();
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
        SetShapeSpriteVisible(true);
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
            SetSortingOrder(35);
            levelManager.RefreshCompletedBlockShapeSprites();
            return;
        }

        transform.position = trayWorldPosition;
        State = PieceState.InTray;
        SetBlockShadowVisible(true);
        SetSortingOrder(20);
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

    private int GetRendererSortingOrder(SpriteRenderer spriteRenderer, int baseSortingOrder)
    {
        if (spriteRenderer.name == "Shadow")
        {
            return baseSortingOrder;
        }

        if (spriteRenderer.name == "ShapeSprite")
        {
            return baseSortingOrder + 1;
        }

        if (spriteRenderer.name == "Visual")
        {
            return baseSortingOrder + 2;
        }

        if (spriteRenderer.name == ScrewObjectName)
        {
            return baseSortingOrder + 3;
        }

        return baseSortingOrder;
    }

    private Color Darken(Color color, float amount)
    {
        Color darkerColor = Color.Lerp(color, Color.black, amount);
        darkerColor.a = color.a;
        return darkerColor;
    }

}
