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
        BoundingBoxHelper.InvalidateEntity(entity.Handle);
    }

    // Backward-compat overload used by ScaleMapCommand (uniform scale around origin).
    public static void ScaleEntity(Entity entity, double factor)
        => ScaleEntity(entity, 0, 0, factor, factor);

    public static void ScaleEntity(Entity entity, double pivotX, double pivotY, double scaleX, double scaleY)
    {
        switch (entity)
        {
            case Line line:
                var (lsx, lsy) = ScalePoint(line.StartPoint.X, line.StartPoint.Y, pivotX, pivotY, scaleX, scaleY);
                var (lex, ley) = ScalePoint(line.EndPoint.X, line.EndPoint.Y, pivotX, pivotY, scaleX, scaleY);
                line.StartPoint = new XYZ(lsx, lsy, line.StartPoint.Z);
                line.EndPoint = new XYZ(lex, ley, line.EndPoint.Z);
                break;

            case Arc arc:
                var (acx, acy) = ScalePoint(arc.Center.X, arc.Center.Y, pivotX, pivotY, scaleX, scaleY);
                arc.Center = new XYZ(acx, acy, arc.Center.Z);
                arc.Radius *= Math.Abs(scaleX);
                break;

            case Circle circle:
                var (ccx, ccy) = ScalePoint(circle.Center.X, circle.Center.Y, pivotX, pivotY, scaleX, scaleY);
                circle.Center = new XYZ(ccx, ccy, circle.Center.Z);
                circle.Radius *= Math.Abs(scaleX);
                break;

            case Ellipse ellipse:
                var (ecx, ecy) = ScalePoint(ellipse.Center.X, ellipse.Center.Y, pivotX, pivotY, scaleX, scaleY);
                ellipse.Center = new XYZ(ecx, ecy, ellipse.Center.Z);
                ellipse.MajorAxisEndPoint = new XYZ(
                    ellipse.MajorAxisEndPoint.X * scaleX,
                    ellipse.MajorAxisEndPoint.Y * scaleY,
                    ellipse.MajorAxisEndPoint.Z);
                break;

            case LwPolyline lwPolyline:
                for (int i = 0; i < lwPolyline.Vertices.Count; i++)
                {
                    var v = lwPolyline.Vertices[i];
                    var (vx, vy) = ScalePoint(v.Location.X, v.Location.Y, pivotX, pivotY, scaleX, scaleY);
                    lwPolyline.Vertices[i] = new LwPolyline.Vertex(new XY(vx, vy))
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
                    var (vx, vy) = ScalePoint(vertex.Location.X, vertex.Location.Y, pivotX, pivotY, scaleX, scaleY);
                    vertex.Location = new XYZ(vx, vy, vertex.Location.Z);
                }
                break;

            case MText mtext:
                var (mtx, mty) = ScalePoint(mtext.InsertPoint.X, mtext.InsertPoint.Y, pivotX, pivotY, scaleX, scaleY);
                mtext.InsertPoint = new XYZ(mtx, mty, mtext.InsertPoint.Z);
                mtext.Height *= Math.Abs(scaleY);
                if (mtext.RectangleWidth > 0)
                    mtext.RectangleWidth *= Math.Abs(scaleX);
                break;

            case TextEntity text:
                var (tx, ty) = ScalePoint(text.InsertPoint.X, text.InsertPoint.Y, pivotX, pivotY, scaleX, scaleY);
                text.InsertPoint = new XYZ(tx, ty, text.InsertPoint.Z);
                text.Height *= Math.Abs(scaleY);
                break;

            case Insert insert:
                var (ix, iy) = ScalePoint(insert.InsertPoint.X, insert.InsertPoint.Y, pivotX, pivotY, scaleX, scaleY);
                insert.InsertPoint = new XYZ(ix, iy, insert.InsertPoint.Z);
                insert.XScale *= scaleX;
                insert.YScale *= scaleY;
                break;

            case Point point:
                var (px, py) = ScalePoint(point.Location.X, point.Location.Y, pivotX, pivotY, scaleX, scaleY);
                point.Location = new XYZ(px, py, point.Location.Z);
                break;
        }
        BoundingBoxHelper.InvalidateEntity(entity.Handle);
    }

    public static void RotateEntity(Entity entity, double pivotX, double pivotY, double angleRadians)
    {
        double cos = Math.Cos(angleRadians);
        double sin = Math.Sin(angleRadians);
        double angleDegrees = angleRadians * (180.0 / Math.PI);

        switch (entity)
        {
            case Line line:
                var (lsx, lsy) = RotatePoint(line.StartPoint.X, line.StartPoint.Y, pivotX, pivotY, cos, sin);
                var (lex, ley) = RotatePoint(line.EndPoint.X, line.EndPoint.Y, pivotX, pivotY, cos, sin);
                line.StartPoint = new XYZ(lsx, lsy, line.StartPoint.Z);
                line.EndPoint = new XYZ(lex, ley, line.EndPoint.Z);
                break;

            case Arc arc:
                var (acx, acy) = RotatePoint(arc.Center.X, arc.Center.Y, pivotX, pivotY, cos, sin);
                arc.Center = new XYZ(acx, acy, arc.Center.Z);
                arc.StartAngle += angleRadians;
                arc.EndAngle += angleRadians;
                break;

            case Circle circle:
                var (ccx, ccy) = RotatePoint(circle.Center.X, circle.Center.Y, pivotX, pivotY, cos, sin);
                circle.Center = new XYZ(ccx, ccy, circle.Center.Z);
                break;

            case Ellipse ellipse:
                var (ecx, ecy) = RotatePoint(ellipse.Center.X, ellipse.Center.Y, pivotX, pivotY, cos, sin);
                ellipse.Center = new XYZ(ecx, ecy, ellipse.Center.Z);
                double mx = ellipse.MajorAxisEndPoint.X;
                double my = ellipse.MajorAxisEndPoint.Y;
                ellipse.MajorAxisEndPoint = new XYZ(
                    mx * cos - my * sin,
                    mx * sin + my * cos,
                    ellipse.MajorAxisEndPoint.Z);
                break;

            case LwPolyline lwPolyline:
                for (int i = 0; i < lwPolyline.Vertices.Count; i++)
                {
                    var v = lwPolyline.Vertices[i];
                    var (vx, vy) = RotatePoint(v.Location.X, v.Location.Y, pivotX, pivotY, cos, sin);
                    lwPolyline.Vertices[i] = new LwPolyline.Vertex(new XY(vx, vy))
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
                    var (vx, vy) = RotatePoint(vertex.Location.X, vertex.Location.Y, pivotX, pivotY, cos, sin);
                    vertex.Location = new XYZ(vx, vy, vertex.Location.Z);
                }
                break;

            case MText mtext:
                var (mtx, mty) = RotatePoint(mtext.InsertPoint.X, mtext.InsertPoint.Y, pivotX, pivotY, cos, sin);
                mtext.InsertPoint = new XYZ(mtx, mty, mtext.InsertPoint.Z);
                // MText.Rotation is read-only; position is rotated but text angle is not adjustable
                break;

            case TextEntity text:
                var (tx, ty) = RotatePoint(text.InsertPoint.X, text.InsertPoint.Y, pivotX, pivotY, cos, sin);
                text.InsertPoint = new XYZ(tx, ty, text.InsertPoint.Z);
                text.Rotation += angleDegrees;
                break;

            case Insert insert:
                var (ix, iy) = RotatePoint(insert.InsertPoint.X, insert.InsertPoint.Y, pivotX, pivotY, cos, sin);
                insert.InsertPoint = new XYZ(ix, iy, insert.InsertPoint.Z);
                insert.Rotation += angleDegrees;
                break;

            case Point point:
                var (px, py) = RotatePoint(point.Location.X, point.Location.Y, pivotX, pivotY, cos, sin);
                point.Location = new XYZ(px, py, point.Location.Z);
                break;
        }
        BoundingBoxHelper.InvalidateEntity(entity.Handle);
    }

    private static (double x, double y) ScalePoint(double x, double y, double pivotX, double pivotY, double scaleX, double scaleY)
    {
        return (pivotX + (x - pivotX) * scaleX, pivotY + (y - pivotY) * scaleY);
    }

    private static (double x, double y) RotatePoint(double x, double y, double pivotX, double pivotY, double cos, double sin)
    {
        double dx = x - pivotX;
        double dy = y - pivotY;
        return (pivotX + dx * cos - dy * sin, pivotY + dx * sin + dy * cos);
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
