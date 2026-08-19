using System.Collections.Generic;
using UnityEngine;

public class HolderBoardController : BoardController
{
    private const int MinimumSortingOrderPerLowerRow = 10;
    private const int WallBackSortingOffset = 0;
    private const int PlacedShadowSortingOffset = 1;
    private const int PlacedVisualSortingOffset = 2;
    private const int PlacedStrokeSortingOffset = 3;
    private const int PlacedScrewSortingOffset = 4;
    private const int WallFrontSortingOffset = 5;
    private const int RaisedCellSortingOrderOffset = 20;

    [SerializeField] private float previewBrightnessMultiplier = 1.75f;
    [SerializeField, Range(0f, 1f)] private float previewWhiteBlend = 0.25f;
    [SerializeField] private int previewSortingOrder = 8;
    [SerializeField] private int holderSortingOrder = 0;
    [SerializeField] private int wallSortingOrderBase = 3;
    [SerializeField] private int wallSortingOrderPerLowerRow = 2;

    private class HolderSlot
    {
        public HolderCellData data;
        public Cell view;
    }

    private readonly Dictionary<Vector2Int, HolderSlot> holderSlots = new Dictionary<Vector2Int, HolderSlot>();
    private readonly Dictionary<Vector2Int, Block> occupiedCells = new Dictionary<Vector2Int, Block>();
    private readonly List<Vector2Int> previewedCells = new List<Vector2Int>();

    public int PlacedBlockSortingOrder => GetPlacedCellVisualSortingOrder(Vector2Int.zero);
    public int PlacedBlockShadowSortingOrder => holderSortingOrder + 3;
    public int RaisedCellSortingOrder => GetMaxWallFrontSortingOrder() + RaisedCellSortingOrderOffset;

    public int GetPlacedCellShadowSortingOrder(Vector2Int position)
    {
        return GetDepthRowSortingOrder(position) + PlacedShadowSortingOffset;
    }

    public int GetPlacedCellVisualSortingOrder(Vector2Int position)
    {
        return GetDepthRowSortingOrder(position) + PlacedVisualSortingOffset;
    }

    public int GetPlacedCellStrokeSortingOrder(Vector2Int position)
    {
        return GetDepthRowSortingOrder(position) + PlacedStrokeSortingOffset;
    }

    public int GetPlacedCellScrewSortingOrder(Vector2Int position)
    {
        return GetDepthRowSortingOrder(position) + PlacedScrewSortingOffset;
    }

    public void BuildHolder(Vector2Int size, List<HolderCellData> holderCells)
    {
        SetBoardSize(size);
        holderSlots.Clear();
        occupiedCells.Clear();
        previewedCells.Clear();
        ClearChildren();

        foreach (HolderCellData holderCell in holderCells)
        {
            if (!InBounds(holderCell.position) || holderCell.color == BlockColorId.None)
            {
                continue;
            }

            Cell cell = CreateCell(holderCell.position, holderCell.color, GetBaseHolderColor(holderCell.color));
            holderSlots[holderCell.position] = new HolderSlot { data = holderCell, view = cell };
        }

        RefreshHolderWalls();
    }

    public void PreviewPlacement(Block block, Vector2Int anchor)
    {
        ClearPreview();

        if (!CanPlace(block, anchor))
        {
            return;
        }

        foreach (ColoredCellData blockCell in block.Cells)
        {
            Vector2Int position = anchor + blockCell.offset;

            HolderSlot holderSlot = holderSlots[position];

            holderSlot.view.SetVisual(Brighten(GetBaseHolderColor(holderSlot.data.color), previewBrightnessMultiplier), previewSortingOrder);
            previewedCells.Add(position);
        }
    }

    public void PreviewPlacement(Block block, Vector3 blockWorldPosition)
    {
        ClearPreview();

        if (!TryFindBestPlacement(block, blockWorldPosition, out Vector2Int anchor))
        {
            return;
        }

        foreach (ColoredCellData blockCell in block.Cells)
        {
            Vector2Int position = anchor + blockCell.offset;
            HolderSlot holderSlot = holderSlots[position];

            holderSlot.view.SetVisual(Brighten(GetBaseHolderColor(holderSlot.data.color), previewBrightnessMultiplier), previewSortingOrder);
            previewedCells.Add(position);
        }
    }

    public void ClearPreview()
    {
        foreach (Vector2Int position in previewedCells)
        {
            if (holderSlots.TryGetValue(position, out HolderSlot holderSlot))
            {
                holderSlot.view.SetVisual(GetBaseHolderColor(holderSlot.data.color), holderSortingOrder);
            }
        }

        previewedCells.Clear();
    }

    public bool CanPlace(Block block, Vector2Int anchor)
    {
        foreach (ColoredCellData blockCell in block.Cells)
        {
            Vector2Int position = anchor + blockCell.offset;

            if (!holderSlots.TryGetValue(position, out HolderSlot holderSlot))
            {
                return false;
            }

            if (holderSlot.data.color != blockCell.color)
            {
                return false;
            }

            if (occupiedCells.TryGetValue(position, out Block occupyingBlock) && occupyingBlock != block)
            {
                return false;
            }
        }

        return true;
    }

    public bool TryFindBestPlacement(Block block, Vector3 blockWorldPosition, out Vector2Int bestAnchor)
    {
        bestAnchor = Vector2Int.zero;
        float bestScore = float.MaxValue;
        float snapTolerance = CellSize * 0.8f;
        HashSet<Vector2Int> candidates = new HashSet<Vector2Int>();

        foreach (Vector2Int holderPosition in holderSlots.Keys)
        {
            foreach (ColoredCellData blockCell in block.Cells)
            {
                candidates.Add(holderPosition - blockCell.offset);
            }
        }

        foreach (Vector2Int candidate in candidates)
        {
            if (!CanPlace(block, candidate))
            {
                continue;
            }

            float score = GetPlacementScore(block, blockWorldPosition, candidate);
            if (score < bestScore)
            {
                bestScore = score;
                bestAnchor = candidate;
            }
        }

        return bestScore <= snapTolerance;
    }

    public void PlaceBlock(Block block, Vector2Int anchor)
    {
        ClearPreview();
        ClearOccupancy(block);

        foreach (ColoredCellData blockCell in block.Cells)
        {
            occupiedCells[anchor + blockCell.offset] = block;
        }

        block.SnapToHolder(anchor);
    }

    public bool IsComplete()
    {
        foreach (Vector2Int position in holderSlots.Keys)
        {
            if (!occupiedCells.ContainsKey(position))
            {
                return false;
            }
        }

        return holderSlots.Count > 0;
    }

    public bool AreBlockColorsComplete(Block block)
    {
        HashSet<BlockColorId> blockColors = new HashSet<BlockColorId>();

        foreach (ColoredCellData blockCell in block.Cells)
        {
            if (blockCell.color != BlockColorId.None)
            {
                blockColors.Add(blockCell.color);
            }
        }

        if (blockColors.Count == 0)
        {
            return false;
        }

        foreach (BlockColorId colorId in blockColors)
        {
            if (!IsColorComplete(colorId))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsColorComplete(BlockColorId colorId)
    {
        bool hasColorSlot = false;

        foreach (KeyValuePair<Vector2Int, HolderSlot> pair in holderSlots)
        {
            if (pair.Value.data.color != colorId)
            {
                continue;
            }

            hasColorSlot = true;

            if (!occupiedCells.ContainsKey(pair.Key))
            {
                return false;
            }
        }

        return hasColorSlot;
    }

    public bool DoesCellBottomTouchShadowBlockingWall(Vector2Int position)
    {
        Vector2Int belowPosition = position + Vector2Int.down;
        return holderSlots.TryGetValue(belowPosition, out HolderSlot belowSlot) && HasBackWall(belowPosition, belowSlot.data.color);
    }

    private void RefreshHolderWalls()
    {
        foreach (KeyValuePair<Vector2Int, HolderSlot> pair in holderSlots)
        {
            Vector2Int position = pair.Key;
            HolderSlot holderSlot = pair.Value;
            BlockColorId colorId = holderSlot.data.color;

            holderSlot.view.SetWalls(
                HasLeftWall(position, colorId),
                HasRightWall(position, colorId),
                HasBackWall(position, colorId),
                HasFrontWall(position),
                GetWallSortingOrder(position)
            );
        }
    }

    private bool HasBackWall(Vector2Int position, BlockColorId colorId)
    {
        return !holderSlots.TryGetValue(position + Vector2Int.up, out HolderSlot upSlot) || upSlot.data.color != colorId;
    }

    private bool HasFrontWall(Vector2Int position)
    {
        return holderSlots.ContainsKey(position) && !holderSlots.ContainsKey(position + Vector2Int.down);
    }

    private bool HasLeftWall(Vector2Int position, BlockColorId colorId)
    {
        Vector2Int leftPosition = position + Vector2Int.left;
        if (!holderSlots.TryGetValue(leftPosition, out HolderSlot leftSlot))
        {
            return true;
        }

        return leftSlot.data.color == colorId ? false : !HasRightWall(leftPosition, leftSlot.data.color);
    }

    private bool HasRightWall(Vector2Int position, BlockColorId colorId)
    {
        Vector2Int rightPosition = position + Vector2Int.right;
        return !holderSlots.TryGetValue(rightPosition, out HolderSlot rightSlot) || rightSlot.data.color != colorId;
    }

    private int GetWallSortingOrder(Vector2Int position)
    {
        return GetDepthRowSortingOrder(position) + WallBackSortingOffset;
    }

    private int GetMaxWallFrontSortingOrder()
    {
        return GetWallSortingOrder(new Vector2Int(0, 0)) + WallFrontSortingOffset;
    }

    private int GetDepthRowSortingOrder(Vector2Int position)
    {
        int rowsBelowTop = BoardSize.y - 1 - position.y;
        int rowStep = Mathf.Max(wallSortingOrderPerLowerRow, MinimumSortingOrderPerLowerRow);
        return wallSortingOrderBase + rowsBelowTop * rowStep;
    }

    private void ClearOccupancy(Block block)
    {
        List<Vector2Int> positionsToClear = new List<Vector2Int>();

        foreach (KeyValuePair<Vector2Int, Block> pair in occupiedCells)
        {
            if (pair.Value == block)
            {
                positionsToClear.Add(pair.Key);
            }
        }

        foreach (Vector2Int position in positionsToClear)
        {
            occupiedCells.Remove(position);
        }
    }

    private float GetPlacementScore(Block block, Vector3 blockWorldPosition, Vector2Int anchor)
    {
        float maxDistance = 0f;

        foreach (ColoredCellData blockCell in block.Cells)
        {
            Vector3 currentCellWorld = block.GetCellColliderWorldPosition(blockWorldPosition, blockCell.offset);
            Vector3 targetCellWorld = GridToWorld(anchor + blockCell.offset) + CellColliderWorldOffset;
            maxDistance = Mathf.Max(maxDistance, Vector2.Distance(currentCellWorld, targetCellWorld));
        }

        return maxDistance;
    }

    private Color GetBaseHolderColor(BlockColorId colorId)
    {
        Color color = GetVisualColor(colorId);
        return new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, color.a);
    }

    private Color Brighten(Color color, float multiplier)
    {
        Color brightColor = new Color(
            Mathf.Clamp01(color.r * multiplier),
            Mathf.Clamp01(color.g * multiplier),
            Mathf.Clamp01(color.b * multiplier),
            color.a
        );

        return Color.Lerp(brightColor, Color.white, previewWhiteBlend);
    }
}
