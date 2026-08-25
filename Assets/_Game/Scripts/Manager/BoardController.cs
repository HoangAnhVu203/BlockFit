using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public abstract class BoardController : MonoBehaviour
{
    [System.Serializable]
    public class BlockColorMapping
    {
        public BlockColorId id;
        [FormerlySerializedAs("color")] public Color visualColor = Color.white;
        public Color shadowColor = Color.black;
        [FormerlySerializedAs("shapeSpriteColor")] public Color strokeColor = Color.white;
    }

    [Header("Board")]
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private Transform cellRoot;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private int cellSortingOrder = 0;

    [Header("Auto Fit")]
    [SerializeField] private bool autoResizeBoardRenderer;
    [SerializeField] private bool fitGridToRenderer;
    [SerializeField] private SpriteRenderer fitAreaRenderer;
    [SerializeField, Range(0f, 0.45f)] private float fitPaddingPercent = 0.06f;
    [SerializeField] private Vector2 rendererPaddingCells = new Vector2(0.4f, 0.4f);
    [SerializeField] private float minCellSize = 0.1f;

    [Header("Palette")]
    [SerializeField] private List<BlockColorMapping> palette = new List<BlockColorMapping>
    {
        new BlockColorMapping { id = BlockColorId.Red, visualColor = new Color(0.86f, 0.16f, 0.05f), shadowColor = new Color(0.45f, 0.05f, 0.02f), strokeColor = new Color(0.70f, 0.10f, 0.03f) },
        new BlockColorMapping { id = BlockColorId.Orange, visualColor = new Color(1f, 0.48f, 0.02f), shadowColor = new Color(0.55f, 0.22f, 0.01f), strokeColor = new Color(0.82f, 0.34f, 0.01f) },
        new BlockColorMapping { id = BlockColorId.Yellow, visualColor = new Color(1f, 0.82f, 0.05f), shadowColor = new Color(0.58f, 0.42f, 0.02f), strokeColor = new Color(0.82f, 0.62f, 0.02f) },
        new BlockColorMapping { id = BlockColorId.Green, visualColor = new Color(0.02f, 0.72f, 0.22f), shadowColor = new Color(0.00f, 0.34f, 0.10f), strokeColor = new Color(0.01f, 0.55f, 0.16f) },
        new BlockColorMapping { id = BlockColorId.Blue, visualColor = new Color(0.02f, 0.36f, 0.95f), shadowColor = new Color(0.01f, 0.12f, 0.45f), strokeColor = new Color(0.01f, 0.25f, 0.75f) },
        new BlockColorMapping { id = BlockColorId.Pink, visualColor = new Color(1f, 0.26f, 0.58f), shadowColor = new Color(0.54f, 0.08f, 0.26f), strokeColor = new Color(0.82f, 0.16f, 0.44f) },
        new BlockColorMapping { id = BlockColorId.Brown, visualColor = new Color(0.48f, 0.22f, 0.02f), shadowColor = new Color(0.24f, 0.10f, 0.01f), strokeColor = new Color(0.36f, 0.15f, 0.01f) },
        new BlockColorMapping { id = BlockColorId.Gray, visualColor = new Color(0.42f, 0.45f, 0.50f), shadowColor = new Color(0.22f, 0.24f, 0.28f), strokeColor = new Color(0.33f, 0.36f, 0.40f) }
    };

    private Vector2Int boardSize;
    private float runtimeCellSize;
    private Vector3 gridWorldCenter;

    public float CellSize => runtimeCellSize > 0f ? runtimeCellSize : cellSize;
    public Vector2Int BoardSize => boardSize;
    public Vector3 CellColliderWorldOffset => GetPrefabColliderWorldOffset(cellPrefab, GetRuntimeCellScale());

    public void SetBaseCellSize(float newCellSize)
    {
        cellSize = Mathf.Max(minCellSize, newCellSize);
        RecalculateGridLayout();
    }

    protected void SetBoardSize(Vector2Int size)
    {
        boardSize = size;
        RecalculateGridLayout();
    }

    protected Cell CreateCell(Vector2Int gridPosition, BlockColorId colorId, Color color)
    {
        PrepareCellRoot();

        GameObject cellObject = Instantiate(cellPrefab, GetRoot());
        cellObject.transform.position = GridToWorld(gridPosition);
        cellObject.transform.localRotation = cellPrefab.transform.localRotation;
        cellObject.transform.localScale = cellPrefab.transform.localScale * GetRuntimeCellScale();

        Cell cell = cellObject.GetComponent<Cell>();
        if (cell == null)
        {
            cell = cellObject.AddComponent<Cell>();
        }

        cell.Setup(gridPosition, colorId, color, cellSortingOrder);
        return cell;
    }

    public bool TryWorldToGrid(Vector3 worldPosition, out Vector2Int gridPosition)
    {
        float activeCellSize = CellSize;
        float originX = gridWorldCenter.x - (boardSize.x - 1) * 0.5f * activeCellSize;
        float originY = gridWorldCenter.y - (boardSize.y - 1) * 0.5f * activeCellSize;
        int x = Mathf.RoundToInt((worldPosition.x - originX) / activeCellSize);
        int y = Mathf.RoundToInt((worldPosition.y - originY) / activeCellSize);
        gridPosition = new Vector2Int(x, y);

        if (!InBounds(gridPosition))
        {
            return false;
        }

        Vector3 snappedWorld = GridToWorld(gridPosition);
        return Vector2.Distance(worldPosition, snappedWorld) <= activeCellSize * 0.65f;
    }

    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        float activeCellSize = CellSize;
        float x = gridWorldCenter.x + (gridPosition.x - (boardSize.x - 1) * 0.5f) * activeCellSize;
        float y = gridWorldCenter.y + (gridPosition.y - (boardSize.y - 1) * 0.5f) * activeCellSize;
        return new Vector3(x, y, transform.position.z);
    }

    public Color GetColor(BlockColorId colorId)
    {
        return GetVisualColor(colorId);
    }

    public Color GetVisualColor(BlockColorId colorId)
    {
        return GetPaletteColor(colorId, PaletteColorRole.Visual);
    }

    public Color GetShadowColor(BlockColorId colorId)
    {
        return GetPaletteColor(colorId, PaletteColorRole.Shadow);
    }

    public Color GetStrokeColor(BlockColorId colorId)
    {
        return GetPaletteColor(colorId, PaletteColorRole.Stroke);
    }

    private Color GetPaletteColor(BlockColorId colorId, PaletteColorRole role)
    {
        foreach (BlockColorMapping mapping in palette)
        {
            if (mapping.id == colorId)
            {
                switch (role)
                {
                    case PaletteColorRole.Shadow:
                        return mapping.shadowColor;
                    case PaletteColorRole.Stroke:
                        return mapping.strokeColor;
                    default:
                        return mapping.visualColor;
                }
            }
        }

        return Color.white;
    }

    private enum PaletteColorRole
    {
        Visual,
        Shadow,
        Stroke
    }

    public bool InBounds(Vector2Int position)
    {
        return position.x >= 0 && position.y >= 0 && position.x < boardSize.x && position.y < boardSize.y;
    }

    private Transform GetRoot()
    {
        return cellRoot == null ? transform : cellRoot;
    }

    protected void ClearChildren()
    {
        Transform root = GetRoot();
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (ShouldKeepChildWhenClearing(child))
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    private bool ShouldKeepChildWhenClearing(Transform child)
    {
        return fitAreaRenderer != null && (child == fitAreaRenderer.transform || fitAreaRenderer.transform.IsChildOf(child));
    }

    private void PrepareCellRoot()
    {
        Transform root = GetRoot();
        if (root == transform || root.parent == null)
        {
            return;
        }

        Vector3 parentScale = root.parent.lossyScale;
        root.localScale = new Vector3(
            InverseScale(parentScale.x),
            InverseScale(parentScale.y),
            InverseScale(parentScale.z)
        );
    }

    private float InverseScale(float scale)
    {
        return Mathf.Approximately(scale, 0f) ? 1f : 1f / scale;
    }

    private float GetRuntimeCellScale()
    {
        float baseCellSize = Mathf.Max(minCellSize, cellSize);
        return Mathf.Approximately(baseCellSize, 0f) ? 1f : CellSize / baseCellSize;
    }

    private Vector3 GetPrefabColliderWorldOffset(GameObject prefab, float scaleMultiplier = 1f)
    {
        if (prefab == null)
        {
            return Vector3.zero;
        }

        BoxCollider2D boxCollider = prefab.GetComponent<BoxCollider2D>();
        if (boxCollider == null)
        {
            return Vector3.zero;
        }

        Vector3 prefabScale = prefab.transform.localScale;
        return new Vector3(
            boxCollider.offset.x * prefabScale.x * scaleMultiplier,
            boxCollider.offset.y * prefabScale.y * scaleMultiplier,
            0f
        );
    }

    private void RecalculateGridLayout()
    {
        runtimeCellSize = Mathf.Max(minCellSize, cellSize);
        gridWorldCenter = transform.position;

        if (fitAreaRenderer == null || boardSize.x <= 0 || boardSize.y <= 0)
        {
            return;
        }

        gridWorldCenter = fitAreaRenderer.bounds.center;

        if (autoResizeBoardRenderer)
        {
            ResizeRendererToGrid();
            gridWorldCenter = fitAreaRenderer.bounds.center;
            return;
        }

        if (!fitGridToRenderer)
        {
            return;
        }

        Bounds rendererBounds = fitAreaRenderer.bounds;
        float availableWidth = rendererBounds.size.x;
        float availableHeight = rendererBounds.size.y;
        float paddingScale = Mathf.Clamp01(1f - fitPaddingPercent * 2f);

        availableWidth *= paddingScale;
        availableHeight *= paddingScale;

        if (availableWidth <= 0f || availableHeight <= 0f)
        {
            return;
        }

        runtimeCellSize = Mathf.Max(minCellSize, Mathf.Min(availableWidth / boardSize.x, availableHeight / boardSize.y));
    }

    private void ResizeRendererToGrid()
    {
        if (fitAreaRenderer.sprite == null)
        {
            return;
        }

        float targetWidth = (boardSize.x + rendererPaddingCells.x * 2f) * CellSize;
        float targetHeight = (boardSize.y + rendererPaddingCells.y * 2f) * CellSize;

        fitAreaRenderer.drawMode = SpriteDrawMode.Sliced;
        fitAreaRenderer.size = new Vector2(targetWidth, targetHeight);
    }
}
