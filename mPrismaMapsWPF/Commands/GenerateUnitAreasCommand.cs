using ACadSharp.Entities;
using ACadSharp.Tables;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

public class GenerateUnitAreasCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    private readonly HashSet<string> _hiddenLayers;
    private readonly List<Entity> _createdEntities = new();
    private readonly List<(Entity Entity, BlockRecord Owner)> _previousEntities = new();

    public string Description => $"Generate unit areas ({GeneratedCount} areas)";
    public int GeneratedCount { get; private set; }
    public int FailedCount { get; private set; }

    public GenerateUnitAreasCommand(CadDocumentModel document, HashSet<string> hiddenLayers)
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

        // Get or create "Unit Areas" layer
        var unitAreasLayer = _document.GetOrCreateUnitAreasLayer();
        if (unitAreasLayer == null)
            return;

        var modelSpace = _document.Document.BlockRecords
            .FirstOrDefault(b => b.Name == "*Model_Space");
        if (modelSpace == null)
            return;

        // Remove existing "Unit Areas" entities
        var existingAreaEntities = modelSpace.Entities
            .Where(e => e.Layer?.Name == CadDocumentModel.UnitAreasLayerName)
            .ToList();

        foreach (var entity in existingAreaEntities)
        {
            _previousEntities.Add((entity, modelSpace));
            modelSpace.Entities.Remove(entity);
        }

        // Collect MText on "Unit Numbers" layer
        var unitNumbers = _document.ModelSpaceEntities
            .OfType<MText>()
            .Where(m => m.Layer?.Name == CadDocumentModel.UnitNumbersLayerName)
            .ToList();

        if (unitNumbers.Count == 0)
            return;

        // Collect visible wall geometry (filter out hidden layers, text, inserts, points)
        var wallEntities = _document.ModelSpaceEntities
            .Where(e => e.Layer == null || !_hiddenLayers.Contains(e.Layer.Name))
            .Where(e => e is Line or Arc or Circle or LwPolyline or Polyline2D)
            .Where(e => e.Layer?.Name != CadDocumentModel.UnitAreasLayerName)
            .ToList();

        // Compute document extents
        var extents = _document.GetExtents();
        if (!extents.IsValid)
            return;

        double maxDim = Math.Max(extents.Width, extents.Height);
        double cellSize = maxDim / 2000.0;

        // Create grid and rasterize all wall geometry
        var grid = new FloodFillGrid(extents.MinX, extents.MinY, extents.MaxX, extents.MaxY, cellSize);

        foreach (var entity in wallEntities)
        {
            grid.RasterizeEntity(entity);
        }

        // For each unit number, flood fill and create polygon
        foreach (var mtext in unitNumbers)
        {
            var filled = grid.FloodFill(mtext.InsertPoint.X, mtext.InsertPoint.Y);
            if (filled == null)
            {
                FailedCount++;
                continue;
            }

            var contour = grid.ExtractContour(filled);
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

            // Create closed LwPolyline
            var polyline = new LwPolyline
            {
                IsClosed = true,
                Layer = unitAreasLayer
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

        // Remove created entities
        foreach (var entity in _createdEntities)
        {
            modelSpace.Entities.Remove(entity);
        }

        // Restore previous entities
        foreach (var (entity, _) in _previousEntities)
        {
            modelSpace.Entities.Add(entity);
        }

        _document.IsDirty = true;
    }
}
