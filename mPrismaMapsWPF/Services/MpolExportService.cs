using System.IO;
using System.Text.Json;
using ACadSharp.Entities;
using mPrismaMapsWPF.Models;

namespace mPrismaMapsWPF.Services;

public class MpolExportService
{
    private readonly IWalkwayService _walkwayService;

    public MpolExportService(IWalkwayService walkwayService)
    {
        _walkwayService = walkwayService;
    }

    public MpolMap Export(CadDocumentModel document, string storeName, HashSet<string> hiddenLayers,
        bool flipX = false, bool flipY = false, double viewRotation = 0)
    {
        var entities = document.ModelSpaceEntities.ToList();

        // Collect unit numbers (MText on "Unit Numbers" layer)
        var unitNumbers = entities
            .OfType<MText>()
            .Where(m => m.Layer?.Name == CadDocumentModel.UnitNumbersLayerName)
            .Where(m => !hiddenLayers.Contains(m.Layer!.Name))
            .ToList();

        // Collect unit area polylines (LwPolyline on "Unit Areas" layer)
        var unitAreas = entities
            .OfType<LwPolyline>()
            .Where(p => p.Layer?.Name == CadDocumentModel.UnitAreasLayerName)
            .Where(p => !hiddenLayers.Contains(p.Layer!.Name))
            .ToList();

        // Collect background contour polylines (LwPolyline on "Background Contours" layer)
        var backgroundContours = entities
            .OfType<LwPolyline>()
            .Where(p => p.Layer?.Name == CadDocumentModel.BackgroundContoursLayerName)
            .Where(p => !hiddenLayers.Contains(p.Layer!.Name))
            .ToList();

        // Precompute view transform (rotation + flips applied to raw CAD coords)
        double rotRad = viewRotation * Math.PI / 180.0;
        double cosR = Math.Cos(rotRad);
        double sinR = Math.Sin(rotRad);

        (double x, double y) ApplyViewTransform(double x, double y)
        {
            // Apply rotation first
            double rx = x * cosR - y * sinR;
            double ry = x * sinR + y * cosR;
            // Then flips
            if (flipX) rx = -rx;
            if (flipY) ry = -ry;
            return (rx, ry);
        }

        // Compute search radius for walkway path finding
        double maxNodeDistance = ComputeMaxNodeDistance();

        // Build raw units (before scaling)
        var rawUnits = new List<(string unitNumber, List<List<(double x, double y)>> polygons,
            List<(double x, double y)> path, double pathDistance)>();

        foreach (var mtext in unitNumbers)
        {
            double px = mtext.InsertPoint.X;
            double py = mtext.InsertPoint.Y;

            // Find enclosing unit area via point-in-polygon
            var enclosingArea = unitAreas.FirstOrDefault(poly => IsPointInPolygon(px, py, poly));
            if (enclosingArea == null)
                continue;

            var vertices = ExtractVertices(enclosingArea)
                .Select(v => ApplyViewTransform(v.x, v.y)).ToList();
            var polygons = new List<List<(double x, double y)>> { vertices };

            // Find path to entrance
            List<(double x, double y)> pathCoords = new();
            double pathDistance = 0;
            var pathResult = _walkwayService.Graph.FindPathCoordinatesToEntrance(px, py, maxNodeDistance);
            if (pathResult != null)
            {
                pathCoords = pathResult.Value.path
                    .Select(p => ApplyViewTransform(p.x, p.y)).ToList();
                pathDistance = pathResult.Value.distance;
            }

            rawUnits.Add((mtext.Value, polygons, pathCoords, pathDistance));
        }

        // Collect all raw background contour vertices (with view transform applied)
        var rawBackgrounds = backgroundContours
            .Select(c => ExtractVertices(c)
                .Select(v => ApplyViewTransform(v.x, v.y)).ToList())
            .ToList();

        // Compute bounding box of all geometry
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        void ExpandBounds(double x, double y)
        {
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        foreach (var (_, polygons, path, _) in rawUnits)
        {
            foreach (var poly in polygons)
                foreach (var (x, y) in poly)
                    ExpandBounds(x, y);
            foreach (var (x, y) in path)
                ExpandBounds(x, y);
        }

        foreach (var bg in rawBackgrounds)
            foreach (var (x, y) in bg)
                ExpandBounds(x, y);

        // Scale to 300x300
        const int targetSize = 300;
        const int margin = 3;
        double bboxWidth = maxX - minX;
        double bboxHeight = maxY - minY;

        // Avoid division by zero
        if (bboxWidth <= 0) bboxWidth = 1;
        if (bboxHeight <= 0) bboxHeight = 1;

        double factor = bboxWidth > bboxHeight ? targetSize / bboxWidth : targetSize / bboxHeight;
        double scaledOffsetX = minX * factor - margin;
        double scaledOffsetY = minY * factor - margin;

        double[] Transform(double x, double y) => [x * factor - scaledOffsetX, y * factor - scaledOffsetY];

        // Build the export model
        var map = new MpolMap { Name = storeName };

        foreach (var (unitNumber, polygons, path, pathDistance) in rawUnits)
        {
            var mpolUnit = new MpolUnit { UnitNumber = unitNumber };

            double uMinX = double.MaxValue, uMinY = double.MaxValue;
            double uMaxX = double.MinValue, uMaxY = double.MinValue;

            foreach (var poly in polygons)
            {
                var transformedPoly = new List<double[]>();
                var first = true;
                double[] firstPoint = null!;

                foreach (var (x, y) in poly)
                {
                    var tp = Transform(x, y);
                    transformedPoly.Add(tp);

                    if (first) { firstPoint = tp; first = false; }

                    if (tp[0] < uMinX) uMinX = tp[0];
                    if (tp[0] > uMaxX) uMaxX = tp[0];
                    if (tp[1] < uMinY) uMinY = tp[1];
                    if (tp[1] > uMaxY) uMaxY = tp[1];
                }

                // Close the polygon by appending the first point
                if (firstPoint != null)
                    transformedPoly.Add(firstPoint);

                mpolUnit.Polygons.Add(transformedPoly);
            }

            mpolUnit.Width = uMaxX - uMinX;
            mpolUnit.Height = uMaxY - uMinY;
            mpolUnit.IsVertical = mpolUnit.Height > mpolUnit.Width;
            mpolUnit.Area = CalculateArea(mpolUnit.Polygons.FirstOrDefault() ?? new());

            // Transform path
            double scaledPathDistance = 0;
            for (int i = 0; i < path.Count; i++)
            {
                var tp = Transform(path[i].x, path[i].y);
                mpolUnit.Path.Add(tp);

                if (i > 0)
                {
                    var prev = mpolUnit.Path[i - 1];
                    double dx = tp[0] - prev[0];
                    double dy = tp[1] - prev[1];
                    scaledPathDistance += Math.Sqrt(dx * dx + dy * dy);
                }
            }
            mpolUnit.Distance = scaledPathDistance;

            map.Units.Add(mpolUnit);
        }

        // Transform background contours
        foreach (var bg in rawBackgrounds)
        {
            var transformedBg = new List<double[]>();
            double[]? firstPoint = null;

            foreach (var (x, y) in bg)
            {
                var tp = Transform(x, y);
                transformedBg.Add(tp);
                firstPoint ??= tp;
            }

            // Close the contour
            if (firstPoint != null)
                transformedBg.Add(firstPoint);

            map.Background.Add(transformedBg);
        }

        return map;
    }

    public static void SerializeToFile(MpolMap map, string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        string json = JsonSerializer.Serialize(map, options);
        File.WriteAllText(filePath, json);
    }

    private static List<(double x, double y)> ExtractVertices(LwPolyline polyline)
    {
        return polyline.Vertices.Select(v => (v.Location.X, v.Location.Y)).ToList();
    }

    private static bool IsPointInPolygon(double px, double py, LwPolyline polygon)
    {
        var vertices = polygon.Vertices;
        int n = vertices.Count;
        if (n < 3) return false;

        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            double xi = vertices[i].Location.X, yi = vertices[i].Location.Y;
            double xj = vertices[j].Location.X, yj = vertices[j].Location.Y;

            if (((yi > py) != (yj > py)) &&
                (px < (xj - xi) * (py - yi) / (yj - yi) + xi))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static double CalculateArea(List<double[]> points)
    {
        double result = 0;
        int n = points.Count;
        for (int i = 0; i < n; i++)
        {
            var p1 = points[i];
            var p2 = points[(i + 1) % n];
            result += (p1[0] * p2[1]) - (p2[0] * p1[1]);
        }
        return Math.Abs(result) / 2.0;
    }

    private double ComputeMaxNodeDistance()
    {
        var graph = _walkwayService.Graph;
        if (graph.Nodes.Count < 2)
            return double.MaxValue;

        if (graph.Edges.Count > 0)
        {
            double avgWeight = graph.Edges.Values.Average(e => e.Weight);
            return avgWeight * 3;
        }

        double mX = double.MaxValue, mxX = double.MinValue;
        double mY = double.MaxValue, mxY = double.MinValue;
        foreach (var node in graph.Nodes.Values)
        {
            mX = Math.Min(mX, node.X);
            mxX = Math.Max(mxX, node.X);
            mY = Math.Min(mY, node.Y);
            mxY = Math.Max(mxY, node.Y);
        }
        double span = Math.Max(mxX - mX, mxY - mY);
        return Math.Max(span * 0.1, 50.0);
    }
}
