using UnityEngine;

public class TerrainGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public int rows = 5;
    public int columns = 9;
    public float cellWidth = 1.2f;
    public float cellHeight = 1.2f;
    public GameObject cellPrefab;

    private TerrainCell[,] cells;

    private void Awake()
    {
        cells = new TerrainCell[rows, columns];
    }

    private void Start()
    {
        GenerateGrid();
    }

    public void GenerateGrid()
    {
        if (cellPrefab == null) return;

        Vector2 startPos = (Vector2)transform.position;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                Vector2 pos = startPos + new Vector2(col * cellWidth, -row * cellHeight);
                GameObject cellObj = Instantiate(cellPrefab, pos, Quaternion.identity, transform);
                cellObj.name = $"Cell_{row}_{col}";
                cells[row, col] = cellObj.GetComponent<TerrainCell>();
            }
        }
    }

    public TerrainCell GetCell(int row, int col)
    {
        if (row >= 0 && row < rows && col >= 0 && col < columns)
            return cells[row, col];
        return null;
    }
}
