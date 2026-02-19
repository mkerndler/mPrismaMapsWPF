using System.Text.Json;
using System.Text.Json.Serialization;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using CSMath;
using mPrismaMapsWPF.Models;

namespace mPrismaMapsWPF.Services;

/// <summary>
/// Imports and exports the legacy JSON map format produced by the old MPrismaMaps tool.
/// The format is described by <see cref="LegacyMapData"/> and its nested types.
/// </summary>
public class LegacyMapImportExport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    // =========================================================================
    // Import
    // =========================================================================

    /// <summary>
    /// Parses a legacy JSON string and returns a populated <see cref="CadDocumentModel"/>.
    /// Throws <see cref="InvalidOperationException"/> if the JSON cannot be parsed.
    /// </summary>
    public CadDocumentModel Import(string json)
    {
        var mapData = JsonSerializer.Deserialize<LegacyMapData>(json, JsonOptions)
            ?? throw new InvalidOperationException("Legacy JSON contains no valid map data.");

        var model = new CadDocumentModel();
        model.EnsureDocumentExists();
        var modelSpace = model.Document!.ModelSpace;

        // Pre-create the named layers so entities can reference them.
        var userDrawingsLayer = model.GetOrCreateUserDrawingsLayer()!;
        var unitNumbersLayer  = model.GetOrCreateUnitNumbersLayer()!;
        var unitAreasLayer    = model.GetOrCreateUnitAreasLayer()!;
        var bgContoursLayer   = model.GetOrCreateBackgroundContoursLayer()!;
        var walkwaysLayer     = model.GetOrCreateWalkwaysLayer()!;

        // Cache for ad-hoc layers referenced by name in dxfObjects.
        var layerCache = new Dictionary<string, Layer>(StringComparer.OrdinalIgnoreCase)
        {
            [userDrawingsLayer.Name] = userDrawingsLayer,
            [unitNumbersLayer.Name]  = unitNumbersLayer,
            [unitAreasLayer.Name]    = unitAreasLayer,
            [bgContoursLayer.Name]   = bgContoursLayer,
            [walkwaysLayer.Name]     = walkwaysLayer,
        };

        Layer ResolveLayer(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return userDrawingsLayer;

            if (layerCache.TryGetValue(name, out var cached))
                return cached;

            var docLayer = model.Document.Layers
                .FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
            if (docLayer != null)
            {
                layerCache[name] = docLayer;
                return docLayer;
            }

            var newLayer = new Layer(name);
            model.Document.Layers.Add(newLayer);
            layerCache[name] = newLayer;
            return newLayer;
        }

        // --- dxfObjects ---
        if (mapData.DxfObjects != null)
        {
            foreach (var obj in mapData.DxfObjects)
            {
                if (obj.Type == null) continue;

                var layer = obj.Type == "UNIT_NUMBER"
                    ? unitNumbersLayer
                    : ResolveLayer(obj.LayerName);

                Entity? entity = obj.Type switch
                {
                    "LINE"        => ImportLine(obj, layer),
                    "TEXT"        => ImportMText(obj, layer),
                    "MTEXT"       => ImportMText(obj, layer),
                    "UNIT_NUMBER" => ImportMText(obj, layer),
                    "LWPOLYLINE"  => ImportLwPolyline(obj, layer, closed: false),
                    "POLYLINE"    => ImportLwPolyline(obj, layer, closed: true),
                    "ARC"         => ImportArc(obj, layer),
                    "HATCH"       => ImportPointsAsPolyline(obj.Points, layer, closed: true),
                    "SOLID"       => ImportPointsAsPolyline(obj.Points?.Take(4).ToList(), layer, closed: true),
                    _             => null
                };

                if (entity != null)
                    modelSpace.Entities.Add(entity);
            }
        }

        // --- unitContours → "Unit Areas" layer ---
        // Each entry: unit number → [main contour points, hole1 points, ...].
        // Holes are not natively supported in the new tool, so each contour list
        // is imported as a separate closed LwPolyline.
        if (mapData.UnitContours != null)
        {
            foreach (var (_, contourLists) in mapData.UnitContours)
            {
                if (contourLists == null) continue;
                foreach (var pointList in contourLists)
                {
                    var poly = PointsToLwPolyline(pointList, unitAreasLayer, closed: true);
                    if (poly != null)
                        modelSpace.Entities.Add(poly);
                }
            }
        }

        // --- backgroundContours → "Background Contours" layer ---
        if (mapData.BackgroundContours != null)
        {
            foreach (var pointList in mapData.BackgroundContours)
            {
                var poly = PointsToLwPolyline(pointList, bgContoursLayer, closed: true);
                if (poly != null)
                    modelSpace.Entities.Add(poly);
            }
        }

        // --- walkingPaths → "Walkways" layer (Circle per node, Line per edge) ---
        if (mapData.WalkingPaths != null)
            ImportWalkways(mapData.WalkingPaths, modelSpace, walkwaysLayer);

        return model;
    }

    // =========================================================================
    // Export
    // =========================================================================

    /// <summary>
    /// Converts the current document to the legacy JSON string.
    /// <paramref name="storeName"/> and <paramref name="storeNote"/> map to the
    /// legacy <c>storeName</c> / <c>storeNote</c> fields.
    /// </summary>
    public string Export(CadDocumentModel document, string storeName = "", string storeNote = "")
    {
        var entities = document.ModelSpaceEntities.ToList();

        var unitNumbers = entities
            .OfType<MText>()
            .Where(e => e.Layer?.Name == CadDocumentModel.UnitNumbersLayerName)
            .ToList();

        var unitAreas = entities
            .OfType<LwPolyline>()
            .Where(e => e.Layer?.Name == CadDocumentModel.UnitAreasLayerName)
            .ToList();

        var bgContours = entities
            .OfType<LwPolyline>()
            .Where(e => e.Layer?.Name == CadDocumentModel.BackgroundContoursLayerName)
            .ToList();

        var walkwayNodes = entities
            .OfType<Circle>()
            .Where(e => e.Layer?.Name == CadDocumentModel.WalkwaysLayerName)
            .ToList();

        var walkwayEdges = entities
            .OfType<Line>()
            .Where(e => e.Layer?.Name == CadDocumentModel.WalkwaysLayerName)
            .ToList();

        // These layers are exported into their own top-level sections, not dxfObjects.
        var specialLayers = new HashSet<string>
        {
            CadDocumentModel.UnitAreasLayerName,
            CadDocumentModel.BackgroundContoursLayerName,
            CadDocumentModel.WalkwaysLayerName,
        };

        // --- Generic dxfObjects ---
        var dxfObjects = new List<LegacyDxfObject>();
        foreach (var entity in entities)
        {
            if (entity.Layer?.Name != null && specialLayers.Contains(entity.Layer.Name))
                continue;

            LegacyDxfObject? obj = entity switch
            {
                Line line             => ExportLine(line),
                Arc arc               => ExportArc(arc),    // Arc before Circle (Arc : Circle)
                Circle circle         => null,              // bare circles outside walkways: skip
                MText mtext           => ExportMText(mtext),
                TextEntity text       => ExportTextEntity(text),
                LwPolyline poly       => ExportLwPolyline(poly),
                Polyline2D poly2d     => ExportPolyline2D(poly2d),
                _                    => null
            };

            if (obj != null)
                dxfObjects.Add(obj);
        }

        // --- unitContours: match each unit number to its enclosing area polygon ---
        var unitContoursDict = new Dictionary<string, List<List<double[]>>>();
        foreach (var mtext in unitNumbers)
        {
            var area = unitAreas.FirstOrDefault(
                p => IsPointInLwPolyline(mtext.InsertPoint.X, mtext.InsertPoint.Y, p));
            if (area == null) continue;

            var points = area.Vertices
                .Select(v => new double[] { v.Location.X, v.Location.Y })
                .ToList();
            unitContoursDict[mtext.Value] = new List<List<double[]>> { points };
        }

        // --- backgroundContours ---
        var backgroundContours = bgContours
            .Select(p => p.Vertices
                .Select(v => new double[] { v.Location.X, v.Location.Y })
                .ToList())
            .ToList<List<double[]>>();

        // --- walkingPaths ---
        var walkingPaths = ExportWalkways(walkwayNodes, walkwayEdges);

        var mapData = new LegacyMapData
        {
            StoreName          = storeName,
            StoreNote          = string.IsNullOrEmpty(storeNote) ? null : storeNote,
            DxfObjects         = dxfObjects.Count > 0 ? dxfObjects : null,
            UnitContours       = unitContoursDict.Count > 0 ? unitContoursDict : null,
            BackgroundContours = backgroundContours.Count > 0 ? backgroundContours : null,
            WalkingPaths       = walkingPaths,
        };

        return JsonSerializer.Serialize(mapData, JsonOptions);
    }

    // =========================================================================
    // Import helpers
    // =========================================================================

    private static Line? ImportLine(LegacyDxfObject obj, Layer layer)
    {
        if (obj.Start == null || obj.Start.Length < 2 ||
            obj.End   == null || obj.End.Length   < 2) return null;

        return new Line
        {
            StartPoint = new XYZ(obj.Start[0], obj.Start[1], 0),
            EndPoint   = new XYZ(obj.End[0],   obj.End[1],   0),
            Layer      = layer,
        };
    }

    private static MText? ImportMText(LegacyDxfObject obj, Layer layer)
    {
        if (obj.Pos == null || obj.Pos.Length < 2 || obj.Text == null) return null;

        var mtext = new MText
        {
            InsertPoint = new XYZ(obj.Pos[0], obj.Pos[1], 0),
            Value       = obj.Text,
            Layer       = layer,
        };

        if (obj.FormatOptions?.Anchor is "w" or "W")
            mtext.AttachmentPoint = AttachmentPointType.MiddleLeft;
        else
            mtext.AttachmentPoint = AttachmentPointType.MiddleCenter;

        return mtext;
    }

    private static LwPolyline? ImportLwPolyline(LegacyDxfObject obj, Layer layer, bool closed)
        => PointsToLwPolyline(obj.Points, layer, closed);

    private static LwPolyline? ImportPointsAsPolyline(
        IList<double[]>? points, Layer layer, bool closed)
        => PointsToLwPolyline(points, layer, closed);

    private static Arc? ImportArc(LegacyDxfObject obj, Layer layer)
    {
        if (obj.Center == null || obj.Center.Length < 2 ||
            obj.Radius  == null || obj.Sangle == null || obj.Eangle == null) return null;

        return new Arc
        {
            Center      = new XYZ(obj.Center[0], obj.Center[1], 0),
            Radius      = obj.Radius.Value,
            StartAngle  = obj.Sangle.Value,
            EndAngle    = obj.Eangle.Value,
            Layer       = layer,
        };
    }

    private static LwPolyline? PointsToLwPolyline(
        IList<double[]>? points, Layer layer, bool closed)
    {
        if (points == null || points.Count < 2) return null;

        var poly = new LwPolyline { IsClosed = closed, Layer = layer };
        foreach (var p in points)
        {
            if (p == null || p.Length < 2) continue;
            var v = new LwPolyline.Vertex { Location = new XY(p[0], p[1]) };
            poly.Vertices.Add(v);
        }

        return poly.Vertices.Count >= 2 ? poly : null;
    }

    private static void ImportWalkways(
        LegacyWalkingPaths walkingPaths, BlockRecord modelSpace, Layer layer)
    {
        if (walkingPaths.Points == null) return;

        var entranceIds = walkingPaths.EntryPoints != null
            ? new HashSet<int>(walkingPaths.EntryPoints)
            : new HashSet<int>();

        var nodeById = new Dictionary<int, XYZ>();
        foreach (var pt in walkingPaths.Points)
        {
            var pos = new XYZ(pt.Xpos, pt.Ypos, 0);
            nodeById[pt.Id] = pos;

            bool isEntrance = entranceIds.Contains(pt.Id);
            modelSpace.Entities.Add(new Circle
            {
                Center = pos,
                Radius = 0.3,
                Layer  = layer,
                Color  = new Color(isEntrance ? (short)3 : (short)5), // green = entrance, blue = regular
            });
        }

        if (walkingPaths.Neighbours == null) return;

        // Deduplicate undirected edges.
        var addedEdges = new HashSet<(int, int)>();
        foreach (var n in walkingPaths.Neighbours)
        {
            if (!nodeById.TryGetValue(n.Id, out var start) || n.Neighbours == null)
                continue;

            foreach (var nid in n.Neighbours)
            {
                if (!nodeById.TryGetValue(nid, out var end)) continue;

                int a = Math.Min(n.Id, nid), b = Math.Max(n.Id, nid);
                if (!addedEdges.Add((a, b))) continue;

                modelSpace.Entities.Add(new Line
                {
                    StartPoint = start,
                    EndPoint   = end,
                    Layer      = layer,
                    Color      = new Color(5),
                });
            }
        }
    }

    // =========================================================================
    // Export helpers
    // =========================================================================

    private static LegacyDxfObject ExportLine(Line line) => new()
    {
        Type      = "LINE",
        LayerName = line.Layer?.Name,
        Start     = [(float)line.StartPoint.X, (float)line.StartPoint.Y],
        End       = [(float)line.EndPoint.X,   (float)line.EndPoint.Y],
    };

    private static LegacyDxfObject ExportArc(Arc arc) => new()
    {
        Type      = "ARC",
        LayerName = arc.Layer?.Name,
        Center    = [(float)arc.Center.X, (float)arc.Center.Y],
        Radius    = (float)arc.Radius,
        Sangle    = (float)arc.StartAngle,
        Eangle    = (float)arc.EndAngle,
    };

    private static LegacyDxfObject ExportMText(MText mtext)
    {
        bool isUnitNumber = mtext.Layer?.Name == CadDocumentModel.UnitNumbersLayerName;

        bool isLeft = mtext.AttachmentPoint is
            AttachmentPointType.MiddleLeft or
            AttachmentPointType.TopLeft    or
            AttachmentPointType.BottomLeft;

        return new LegacyDxfObject
        {
            Type          = isUnitNumber ? "UNIT_NUMBER" : "MTEXT",
            LayerName     = isUnitNumber ? null : mtext.Layer?.Name,
            Pos           = [(float)mtext.InsertPoint.X, (float)mtext.InsertPoint.Y],
            Text          = mtext.Value,
            FormatOptions = isLeft ? new LegacyFormatOptions { Anchor = "w" } : null,
        };
    }

    private static LegacyDxfObject ExportTextEntity(TextEntity text) => new()
    {
        Type      = "TEXT",
        LayerName = text.Layer?.Name,
        Pos       = [(float)text.InsertPoint.X, (float)text.InsertPoint.Y],
        Text      = text.Value,
    };

    private static LegacyDxfObject ExportLwPolyline(LwPolyline poly) => new()
    {
        Type      = poly.IsClosed ? "POLYLINE" : "LWPOLYLINE",
        LayerName = poly.Layer?.Name,
        Points    = poly.Vertices
            .Select(v => new double[] { v.Location.X, v.Location.Y })
            .ToList(),
    };

    private static LegacyDxfObject ExportPolyline2D(Polyline2D poly) => new()
    {
        Type      = poly.IsClosed ? "POLYLINE" : "LWPOLYLINE",
        LayerName = poly.Layer?.Name,
        Points    = poly.Vertices
            .Select(v => new double[] { v.Location.X, v.Location.Y })
            .ToList(),
    };

    private static LegacyWalkingPaths? ExportWalkways(List<Circle> nodes, List<Line> edges)
    {
        if (nodes.Count == 0) return null;

        var idByPos = new Dictionary<(double x, double y), int>();
        var points  = new List<LegacyWalkingPathPoint>();
        int id      = 1;

        var entrancePointIds = new List<int>();

        foreach (var node in nodes)
        {
            var key = (Math.Round(node.Center.X, 6), Math.Round(node.Center.Y, 6));
            if (idByPos.ContainsKey(key)) continue; // deduplicate overlapping nodes

            idByPos[key] = id;
            points.Add(new LegacyWalkingPathPoint
            {
                Id          = id,
                Xpos        = node.Center.X,
                Ypos        = node.Center.Y,
                IsMinorpoint = false,
            });

            if (node.Color.Index == 3) // green = entrance
                entrancePointIds.Add(id);

            id++;
        }

        // Build bidirectional adjacency from edges.
        var neighbourMap = new Dictionary<int, List<int>>();
        foreach (var edge in edges)
        {
            var sk = (Math.Round(edge.StartPoint.X, 6), Math.Round(edge.StartPoint.Y, 6));
            var ek = (Math.Round(edge.EndPoint.X,   6), Math.Round(edge.EndPoint.Y,   6));

            if (!idByPos.TryGetValue(sk, out int startId) ||
                !idByPos.TryGetValue(ek, out int endId)) continue;

            if (!neighbourMap.TryGetValue(startId, out var sn)) neighbourMap[startId] = sn = [];
            if (!sn.Contains(endId)) sn.Add(endId);

            if (!neighbourMap.TryGetValue(endId, out var en)) neighbourMap[endId] = en = [];
            if (!en.Contains(startId)) en.Add(startId);
        }

        var neighbours = neighbourMap
            .Select(kv => new LegacyWalkingPathNeighbour { Id = kv.Key, Neighbours = kv.Value })
            .ToList();

        return new LegacyWalkingPaths
        {
            CreationTime = DateTime.UtcNow.ToString("o"),
            Points       = points,
            Neighbours   = neighbours,
            EntryPoints  = entrancePointIds,
        };
    }

    private static bool IsPointInLwPolyline(double px, double py, LwPolyline polygon)
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
                inside = !inside;
        }

        return inside;
    }

    // =========================================================================
    // Legacy JSON data model (private)
    // Mirrors the MapData / DxfObject / WalkingPaths structure from the old tool.
    // =========================================================================

    private sealed class LegacyMapData
    {
        public string? StoreName { get; set; }
        public string? StoreNote { get; set; }
        public List<LegacyDxfObject>? DxfObjects { get; set; }
        public Dictionary<string, List<List<double[]>>>? UnitContours { get; set; }
        public List<List<double[]>>? BackgroundContours { get; set; }
        public LegacyWalkingPaths? WalkingPaths { get; set; }
        // pathsToUnits is pre-computed data only; ignored on import, omitted on export.
        public List<Dictionary<string, List<double[]>>>? PathsToUnits { get; set; }
    }

    private sealed class LegacyDxfObject
    {
        public string? Type { get; set; }
        public string? LayerName { get; set; }
        public double[]? Start { get; set; }
        public double[]? End { get; set; }
        public List<double[]>? Points { get; set; }
        public double[]? Center { get; set; }
        public double? Radius { get; set; }
        public double? Sangle { get; set; }
        public double? Eangle { get; set; }
        public double[]? Pos { get; set; }
        public string? Text { get; set; }
        public LegacyFormatOptions? FormatOptions { get; set; }
    }

    private sealed class LegacyFormatOptions
    {
        public string? Anchor { get; set; }
    }

    private sealed class LegacyWalkingPaths
    {
        // Stored as string to tolerate the variety of date formats the old tool wrote.
        // Not used during import; written as ISO 8601 on export.
        public string? CreationTime { get; set; }
        public List<LegacyWalkingPathPoint>? Points { get; set; }
        public List<LegacyWalkingPathNeighbour>? Neighbours { get; set; }
        public List<int>? EntryPoints { get; set; }
    }

    private sealed class LegacyWalkingPathPoint
    {
        public int    Id          { get; set; }
        public double Xpos        { get; set; }
        public double Ypos        { get; set; }
        public bool   IsMinorpoint { get; set; }
    }

    private sealed class LegacyWalkingPathNeighbour
    {
        public int        Id         { get; set; }
        public List<int>? Neighbours { get; set; }
    }
}
