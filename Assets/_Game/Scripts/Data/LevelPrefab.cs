using System.Collections.Generic;
using UnityEngine;

public class LevelPrefab : MonoBehaviour
{
    [SerializeField] private string levelId = "Level";
    [SerializeField] private Vector2Int holderSize = new Vector2Int(7, 7);
    [SerializeField] private Vector2Int traySize = new Vector2Int(7, 3);
    [SerializeField] private List<HolderCellData> holderCells = new List<HolderCellData>();
    [SerializeField] private List<FixedHolderBlockData> fixedBlocks = new List<FixedHolderBlockData>();
    [SerializeField] private List<PieceWaveData> waves = new List<PieceWaveData>();

    public string LevelId => levelId;
    public Vector2Int HolderSize => holderSize;
    public Vector2Int TraySize => traySize;
    public IReadOnlyList<HolderCellData> HolderCells => holderCells;
    public IReadOnlyList<FixedHolderBlockData> FixedBlocks => fixedBlocks;
    public IReadOnlyList<PieceWaveData> Waves => waves;

    private void OnValidate()
    {
        holderSize = HolderBoardController.ClampHolderSize(holderSize);
    }

    public void SetData(
        string id,
        Vector2Int newHolderSize,
        Vector2Int newTraySize,
        IEnumerable<HolderCellData> newHolderCells,
        IEnumerable<FixedHolderBlockData> newFixedBlocks,
        IEnumerable<PieceWaveData> newWaves)
    {
        levelId = string.IsNullOrWhiteSpace(id) ? name : id;
        holderSize = HolderBoardController.ClampHolderSize(newHolderSize);
        traySize = newTraySize;
        holderCells = LevelDataUtility.CloneHolderCells(newHolderCells);
        fixedBlocks = LevelDataUtility.CloneFixedBlocks(newFixedBlocks);
        waves = LevelDataUtility.CloneWaves(newWaves);
    }
}
