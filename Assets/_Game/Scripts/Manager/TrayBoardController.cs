using UnityEngine;

public class TrayBoardController : BoardController
{
    public void BuildTray(Vector2Int size)
    {
        SetBoardSize(size);
        ClearChildren();

        for (int y = 0; y < BoardSize.y; y++)
        {
            for (int x = 0; x < BoardSize.x; x++)
            {
                Vector2Int position = new Vector2Int(x, y);
                Cell cell = CreateCell(position, BlockColorId.Gray, GetColor(BlockColorId.Gray));
                cell.ClearWalls();
            }
        }
    }
}
