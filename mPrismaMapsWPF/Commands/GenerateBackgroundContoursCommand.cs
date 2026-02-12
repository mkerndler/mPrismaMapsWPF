using ACadSharp.Entities;
using ACadSharp.Tables;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

public class GenerateBackgroundContoursCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    private readonly HashSet<string> _hiddenLayers;
    private readonly List<Entity> _createdEntities = new();
    private readonly List<(Entity Entity, BlockRecord Owner)> _previousEntities = new();

    private static readonly HashSet<string> AppGeneratedLayers = new()
    {
        CadDocumentModel.UserDrawingsLayerName,
        CadDocumentModel.UnitNumbersLayerName,
        CadDocumentModel.WalkwaysLayerName,
        CadDocumentModel.UnitAreasLayerName,
        CadDocumentModel.BackgroundContoursLayerName
    };

    public string Description => $"Generate background contours ({GeneratedCount} contours)";
    public int GeneratedCount { get; private set; }
    public int FailedCount { get; private set; }

    public GenerateBackgroundContoursCommand(CadDocumentModel document, HashSet<string> hiddenLayers)
    {
        _document = document;
        _hiddenLayers = hiddenLayers;
    }

    public void Execute()
    {
        if (_document.Document == null)
            return;

        _createdEntities.Clear();
        _previousEntities.Clear();
        GeneratedCount = 0;
        FailedCount = 0;

        var contoursLayer = _document.GetOrCreateBackgroundContoursLayer();
        if (contoursLayer == null)
            return;

        var modelSpace = _document.Document.BlockRecords
            .FirstOrDefault(b => b.Name == "*Model_Space");
        if (modelSpace == null)
            return;

        // Remove existing "Background Contours" entities
        var existingContourEntities = modelSpace.Entities
            .Where(e => e.Layer?.Name == CadDocumentModel.BackgroundContoursLayerName)
            .ToList();

        foreach (var entity in existingContourEntities)
        {
            _previousEntities.Add((entity, modelSpace));
            modelSpace.Entities.Remove(entity);
        }

        // Collect background geometry: visible Lines/Arcs/Circles/LwPolylines/Polyline2Ds
        // NOT on any app-generated layer
        var backgroundEntities = _document.ModelSpaceEntities
            .Where(e => e.Layer == null || !_hiddenLayers.Contains(e.Layer.Name))
            .Where(e => e is Line or Arc or Circle or LwPolyline or Polyline2D)
            .Where(e => e.Layer == null || !AppGeneratedLayers.Contains(e.Layer.Name))
            .ToList();

        if (backgroundEntities.Count == 0)
            return;

        // Compute document extents
        var extents = _document.GetExtents();
        if (!extents.IsValid)
            return;

        double maxDim = Math.Max(extents.Width, extents.Height);
        double cellSize = maxDim / 2000.0;

        // Create grid and rasterize all background geometry
        var grid = new FloodFillGrid(extents.MinX, extents.MinY, extents.MaxX, extents.MaxY, cellSize);

        foreach (var entity in backgroundEntities)
        {
            grid.RasterizeEntity(entity);
        }

        // Find connected components of wall cells
        var components = grid.FindWallComponents(minCellCount: 20);

        // For each component, extract contour and create polyline
        foreach (var componentMask in components)
        {
            var contour = grid.ExtractContour(componentMask);
            if (contour.Count < 3)
            {
                FailedCount++;
                continue;
            }

            var simplified = FloodFillGrid.SimplifyPolygon(contour, cellSize);
            if (simplified.Count < 3)
            {
                FailedCount++;
                continue;
            }

            var polyline = new LwPolyline
            {
                IsClosed = true,
                Layer = contoursLayer
            };

            foreach (var (x, y) in simplified)
            {
                polyline.Vertices.Add(new LwPolyline.Vertex(new CSMath.XY(x, y)));
            }

            modelSpace.Entities.Add(polyline);
            _createdEntities.Add(polyline);
            GeneratedCount++;
        }

        _document.IsDirty = true;
    }

    public void Undo()
    {
        if (_document.Document == null)
            return;

        var modelSpace = _document.Document.BlockRecords
            .FirstOrDefault(b => b.Name == "*Model_Space");
        if (modelSpace == null)
            return;

        foreach (var entity in _createdEntities)
        {
            modelSpace.Entities.Remove(entity);
        }

        foreach (var (entity, _) in _previousEntities)
        {
            modelSpace.Entities.Add(entity);
        }

        _document.IsDirty = true;
    }
}
