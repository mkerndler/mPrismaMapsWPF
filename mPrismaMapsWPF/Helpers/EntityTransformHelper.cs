using ACadSharp.Entities;
using CSMath;
using Microsoft.Extensions.Logging;
using Point = ACadSharp.Entities.Point;

namespace mPrismaMapsWPF.Helpers;

public static class EntityTransformHelper
{
    public static void TranslateEntity(Entity entity, double dx, double dy)
    {
        switch (entity)
        {
            case Line line:
                line.StartPoint = new XYZ(line.StartPoint.X + dx, line.StartPoint.Y + dy, line.StartPoint.Z);
                line.EndPoint = new XYZ(line.EndPoint.X + dx, line.EndPoint.Y + dy, line.EndPoint.Z);
                break;

            case Arc arc:
                arc.Center = new XYZ(arc.Center.X + dx, arc.Center.Y + dy, arc.Center.Z);
                break;

            case Circle circle:
                circle.Center = new XYZ(circle.Center.X + dx, circle.Center.Y + dy, circle.Center.Z);
                break;

            case Ellipse ellipse:
                ellipse.Center = new XYZ(ellipse.Center.X + dx, ellipse.Center.Y + dy, ellipse.Center.Z);
                break;

            case LwPolyline lwPolyline:
                for (int i = 0; i < lwPolyline.Vertices.Count; i++)
                {
                    var v = lwPolyline.Vertices[i];
                    lwPolyline.Vertices[i] = new LwPolyline.Vertex(
                        new XY(v.Location.X + dx, v.Location.Y + dy))
                    {
                        Bulge = v.Bulge,
                        StartWidth = v.StartWidth,
                        EndWidth = v.EndWidth
                    };
                }
                break;

            case Polyline2D polyline2D:
                foreach (var vertex in polyline2D.Vertices)
                {
                    vertex.Location = new XYZ(
                        vertex.Location.X + dx,
                        vertex.Location.Y + dy,
                        vertex.Location.Z);
                }
                break;

            case MText mtext:
                mtext.InsertPoint = new XYZ(mtext.InsertPoint.X + dx, mtext.InsertPoint.Y + dy, mtext.InsertPoint.Z);
                break;

            case TextEntity text:
                text.InsertPoint = new XYZ(text.InsertPoint.X + dx, text.InsertPoint.Y + dy, text.InsertPoint.Z);
                break;

            case Insert insert:
                insert.InsertPoint = new XYZ(insert.InsertPoint.X + dx, insert.InsertPoint.Y + dy, insert.InsertPoint.Z);
                break;

            case Point point:
                point.Location = new XYZ(point.Location.X + dx, point.Location.Y + dy, point.Location.Z);
                break;

            default:
                // Unsupported entity type - no-op
                break;
        }
    }

    public static Entity? CloneEntity(Entity entity)
    {
        switch (entity)
        {
            case Line line:
                return new Line
                {
                    StartPoint = line.StartPoint,
                    EndPoint = line.EndPoint,
                    Layer = line.Layer,
                    Color = line.Color
                };

            case Arc arc:
                return new Arc
                {
                    Center = arc.Center,
                    Radius = arc.Radius,
                    StartAngle = arc.StartAngle,
                    EndAngle = arc.EndAngle,
                    Layer = arc.Layer,
                    Color = arc.Color
                };

            case Circle circle:
                return new Circle
                {
                    Center = circle.Center,
                    Radius = circle.Radius,
                    Layer = circle.Layer,
                    Color = circle.Color
                };

            case Ellipse ellipse:
                return new Ellipse
                {
                    Center = ellipse.Center,
                    MajorAxisEndPoint = ellipse.MajorAxisEndPoint,
                    RadiusRatio = ellipse.RadiusRatio,
                    StartParameter = ellipse.StartParameter,
                    EndParameter = ellipse.EndParameter,
                    Layer = ellipse.Layer,
                    Color = ellipse.Color
                };

            case LwPolyline lwPolyline:
                var clonedPoly = new LwPolyline
                {
                    IsClosed = lwPolyline.IsClosed,
                    Layer = lwPolyline.Layer,
                    Color = lwPolyline.Color
                };
                foreach (var v in lwPolyline.Vertices)
                {
                    clonedPoly.Vertices.Add(new LwPolyline.Vertex(new XY(v.Location.X, v.Location.Y))
                    {
                        Bulge = v.Bulge,
                        StartWidth = v.StartWidth,
                        EndWidth = v.EndWidth
                    });
                }
                return clonedPoly;

            case MText mtext:
                return new MText
                {
                    InsertPoint = mtext.InsertPoint,
                    Value = mtext.Value,
                    Height = mtext.Height,
                    RectangleWidth = mtext.RectangleWidth,
                    Layer = mtext.Layer,
                    Color = mtext.Color
                };

            case TextEntity text:
                return new TextEntity
                {
                    InsertPoint = text.InsertPoint,
                    Value = text.Value,
                    Height = text.Height,
                    Rotation = text.Rotation,
                    Layer = text.Layer,
                    Color = text.Color
                };

            case Insert insert when insert.Block != null:
                var clonedInsert = new Insert(insert.Block)
                {
                    InsertPoint = insert.InsertPoint,
                    XScale = insert.XScale,
                    YScale = insert.YScale,
                    ZScale = insert.ZScale,
                    Rotation = insert.Rotation,
                    Layer = insert.Layer,
                    Color = insert.Color
                };
                return clonedInsert;

            case Point point:
                return new Point
                {
                    Location = point.Location,
                    Layer = point.Layer,
                    Color = point.Color
                };

            default:
                return null;
        }
    }
}
