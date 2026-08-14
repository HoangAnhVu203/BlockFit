using UnityEngine;

[System.Serializable]
public struct ColoredCellData
{
    public Vector2Int offset;
    public BlockColorId color;
}

[System.Serializable]
public class HolderCellData
{
    public Vector2Int position;
    public BlockColorId color = BlockColorId.Gray;
}

[System.Serializable]
public class LevelPieceData
{
    public string id;
    public Vector2Int trayPosition;
    public System.Collections.Generic.List<ColoredCellData> cells = new System.Collections.Generic.List<ColoredCellData>();
}

[System.Serializable]
public class FixedHolderBlockData
{
    public string id;
    public Vector2Int holderAnchor;
    public System.Collections.Generic.List<ColoredCellData> cells = new System.Collections.Generic.List<ColoredCellData>();
}

[System.Serializable]
public class PieceWaveData
{
    public System.Collections.Generic.List<LevelPieceData> pieces = new System.Collections.Generic.List<LevelPieceData>();
}

public class Cell : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] colorRenderers;
    [SerializeField] private string[] colorChildNames = { "Visual" };
    [SerializeField] private GameObject wallLeft;
    [SerializeField] private GameObject wallRight;
    [SerializeField] private GameObject wallBack;
    [SerializeField] private GameObject wallFront;

    public Vector2Int GridPosition { get; private set; }
    public BlockColorId ColorId { get; private set; }

    private void Awake()
    {
        ResolveSpriteRenderers();
    }

    public void Setup(Vector2Int gridPosition, BlockColorId colorId, Color color, int sortingOrder)
    {
        GridPosition = gridPosition;
        ColorId = colorId;

        ApplyColor(color, sortingOrder);
    }

    public void SetVisual(Color color, int sortingOrder)
    {
        ApplyColor(color, sortingOrder);
    }

    public void SetWalls(bool left, bool right, bool back, bool front)
    {
        SetWalls(left, right, back, front, 0);
    }

    public void SetWalls(bool left, bool right, bool back, bool front, int sortingOrder)
    {
        ResolveWalls();

        SetWall(wallLeft, left, sortingOrder + 1);
        SetWall(wallRight, right, sortingOrder + 1);
        SetWall(wallBack, back, sortingOrder);
        SetWall(wallFront, front, sortingOrder + 2);
    }

    public void ClearWalls()
    {
        ResolveWalls();

        SetWall(wallLeft, false, 0);
        SetWall(wallRight, false, 0);
        SetWall(wallBack, false, 0);
        SetWall(wallFront, false, 0);
    }

    private void ApplyColor(Color color, int sortingOrder)
    {
        ResolveSpriteRenderers();

        foreach (SpriteRenderer spriteRenderer in colorRenderers)
        {
            if (spriteRenderer == null || !ShouldTintRenderer(spriteRenderer))
            {
                continue;
            }

            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = GetSortingOrder(spriteRenderer, sortingOrder);
        }
    }

    private int GetSortingOrder(SpriteRenderer spriteRenderer, int baseSortingOrder)
    {
        if (spriteRenderer.name == "Visual")
        {
            return baseSortingOrder + 2;
        }

        return baseSortingOrder;
    }

    private void ResolveSpriteRenderers()
    {
        if (colorRenderers != null && colorRenderers.Length > 0)
        {
            return;
        }

        System.Collections.Generic.List<SpriteRenderer> resolvedRenderers = new System.Collections.Generic.List<SpriteRenderer>();

        if (colorChildNames != null)
        {
            foreach (string childName in colorChildNames)
            {
                if (string.IsNullOrEmpty(childName))
                {
                    continue;
                }

                Transform child = transform.Find(childName);
                SpriteRenderer childRenderer = child == null ? null : child.GetComponent<SpriteRenderer>();
                if (childRenderer != null && !resolvedRenderers.Contains(childRenderer))
                {
                    resolvedRenderers.Add(childRenderer);
                }
            }
        }

        if (resolvedRenderers.Count == 0)
        {
            SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
            if (rootRenderer != null)
            {
                resolvedRenderers.Add(rootRenderer);
            }
        }

        colorRenderers = resolvedRenderers.ToArray();
    }

    private bool ShouldTintRenderer(SpriteRenderer spriteRenderer)
    {
        return spriteRenderer.name == "Visual";
    }

    private void ResolveWalls()
    {
        wallLeft = ResolveChildObject(wallLeft, "WallLeft");
        wallRight = ResolveChildObject(wallRight, "WallRight");
        wallBack = ResolveChildObject(wallBack, "WallBack");
        wallFront = ResolveChildObject(wallFront, "WallFront");
    }

    private GameObject ResolveChildObject(GameObject current, string childName)
    {
        if (current != null)
        {
            return current;
        }

        Transform child = transform.Find(childName);
        return child == null ? null : child.gameObject;
    }

    private void SetWall(GameObject wall, bool active, int sortingOrder)
    {
        if (wall == null)
        {
            return;
        }

        SpriteRenderer[] renderers = wall.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            spriteRenderer.sortingOrder = sortingOrder;
        }

        wall.SetActive(active);
    }
}
