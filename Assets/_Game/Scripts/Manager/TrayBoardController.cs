using UnityEngine;
using System.Collections.Generic;

public class TrayBoardController : BoardController
{
    [SerializeField] private float previewBrightnessMultiplier = 1.15f;
    [SerializeField, Range(0f, 1f)] private float previewWhiteBlend = 0.1f;
    [SerializeField] private int previewSortingOrder = 8;
    [SerializeField] private int traySortingOrder = 0;

    private readonly Dictionary<Vector2Int, Cell> trayCells = new Dictionary<Vector2Int, Cell>();
    private readonly Dictionary<Vector2Int, Block> occupiedCells = new Dictionary<Vector2Int, Block>();
    private readonly List<Vector2Int> previewedCells = new List<Vector2Int>();

    public void BuildTray(Vector2Int size)
    {
        SetBoardSize(size);
        trayCells.Clear();
        occupiedCells.Clear();
        previewedCells.Clear();
        ClearChildren();

        for (int y = 0; y < BoardSize.y; y++)
        {
            for (int x = 0; x < BoardSize.x; x++)
            {
                Vector2Int position = new Vector2Int(x, y);
                Cell cell = CreateCell(position, BlockColorId.Gray, GetVisualColor(BlockColorId.Gray));
                cell.ClearWalls();
                trayCells[position] = cell;
            }
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
            if (!trayCells.TryGetValue(position, out Cell cell))
            {
                continue;
            }

            cell.SetVisual(Brighten(GetVisualColor(blockCell.color)), previewSortingOrder);
            previewedCells.Add(position);
        }
    }

    public void ClearPreview()
    {
        foreach (Vector2Int position in previewedCells)
        {
            if (trayCells.TryGetValue(position, out Cell cell))
            {
                cell.SetVisual(GetVisualColor(BlockColorId.Gray), traySortingOrder);
            }
        }

        previewedCells.Clear();
    }

    public bool CanPlace(Block block, Vector2Int anchor)
    {
        foreach (ColoredCellData blockCell in block.Cells)
        {
            Vector2Int position = anchor + blockCell.offset;

            if (!InBounds(position))
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

        foreach (Vector2Int trayPosition in trayCells.Keys)
        {
            foreach (ColoredCellData blockCell in block.Cells)
            {
                candidates.Add(trayPosition - blockCell.offset);
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

        block.SnapToTray(anchor);
    }

    public void RemoveBlock(Block block)
    {
        ClearOccupancy(block);
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

    private Color Brighten(Color color)
    {
        Color brightColor = new Color(
            Mathf.Clamp01(color.r * previewBrightnessMultiplier),
            Mathf.Clamp01(color.g * previewBrightnessMultiplier),
            Mathf.Clamp01(color.b * previewBrightnessMultiplier),
            color.a
        );

        return Color.Lerp(brightColor, Color.white, previewWhiteBlend);
    }
}
