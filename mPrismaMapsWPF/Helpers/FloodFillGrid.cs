using ACadSharp.Entities;

namespace mPrismaMapsWPF.Helpers;

public class FloodFillGrid
{
    private readonly bool[,] _walls;
    private readonly int _gridWidth;
    private readonly int _gridHeight;
    private readonly double _cellSize;
    private readonly double _originX;
    private readonly double _originY;

    public FloodFillGrid(double minX, double minY, double maxX, double maxY, double cellSize)
    {
        _cellSize = cellSize;
        // Add 2×cellSize padding around extents
        _originX = minX - 2 * cellSize;
        _originY = minY - 2 * cellSize;
        _gridWidth = (int)Math.Ceiling((maxX - minX + 4 * cellSize) / cellSize);
        _gridHeight = (int)Math.Ceiling((maxY - minY + 4 * cellSize) / cellSize);
        _walls = new bool[_gridWidth, _gridHeight];
    }

    public void RasterizeEntity(Entity entity)
    {
        switch (entity)
        {
            case Line line:
                RasterizeLine(line.StartPoint.X, line.StartPoint.Y, line.EndPoint.X, line.EndPoint.Y);
                break;
            case Arc arc:
                RasterizeArc(arc.Center.X, arc.Center.Y, arc.Radius, arc.StartAngle, arc.EndAngle);
                break;
            case Circle circle:
                RasterizeArc(circle.Center.X, circle.Center.Y, circle.Radius, 0, 2 * Math.PI);
                break;
            case LwPolyline polyline:
                RasterizePolyline(polyline);
                break;
            case Polyline2D polyline2D:
                RasterizePolyline2D(polyline2D);
                break;
        }
    }

    private void RasterizeLine(double x1, double y1, double x2, double y2)
    {
        int gx1 = CadToGridX(x1);
        int gy1 = CadToGridY(y1);
        int gx2 = CadToGridX(x2);
        int gy2 = CadToGridY(y2);

        // Bresenham's line algorithm with 2×2 block marking
        int dx = Math.Abs(gx2 - gx1);
        int dy = Math.Abs(gy2 - gy1);
        int sx = gx1 < gx2 ? 1 : -1;
        int sy = gy1 < gy2 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            MarkWall(gx1, gy1);

            if (gx1 == gx2 && gy1 == gy2)
                break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                gx1 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                gy1 += sy;
            }
        }
    }

    private void RasterizeArc(double cx, double cy, double radius, double startAngle, double endAngle)
    {
        // Sample at cellSize/2 intervals
        double circumference;
        if (Math.Abs(endAngle - startAngle - 2 * Math.PI) < 0.001 ||
            Math.Abs(endAngle - startAngle) < 0.001 && startAngle == 0)
        {
            circumference = 2 * Math.PI * radius;
        }
        else
        {
            double sweep = endAngle - startAngle;
            if (sweep < 0) sweep += 2 * Math.PI;
            circumference = sweep * radius;
        }

        int steps = Math.Max((int)(circumference / (_cellSize / 2)), 8);

        double sweep2 = endAngle - startAngle;
        if (sweep2 <= 0) sweep2 += 2 * Math.PI;
        // Full circle
        if (Math.Abs(sweep2 - 2 * Math.PI) < 0.001)
            sweep2 = 2 * Math.PI;

        double prevX = cx + radius * Math.Cos(startAngle);
        double prevY = cy + radius * Math.Sin(startAngle);

        for (int i = 1; i <= steps; i++)
        {
            double angle = startAngle + sweep2 * i / steps;
            double curX = cx + radius * Math.Cos(angle);
            double curY = cy + radius * Math.Sin(angle);
            RasterizeLine(prevX, prevY, curX, curY);
            prevX = curX;
            prevY = curY;
        }
    }

    private void RasterizePolyline(LwPolyline polyline)
    {
        var vertices = polyline.Vertices.ToList();
        if (vertices.Count < 2)
            return;

        for (int i = 0; i < vertices.Count - 1; i++)
        {
            var v0 = vertices[i];
            var v1 = vertices[i + 1];
            double bulge = v0.Bulge;

            if (Math.Abs(bulge) > 0.0001)
            {
                RasterizeBulgeArc(v0.Location.X, v0.Location.Y, v1.Location.X, v1.Location.Y, bulge);
            }
            else
            {
                RasterizeLine(v0.Location.X, v0.Location.Y, v1.Location.X, v1.Location.Y);
            }
        }

        if (polyline.IsClosed && vertices.Count > 2)
        {
            var vLast = vertices[^1];
            var vFirst = vertices[0];
            double bulge = vLast.Bulge;

            if (Math.Abs(bulge) > 0.0001)
            {
                RasterizeBulgeArc(vLast.Location.X, vLast.Location.Y, vFirst.Location.X, vFirst.Location.Y, bulge);
            }
            else
            {
                RasterizeLine(vLast.Location.X, vLast.Location.Y, vFirst.Location.X, vFirst.Location.Y);
            }
        }
    }

    private void RasterizePolyline2D(Polyline2D polyline)
    {
        var vertices = polyline.Vertices.ToList();
        if (vertices.Count < 2)
            return;

        for (int i = 0; i < vertices.Count - 1; i++)
        {
            RasterizeLine(vertices[i].Location.X, vertices[i].Location.Y,
                          vertices[i + 1].Location.X, vertices[i + 1].Location.Y);
        }

        if (polyline.IsClosed && vertices.Count > 2)
        {
            RasterizeLine(vertices[^1].Location.X, vertices[^1].Location.Y,
                          vertices[0].Location.X, vertices[0].Location.Y);
        }
    }

    private void RasterizeBulgeArc(double x1, double y1, double x2, double y2, double bulge)
    {
        // Convert bulge to arc parameters
        double dx = x2 - x1;
        double dy = y2 - y1;
        double chord = Math.Sqrt(dx * dx + dy * dy);
        if (chord < 1e-10) return;

        double sagitta = Math.Abs(bulge) * chord / 2;
        double radius = (chord * chord / 4 + sagitta * sagitta) / (2 * sagitta);

        // Midpoint of chord
        double mx = (x1 + x2) / 2;
        double my = (y1 + y2) / 2;

        // Unit normal to chord
        double nx = -dy / chord;
        double ny = dx / chord;

        // Distance from midpoint to center
        double d = radius - sagitta;
        double sign = bulge > 0 ? 1 : -1;
        double cx = mx + sign * d * nx;
        double cy = my + sign * d * ny;

        double startAngle = Math.Atan2(y1 - cy, x1 - cx);
        double endAngle = Math.Atan2(y2 - cy, x2 - cx);

        // Ensure correct sweep direction
        if (bulge > 0)
        {
            // CCW
            if (endAngle <= startAngle) endAngle += 2 * Math.PI;
        }
        else
        {
            // CW
            if (startAngle <= endAngle) startAngle += 2 * Math.PI;
            // Swap so we go from start to end in positive direction
            (startAngle, endAngle) = (endAngle, startAngle);
        }

        RasterizeArc(cx, cy, radius, startAngle, endAngle);
    }

    public List<bool[,]> FindWallComponents(int minCellCount)
    {
        var visited = new int[_gridWidth, _gridHeight];
        var components = new List<bool[,]>();
        int componentId = 0;

        for (int y = 0; y < _gridHeight; y++)
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                if (!_walls[x, y] || visited[x, y] != 0)
                    continue;

                componentId++;
                var cells = new List<(int x, int y)>();
                var queue = new Queue<(int x, int y)>();
                queue.Enqueue((x, y));
                visited[x, y] = componentId;

                while (queue.Count > 0)
                {
                    var (cx, cy) = queue.Dequeue();
                    cells.Add((cx, cy));

                    // 8-connected neighbors
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = cx + dx;
                            int ny = cy + dy;
                            if (nx >= 0 && nx < _gridWidth && ny >= 0 && ny < _gridHeight &&
                                _walls[nx, ny] && visited[nx, ny] == 0)
                            {
                                visited[nx, ny] = componentId;
                                queue.Enqueue((nx, ny));
                            }
                        }
                    }
                }

                if (cells.Count < minCellCount)
                    continue;

                var mask = new bool[_gridWidth, _gridHeight];
                foreach (var (cx, cy) in cells)
                    mask[cx, cy] = true;
                components.Add(mask);
            }
        }

        return components;
    }

    public bool[,]? FloodFill(double cadX, double cadY)
    {
        int seedX = CadToGridX(cadX);
        int seedY = CadToGridY(cadY);

        // If seed is on wall, try 4 adjacent cells
        if (!InBounds(seedX, seedY) || _walls[seedX, seedY])
        {
            int[] offsets = { 1, -1, 0, 0 };
            bool found = false;
            for (int i = 0; i < 4; i++)
            {
                int nx = seedX + offsets[i];
                int ny = seedY + offsets[3 - i];
                if (InBounds(nx, ny) && !_walls[nx, ny])
                {
                    seedX = nx;
                    seedY = ny;
                    found = true;
                    break;
                }
            }
            if (!found) return null;
        }

        var filled = new bool[_gridWidth, _gridHeight];
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((seedX, seedY));
        filled[seedX, seedY] = true;

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();

            // Check all 4 neighbors
            ReadOnlySpan<(int dx, int dy)> dirs = [(1, 0), (-1, 0), (0, 1), (0, -1)];
            foreach (var (ddx, ddy) in dirs)
            {
                int nx = x + ddx;
                int ny = y + ddy;

                // If fill reaches grid edge, area is not enclosed
                if (nx < 0 || nx >= _gridWidth || ny < 0 || ny >= _gridHeight)
                    return null;

                if (!filled[nx, ny] && !_walls[nx, ny])
                {
                    filled[nx, ny] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }

        return filled;
    }

    public List<(double x, double y)> ExtractContour(bool[,] filled)
    {
        // Find starting point: first filled cell with an empty neighbor
        int startX = -1, startY = -1;
        for (int y = 0; y < _gridHeight && startX < 0; y++)
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                if (filled[x, y] && HasEmptyNeighbor(filled, x, y))
                {
                    startX = x;
                    startY = y;
                    break;
                }
            }
        }

        if (startX < 0) return new List<(double, double)>();

        // Moore neighborhood boundary tracing
        var contour = new List<(int x, int y)>();
        // 8-connected directions: right, down-right, down, down-left, left, up-left, up, up-right
        int[] dxArr = { 1, 1, 0, -1, -1, -1, 0, 1 };
        int[] dyArr = { 0, 1, 1, 1, 0, -1, -1, -1 };

        int cx = startX, cy = startY;
        int dir = 7; // Start looking from up-right

        contour.Add((cx, cy));
        int maxSteps = _gridWidth * _gridHeight;
        int steps = 0;

        do
        {
            // Start search from (dir + 5) % 8 (backtrack direction + 1)
            int searchDir = (dir + 5) % 8;
            bool found = false;

            for (int i = 0; i < 8; i++)
            {
                int d = (searchDir + i) % 8;
                int nx = cx + dxArr[d];
                int ny = cy + dyArr[d];

                if (nx >= 0 && nx < _gridWidth && ny >= 0 && ny < _gridHeight && filled[nx, ny])
                {
                    cx = nx;
                    cy = ny;
                    dir = d;
                    found = true;
                    break;
                }
            }

            if (!found) break;

            if (cx == startX && cy == startY)
                break;

            contour.Add((cx, cy));
            steps++;
        } while (steps < maxSteps);

        // Convert grid coordinates to CAD coordinates
        var result = new List<(double, double)>(contour.Count);
        foreach (var (gx, gy) in contour)
        {
            result.Add(GridToCad(gx, gy));
        }

        return result;
    }

    public static List<(double x, double y)> SimplifyPolygon(List<(double x, double y)> points, double tolerance)
    {
        if (points.Count < 3)
            return points;

        // Ramer-Douglas-Peucker
        return DouglasPeucker(points, 0, points.Count - 1, tolerance);
    }

    private static List<(double x, double y)> DouglasPeucker(
        List<(double x, double y)> points, int start, int end, double tolerance)
    {
        if (end <= start + 1)
            return new List<(double x, double y)> { points[start], points[end] };

        double maxDist = 0;
        int maxIndex = start;

        double ax = points[start].x, ay = points[start].y;
        double bx = points[end].x, by = points[end].y;

        for (int i = start + 1; i < end; i++)
        {
            double dist = PerpendicularDistance(points[i].x, points[i].y, ax, ay, bx, by);
            if (dist > maxDist)
            {
                maxDist = dist;
                maxIndex = i;
            }
        }

        if (maxDist > tolerance)
        {
            var left = DouglasPeucker(points, start, maxIndex, tolerance);
            var right = DouglasPeucker(points, maxIndex, end, tolerance);

            // Combine, removing duplicate at maxIndex
            var result = new List<(double x, double y)>(left.Count + right.Count - 1);
            result.AddRange(left);
            for (int i = 1; i < right.Count; i++)
                result.Add(right[i]);
            return result;
        }

        return new List<(double x, double y)> { points[start], points[end] };
    }

    private static double PerpendicularDistance(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax;
        double dy = by - ay;
        double lengthSq = dx * dx + dy * dy;

        if (lengthSq < 1e-20)
            return Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));

        return Math.Abs(dy * px - dx * py + bx * ay - by * ax) / Math.Sqrt(lengthSq);
    }

    private void MarkWall(int gx, int gy)
    {
        // 2×2 block marking for gap-free walls
        for (int dx = 0; dx <= 1; dx++)
        {
            for (int dy = 0; dy <= 1; dy++)
            {
                int nx = gx + dx;
                int ny = gy + dy;
                if (nx >= 0 && nx < _gridWidth && ny >= 0 && ny < _gridHeight)
                    _walls[nx, ny] = true;
            }
        }
    }

    private int CadToGridX(double cadX) => (int)Math.Round((cadX - _originX) / _cellSize);
    private int CadToGridY(double cadY) => (int)Math.Round((cadY - _originY) / _cellSize);

    private (double x, double y) GridToCad(int gx, int gy)
    {
        return (_originX + gx * _cellSize, _originY + gy * _cellSize);
    }

    private bool InBounds(int x, int y) => x >= 0 && x < _gridWidth && y >= 0 && y < _gridHeight;

    private bool HasEmptyNeighbor(bool[,] filled, int x, int y)
    {
        if (x > 0 && !filled[x - 1, y]) return true;
        if (x < _gridWidth - 1 && !filled[x + 1, y]) return true;
        if (y > 0 && !filled[x, y - 1]) return true;
        if (y < _gridHeight - 1 && !filled[x, y + 1]) return true;
        return false;
    }
}
