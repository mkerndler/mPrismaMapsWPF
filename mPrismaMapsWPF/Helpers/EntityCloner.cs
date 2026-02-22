using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using CSMath;
using Point = ACadSharp.Entities.Point;

namespace mPrismaMapsWPF.Helpers;

/// <summary>
/// Creates property-by-property copies of CAD entities for cross-document transfer.
/// ACadSharp entities are owned by their originating document and cannot be added
/// directly to a different document, so this helper reconstructs them.
/// </summary>
public static class EntityCloner
{
    /// <summary>
    /// Clones a model-space entity, applying an optional XY offset to all coordinates.
    /// Returns null for unsupported entity types (a warning should be logged by the caller).
    /// </summary>
    public static Entity? Clone(Entity source, Layer targetLayer, double offsetX = 0, double offsetY = 0)
    {
        return source switch
        {
            Line line         => CloneLine(line, targetLayer, offsetX, offsetY),
            Arc arc           => CloneArc(arc, targetLayer, offsetX, offsetY),
            Circle circle     => CloneCircle(circle, targetLayer, offsetX, offsetY),
            LwPolyline lwpoly => CloneLwPolyline(lwpoly, targetLayer, offsetX, offsetY),
            Polyline2D poly2d => ClonePolyline2D(poly2d, targetLayer, offsetX, offsetY),
            MText mtext       => CloneMText(mtext, targetLayer, offsetX, offsetY),
            TextEntity text   => CloneTextEntity(text, targetLayer, offsetX, offsetY),
            Point point       => ClonePoint(point, targetLayer, offsetX, offsetY),
            Ellipse ellipse   => CloneEllipse(ellipse, targetLayer, offsetX, offsetY),
            // Insert is handled separately in MergeDocumentService (needs block-name remapping)
            _                 => null,
        };
    }

    private static Line CloneLine(Line src, Layer layer, double dx, double dy) =>
        new Line
        {
            StartPoint = new XYZ(src.StartPoint.X + dx, src.StartPoint.Y + dy, src.StartPoint.Z),
            EndPoint   = new XYZ(src.EndPoint.X   + dx, src.EndPoint.Y   + dy, src.EndPoint.Z),
            Layer      = layer,
            Color      = src.Color,
            LineWeight = src.LineWeight,
        };

    private static Arc CloneArc(Arc src, Layer layer, double dx, double dy) =>
        new Arc
        {
            Center     = new XYZ(src.Center.X + dx, src.Center.Y + dy, src.Center.Z),
            Radius     = src.Radius,
            StartAngle = src.StartAngle,
            EndAngle   = src.EndAngle,
            Layer      = layer,
            Color      = src.Color,
            LineWeight = src.LineWeight,
        };

    private static Circle CloneCircle(Circle src, Layer layer, double dx, double dy) =>
        new Circle
        {
            Center     = new XYZ(src.Center.X + dx, src.Center.Y + dy, src.Center.Z),
            Radius     = src.Radius,
            Layer      = layer,
            Color      = src.Color,
            LineWeight = src.LineWeight,
        };

    private static LwPolyline CloneLwPolyline(LwPolyline src, Layer layer, double dx, double dy)
    {
        var poly = new LwPolyline
        {
            IsClosed   = src.IsClosed,
            Layer      = layer,
            Color      = src.Color,
            LineWeight = src.LineWeight,
        };

        foreach (var v in src.Vertices)
        {
            poly.Vertices.Add(new LwPolyline.Vertex(new XY(v.Location.X + dx, v.Location.Y + dy))
            {
                Bulge      = v.Bulge,
                StartWidth = v.StartWidth,
                EndWidth   = v.EndWidth,
            });
        }

        return poly;
    }

    private static Polyline2D ClonePolyline2D(Polyline2D src, Layer layer, double dx, double dy)
    {
        var poly = new Polyline2D
        {
            IsClosed   = src.IsClosed,
            Layer      = layer,
            Color      = src.Color,
            LineWeight = src.LineWeight,
        };

        foreach (var v in src.Vertices)
        {
            var vertex = new Vertex2D
            {
                Location = new XYZ(v.Location.X + dx, v.Location.Y + dy, v.Location.Z),
                Bulge    = v.Bulge,
            };
            poly.Vertices.Add(vertex);
        }

        return poly;
    }

    private static MText CloneMText(MText src, Layer layer, double dx, double dy) =>
        new MText
        {
            InsertPoint     = new XYZ(src.InsertPoint.X + dx, src.InsertPoint.Y + dy, src.InsertPoint.Z),
            Value           = src.Value,
            Height          = src.Height,
            AttachmentPoint = src.AttachmentPoint,
            Layer           = layer,
            Color           = src.Color,
        };

    private static TextEntity CloneTextEntity(TextEntity src, Layer layer, double dx, double dy) =>
        new TextEntity
        {
            InsertPoint = new XYZ(src.InsertPoint.X + dx, src.InsertPoint.Y + dy, src.InsertPoint.Z),
            Value       = src.Value,
            Height      = src.Height,
            Rotation    = src.Rotation,
            Layer       = layer,
            Color       = src.Color,
        };

    private static Point ClonePoint(Point src, Layer layer, double dx, double dy) =>
        new Point
        {
            Location = new XYZ(src.Location.X + dx, src.Location.Y + dy, src.Location.Z),
            Layer    = layer,
            Color    = src.Color,
        };

    private static Ellipse CloneEllipse(Ellipse src, Layer layer, double dx, double dy) =>
        new Ellipse
        {
            Center           = new XYZ(src.Center.X + dx, src.Center.Y + dy, src.Center.Z),
            MajorAxisEndPoint = new XYZ(src.MajorAxisEndPoint.X, src.MajorAxisEndPoint.Y, src.MajorAxisEndPoint.Z),
            RadiusRatio      = src.RadiusRatio,
            StartParameter   = src.StartParameter,
            EndParameter     = src.EndParameter,
            Layer            = layer,
            Color            = src.Color,
        };
}
