using System.Collections.Generic;
using UnityEngine;

public static class LevelDataUtility
{
    public static List<HolderCellData> CloneHolderCells(IEnumerable<HolderCellData> source)
    {
        List<HolderCellData> result = new List<HolderCellData>();
        if (source == null)
        {
            return result;
        }

        foreach (HolderCellData cell in source)
        {
            result.Add(new HolderCellData
            {
                position = cell.position,
                color = cell.color
            });
        }

        return result;
    }

    public static List<FixedHolderBlockData> CloneFixedBlocks(IEnumerable<FixedHolderBlockData> source)
    {
        List<FixedHolderBlockData> result = new List<FixedHolderBlockData>();
        if (source == null)
        {
            return result;
        }

        foreach (FixedHolderBlockData block in source)
        {
            FixedHolderBlockData clone = new FixedHolderBlockData
            {
                id = block.id,
                holderAnchor = block.holderAnchor,
                cells = CloneColoredCells(block.cells)
            };

            result.Add(clone);
        }

        return result;
    }

    public static List<PieceWaveData> CloneWaves(IEnumerable<PieceWaveData> source)
    {
        List<PieceWaveData> result = new List<PieceWaveData>();
        if (source == null)
        {
            return result;
        }

        foreach (PieceWaveData wave in source)
        {
            PieceWaveData waveClone = new PieceWaveData();
            if (wave.pieces != null)
            {
                foreach (LevelPieceData piece in wave.pieces)
                {
                    waveClone.pieces.Add(new LevelPieceData
                    {
                        id = piece.id,
                        trayPosition = piece.trayPosition,
                        cells = CloneColoredCells(piece.cells)
                    });
                }
            }

            result.Add(waveClone);
        }

        return result;
    }

    public static List<ColoredCellData> CloneColoredCells(IEnumerable<ColoredCellData> source)
    {
        List<ColoredCellData> result = new List<ColoredCellData>();
        if (source == null)
        {
            return result;
        }

        foreach (ColoredCellData cell in source)
        {
            result.Add(new ColoredCellData
            {
                offset = cell.offset,
                color = cell.color
            });
        }

        return result;
    }
}
