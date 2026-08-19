using System.Collections;
using System.Collections.Generic;
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

        SetWall(wallLeft, left, sortingOrder + 5);
        SetWall(wallRight, right, sortingOrder + 5);
        SetWall(wallBack, back, sortingOrder);
        SetWall(wallFront, front, sortingOrder + 5);
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

public class CellBounceAnimator : MonoBehaviour
{
    private const int NoSortingOverride = int.MinValue;

    private readonly List<SpriteRenderer> sortingRenderers = new List<SpriteRenderer>();
    private readonly List<int> originalSortingOrders = new List<int>();
    private Coroutine bounceCoroutine;
    private bool sortingOrderOverridden;

    public void Play(float delaySeconds, float duration, float height)
    {
        Play(delaySeconds, duration, height, NoSortingOverride);
    }

    public void Play(float delaySeconds, float duration, float height, int raisedSortingOrder)
    {
        if (bounceCoroutine != null)
        {
            StopCoroutine(bounceCoroutine);
            bounceCoroutine = null;
        }

        RestoreSortingOrders();
        bounceCoroutine = StartCoroutine(PlayRoutine(Mathf.Max(0f, delaySeconds), duration, height, raisedSortingOrder));
    }

    private IEnumerator PlayRoutine(float delaySeconds, float duration, float height, int raisedSortingOrder)
    {
        if (delaySeconds > 0f)
        {
            yield return new WaitForSeconds(delaySeconds);
        }

        ApplyRaisedSortingOrder(raisedSortingOrder);

        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            transform.position = startPosition + Vector3.up * Mathf.Sin(progress * Mathf.PI) * height;
            yield return null;
        }

        transform.position = startPosition;
        RestoreSortingOrders();
        bounceCoroutine = null;
    }

    private void ApplyRaisedSortingOrder(int raisedSortingOrder)
    {
        if (raisedSortingOrder == NoSortingOverride)
        {
            return;
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        sortingRenderers.Clear();
        originalSortingOrders.Clear();

        int minSortingOrder = renderers[0].sortingOrder;
        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            if (spriteRenderer != null)
            {
                minSortingOrder = Mathf.Min(minSortingOrder, spriteRenderer.sortingOrder);
            }
        }

        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            if (spriteRenderer == null)
            {
                continue;
            }

            sortingRenderers.Add(spriteRenderer);
            originalSortingOrders.Add(spriteRenderer.sortingOrder);
            spriteRenderer.sortingOrder = raisedSortingOrder + (spriteRenderer.sortingOrder - minSortingOrder);
        }

        sortingOrderOverridden = true;
    }

    private void RestoreSortingOrders()
    {
        if (!sortingOrderOverridden)
        {
            return;
        }

        for (int i = 0; i < sortingRenderers.Count; i++)
        {
            SpriteRenderer spriteRenderer = sortingRenderers[i];
            if (spriteRenderer != null && i < originalSortingOrders.Count)
            {
                spriteRenderer.sortingOrder = originalSortingOrders[i];
            }
        }

        sortingRenderers.Clear();
        originalSortingOrders.Clear();
        sortingOrderOverridden = false;
    }

    private void OnDisable()
    {
        RestoreSortingOrders();
    }
}
