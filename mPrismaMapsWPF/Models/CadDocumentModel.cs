using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;

namespace mPrismaMapsWPF.Models;

public class CadDocumentModel
{
    public CadDocument? Document { get; private set; }
    public string? FilePath { get; private set; }
    public bool IsDirty { get; set; }

    public IEnumerable<Entity> ModelSpaceEntities =>
        Document?.Entities ?? Enumerable.Empty<Entity>();

    public IEnumerable<Layer> Layers =>
        Document?.Layers ?? Enumerable.Empty<Layer>();

    public IEnumerable<BlockRecord> Blocks =>
        Document?.BlockRecords ?? Enumerable.Empty<BlockRecord>();

    public const string UserDrawingsLayerName = "User Drawings";

    /// <summary>
    /// Gets or creates the "User Drawings" layer for user-created entities.
    /// </summary>
    public Layer? GetOrCreateUserDrawingsLayer()
    {
        if (Document == null)
            return null;

        // Check if layer already exists
        var existingLayer = Document.Layers.FirstOrDefault(l => l.Name == UserDrawingsLayerName);
        if (existingLayer != null)
            return existingLayer;

        // Create new layer
        var userLayer = new Layer(UserDrawingsLayerName)
        {
            Color = new ACadSharp.Color(6) // Magenta color for visibility
        };

        Document.Layers.Add(userLayer);
        IsDirty = true;

        return userLayer;
    }

    /// <summary>
    /// Creates a new document if none is loaded. Used for drawing without loading a file.
    /// </summary>
    public void EnsureDocumentExists()
    {
        if (Document == null)
        {
            Document = new CadDocument();
            FilePath = null;
            IsDirty = true;
        }
    }

    public void Load(CadDocument document, string filePath)
    {
        Document = document;
        FilePath = filePath;
        IsDirty = false;
    }

    public void Clear()
    {
        Document = null;
        FilePath = null;
        IsDirty = false;
    }

    public Extents GetExtents()
    {
        if (Document == null || !ModelSpaceEntities.Any())
            return new Extents();

        // Use PLINQ to compute bounds in parallel, then aggregate
        var bounds = ModelSpaceEntities
            .AsParallel()
            .Select(entity => Extents.GetEntityBoundsPublic(entity))
            .Where(b => b.HasValue)
            .Select(b => b!.Value)
            .ToList();

        if (bounds.Count == 0)
            return new Extents();

        return new Extents
        {
            MinX = bounds.Min(b => b.minX),
            MinY = bounds.Min(b => b.minY),
            MaxX = bounds.Max(b => b.maxX),
            MaxY = bounds.Max(b => b.maxY)
        };
    }
}

public class Extents
{
    public double MinX { get; set; } = double.MaxValue;
    public double MinY { get; set; } = double.MaxValue;
    public double MaxX { get; set; } = double.MinValue;
    public double MaxY { get; set; } = double.MinValue;

    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
    public double CenterX => (MinX + MaxX) / 2;
    public double CenterY => (MinY + MaxY) / 2;

    public bool IsValid => MinX < MaxX && MinY < MaxY;

    public void Expand(Entity entity)
    {
        var bbox = GetEntityBoundsPublic(entity);
        if (bbox.HasValue)
        {
            MinX = Math.Min(MinX, bbox.Value.minX);
            MinY = Math.Min(MinY, bbox.Value.minY);
            MaxX = Math.Max(MaxX, bbox.Value.maxX);
            MaxY = Math.Max(MaxY, bbox.Value.maxY);
        }
    }

    public static (double minX, double minY, double maxX, double maxY)? GetEntityBoundsPublic(Entity entity)
    {
        return entity switch
        {
            Line line => (
                Math.Min(line.StartPoint.X, line.EndPoint.X),
                Math.Min(line.StartPoint.Y, line.EndPoint.Y),
                Math.Max(line.StartPoint.X, line.EndPoint.X),
                Math.Max(line.StartPoint.Y, line.EndPoint.Y)
            ),
            Arc arc => GetArcBounds(arc),
            Circle circle => (
                circle.Center.X - circle.Radius,
                circle.Center.Y - circle.Radius,
                circle.Center.X + circle.Radius,
                circle.Center.Y + circle.Radius
            ),
            LwPolyline polyline => GetPolylineBounds(polyline),
            Polyline2D polyline2D => GetPolyline2DBounds(polyline2D),
            //Ellipse ellipse => GetEllipseBounds(ellipse),
            TextEntity text => (text.InsertPoint.X, text.InsertPoint.Y, text.InsertPoint.X, text.InsertPoint.Y),
            MText mtext => (mtext.InsertPoint.X, mtext.InsertPoint.Y, mtext.InsertPoint.X, mtext.InsertPoint.Y),
            Insert insert => (insert.InsertPoint.X, insert.InsertPoint.Y, insert.InsertPoint.X, insert.InsertPoint.Y),
            _ => null
        };
    }

    private static (double minX, double minY, double maxX, double maxY) GetArcBounds(Arc arc)
    {
        double minX = arc.Center.X - arc.Radius;
        double minY = arc.Center.Y - arc.Radius;
        double maxX = arc.Center.X + arc.Radius;
        double maxY = arc.Center.Y + arc.Radius;
        return (minX, minY, maxX, maxY);
    }

    private static (double minX, double minY, double maxX, double maxY)? GetPolylineBounds(LwPolyline polyline)
    {
        if (!polyline.Vertices.Any())
            return null;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var vertex in polyline.Vertices)
        {
            minX = Math.Min(minX, vertex.Location.X);
            minY = Math.Min(minY, vertex.Location.Y);
            maxX = Math.Max(maxX, vertex.Location.X);
            maxY = Math.Max(maxY, vertex.Location.Y);
        }

        return (minX, minY, maxX, maxY);
    }

    private static (double minX, double minY, double maxX, double maxY)? GetPolyline2DBounds(Polyline2D polyline)
    {
        if (!polyline.Vertices.Any())
            return null;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var vertex in polyline.Vertices)
        {
            minX = Math.Min(minX, vertex.Location.X);
            minY = Math.Min(minY, vertex.Location.Y);
            maxX = Math.Max(maxX, vertex.Location.X);
            maxY = Math.Max(maxY, vertex.Location.Y);
        }

        return (minX, minY, maxX, maxY);
    }

    //private static (double minX, double minY, double maxX, double maxY) GetEllipseBounds(Ellipse ellipse)
    //{
    //    double majorRadius = ellipse.RadiusA;
    //    double minorRadius = ellipse.RadiusB;
    //    double maxRadius = Math.Max(majorRadius, minorRadius);

    //    return (
    //        ellipse.Center.X - maxRadius,
    //        ellipse.Center.Y - maxRadius,
    //        ellipse.Center.X + maxRadius,
    //        ellipse.Center.Y + maxRadius
    //    );
    //}
}
