using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using CSMath;
using mPrismaMapsWPF.Models;

namespace mPrismaMapsWPF.Tests.TestHelpers;

public static class EntityFactory
{
    public static Line CreateLine(double x1, double y1, double x2, double y2, Layer? layer = null)
    {
        var line = new Line
        {
            StartPoint = new XYZ(x1, y1, 0),
            EndPoint = new XYZ(x2, y2, 0)
        };
        if (layer != null) line.Layer = layer;
        return line;
    }

    public static Circle CreateCircle(double cx, double cy, double radius, Layer? layer = null)
    {
        var circle = new Circle
        {
            Center = new XYZ(cx, cy, 0),
            Radius = radius
        };
        if (layer != null) circle.Layer = layer;
        return circle;
    }

    public static Arc CreateArc(double cx, double cy, double radius, double startAngle, double endAngle, Layer? layer = null)
    {
        var arc = new Arc
        {
            Center = new XYZ(cx, cy, 0),
            Radius = radius,
            StartAngle = startAngle,
            EndAngle = endAngle
        };
        if (layer != null) arc.Layer = layer;
        return arc;
    }

    public static LwPolyline CreateLwPolyline(params (double x, double y)[] points)
    {
        var polyline = new LwPolyline();
        foreach (var (x, y) in points)
        {
            polyline.Vertices.Add(new LwPolyline.Vertex(new XY(x, y)));
        }
        return polyline;
    }

    public static MText CreateMText(double x, double y, string value, Layer? layer = null)
    {
        var mtext = new MText
        {
            InsertPoint = new XYZ(x, y, 0),
            Value = value,
            Height = 2.5
        };
        if (layer != null) mtext.Layer = layer;
        return mtext;
    }

    public static Point CreatePoint(double x, double y)
    {
        return new Point
        {
            Location = new XYZ(x, y, 0)
        };
    }

    public static CadDocument CreateDocument()
    {
        return new CadDocument();
    }

    public static CadDocumentModel CreateDocumentModel()
    {
        var model = new CadDocumentModel();
        var doc = new CadDocument();
        model.Load(doc, "test.dwg");
        return model;
    }

    public static EntityModel CreateEntityModel(Entity entity)
    {
        return new EntityModel(entity);
    }
}
