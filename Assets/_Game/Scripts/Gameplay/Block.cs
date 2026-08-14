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

    private readonly List<ColoredCellData> cells = new List<ColoredCellData>();

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

        cells.Clear();
        cells.AddRange(data.cells);

        name = $"FixedBlock_{Id}";
        BuildVisuals();
    }

    public void MoveToTray(Vector2Int trayPosition)
    {
        trayWorldPosition = trayBoard.GridToWorld(trayPosition);
        transform.position = trayWorldPosition;
        State = PieceState.InTray;
        SetBlockShadowVisible(true);
        SetSortingOrder(20);
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
        }

        BuildShapeSprite();
        RemoveRootCollider();
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

        return baseSortingOrder;
    }

    private Color Darken(Color color, float amount)
    {
        Color darkerColor = Color.Lerp(color, Color.black, amount);
        darkerColor.a = color.a;
        return darkerColor;
    }

}
