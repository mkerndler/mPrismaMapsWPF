using System.Windows;
using ACadSharp.Entities;
using WpfPoint = System.Windows.Point;

namespace mPrismaMapsWPF.Helpers;

/// <summary>
/// Uniform grid spatial index for fast entity hit testing.
/// Divides the document extents into cells and maps entities to cells based on their bounding boxes.
/// </summary>
public class SpatialGrid
{
    private readonly int _cols;
    private readonly int _rows;
    private readonly double _cellWidth;
    private readonly double _cellHeight;
    private readonly double _minX;
    private readonly double _minY;
    private readonly List<Entity>?[,] _cells;
    private const int DefaultGridSize = 50;

    private SpatialGrid(double minX, double minY, double width, double height, int cols, int rows)
    {
        _minX = minX;
        _minY = minY;
        _cols = cols;
        _rows = rows;
        _cellWidth = width / cols;
        _cellHeight = height / rows;
        _cells = new List<Entity>?[cols, rows];
    }

    /// <summary>
    /// Builds a spatial grid from the given entities and extents.
    /// </summary>
    public static SpatialGrid Build(IEnumerable<Entity> entities, Rect extents)
    {
        double width = Math.Max(extents.Width, 1);
        double height = Math.Max(extents.Height, 1);

        int cols = Math.Min(DefaultGridSize, Math.Max(1, (int)Math.Ceiling(width / (width / DefaultGridSize))));
        int rows = Math.Min(DefaultGridSize, Math.Max(1, (int)Math.Ceiling(height / (height / DefaultGridSize))));

        var grid = new SpatialGrid(extents.X, extents.Y, width, height, cols, rows);

        foreach (var entity in entities)
        {
            grid.Insert(entity);
        }

        return grid;
    }

    /// <summary>
    /// Inserts an entity into the grid based on its bounding box.
    /// </summary>
    public void Insert(Entity entity)
    {
        var bounds = BoundingBoxHelper.GetBounds(entity);
        if (!bounds.HasValue)
            return;

        GetCellRange(bounds.Value, out int minCol, out int minRow, out int maxCol, out int maxRow);

        for (int col = minCol; col <= maxCol; col++)
        {
            for (int row = minRow; row <= maxRow; row++)
            {
                _cells[col, row] ??= new List<Entity>();
                _cells[col, row]!.Add(entity);
            }
        }
    }

    /// <summary>
    /// Removes an entity from the grid.
    /// </summary>
    public void Remove(Entity entity)
    {
        var bounds = BoundingBoxHelper.GetBounds(entity);
        if (!bounds.HasValue)
            return;

        GetCellRange(bounds.Value, out int minCol, out int minRow, out int maxCol, out int maxRow);

        for (int col = minCol; col <= maxCol; col++)
        {
            for (int row = minRow; row <= maxRow; row++)
            {
                _cells[col, row]?.Remove(entity);
            }
        }
    }

    /// <summary>
    /// Queries the grid for entities near the given point within the specified tolerance.
    /// Returns a deduplicated list of candidate entities.
    /// </summary>
    public List<Entity> Query(WpfPoint point, double tolerance)
    {
        var queryBounds = new Rect(
            point.X - tolerance, point.Y - tolerance,
            tolerance * 2, tolerance * 2);

        GetCellRange(queryBounds, out int minCol, out int minRow, out int maxCol, out int maxRow);

        var seen = new HashSet<ulong>();
        var result = new List<Entity>();

        for (int col = minCol; col <= maxCol; col++)
        {
            for (int row = minRow; row <= maxRow; row++)
            {
                var cell = _cells[col, row];
                if (cell == null)
                    continue;

                foreach (var entity in cell)
                {
                    if (seen.Add(entity.Handle))
                    {
                        result.Add(entity);
                    }
                }
            }
        }

        return result;
    }

    private void GetCellRange(Rect bounds, out int minCol, out int minRow, out int maxCol, out int maxRow)
    {
        minCol = Math.Clamp((int)Math.Floor((bounds.Left   - _minX) / _cellWidth),      0, _cols - 1);
        minRow = Math.Clamp((int)Math.Floor((bounds.Top    - _minY) / _cellHeight),     0, _rows - 1);
        maxCol = Math.Clamp((int)Math.Ceiling((bounds.Right  - _minX) / _cellWidth)  - 1, 0, _cols - 1);
        maxRow = Math.Clamp((int)Math.Ceiling((bounds.Bottom - _minY) / _cellHeight) - 1, 0, _rows - 1);
    }
}
