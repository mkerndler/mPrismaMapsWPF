using System.Windows.Media;
using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;
using WpfPoint = System.Windows.Point;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class PolylineRenderer : IEntityRenderer
{
    public bool CanRender(Entity entity) => entity is LwPolyline or Polyline2D;

    public void Render(DrawingContext context, Entity entity, RenderContext renderContext)
    {
        switch (entity)
        {
            case LwPolyline lwPolyline:
                RenderLwPolyline(context, lwPolyline, renderContext);
                break;
            case Polyline2D polyline2D:
                RenderPolyline2D(context, polyline2D, renderContext);
                break;
        }
    }

    private static void RenderLwPolyline(DrawingContext context, LwPolyline polyline, RenderContext renderContext)
    {
        var vertices = polyline.Vertices.ToList();
        if (vertices.Count < 2)
            return;

        var pen = GetPen(polyline, renderContext);
        bool shouldFill = polyline.IsClosed &&
            (polyline.Layer?.Name == CadDocumentModel.UnitAreasLayerName ||
             polyline.Layer?.Name == CadDocumentModel.BackgroundContoursLayerName);
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            var firstPoint = renderContext.Transform(vertices[0].Location.X, vertices[0].Location.Y);
            ctx.BeginFigure(firstPoint, shouldFill, polyline.IsClosed);

            for (int i = 1; i < vertices.Count; i++)
            {
                var point = renderContext.Transform(vertices[i].Location.X, vertices[i].Location.Y);

                double bulge = vertices[i - 1].Bulge;
                if (Math.Abs(bulge) > 0.0001)
                {
                    var prevPoint = renderContext.Transform(vertices[i - 1].Location.X, vertices[i - 1].Location.Y);
                    RenderBulgeArc(ctx, prevPoint, point, bulge, renderContext);
                }
                else
                {
                    ctx.LineTo(point, true, false);
                }
            }

            if (polyline.IsClosed && vertices.Count > 2)
            {
                double lastBulge = vertices[^1].Bulge;
                if (Math.Abs(lastBulge) > 0.0001)
                {
                    var lastPoint = renderContext.Transform(vertices[^1].Location.X, vertices[^1].Location.Y);
                    RenderBulgeArc(ctx, lastPoint, firstPoint, lastBulge, renderContext);
                }
            }
        }

        geometry.Freeze();

        Brush? fillBrush = null;
        if (shouldFill)
        {
            var color = ColorHelper.GetEntityColor(polyline, renderContext.DefaultColor);
            fillBrush = RenderCache.GetBrush(Color.FromArgb(60, color.R, color.G, color.B));
        }

        context.DrawGeometry(fillBrush, pen, geometry);
    }

    private static void RenderBulgeArc(StreamGeometryContext ctx, WpfPoint start, WpfPoint end, double bulge, RenderContext renderContext)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double chord = Math.Sqrt(dx * dx + dy * dy);

        double sagitta = Math.Abs(bulge) * chord / 2;
        double radius = (chord * chord / 4 + sagitta * sagitta) / (2 * sagitta);

        bool isLargeArc = Math.Abs(bulge) > 1;
        SweepDirection direction = bulge > 0 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;

        ctx.ArcTo(end, new System.Windows.Size(radius, radius), 0, isLargeArc, direction, true, false);
    }

    private static void RenderPolyline2D(DrawingContext context, Polyline2D polyline, RenderContext renderContext)
    {
        var vertices = polyline.Vertices.ToList();
        if (vertices.Count < 2)
            return;

        var pen = GetPen(polyline, renderContext);
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            var firstPoint = renderContext.Transform(vertices[0].Location.X, vertices[0].Location.Y);
            ctx.BeginFigure(firstPoint, false, polyline.IsClosed);

            for (int i = 1; i < vertices.Count; i++)
            {
                var point = renderContext.Transform(vertices[i].Location.X, vertices[i].Location.Y);
                ctx.LineTo(point, true, false);
            }
        }

        geometry.Freeze();
        context.DrawGeometry(null, pen, geometry);
    }

    private static Pen GetPen(Entity entity, RenderContext renderContext)
    {
        Color color;
        double thickness = renderContext.LineThickness;

        if (renderContext.IsSelected(entity))
        {
            color = Colors.Cyan;
            thickness *= 2;
        }
        else
        {
            color = ColorHelper.GetEntityColor(entity, renderContext.DefaultColor);
        }

        return RenderCache.GetPen(color, thickness);
    }
}
