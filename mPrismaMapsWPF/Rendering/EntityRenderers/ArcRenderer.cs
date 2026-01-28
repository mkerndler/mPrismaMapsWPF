using System.Windows;
using System.Windows.Media;
using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class ArcRenderer : IEntityRenderer
{
    public bool CanRender(Entity entity) => entity is Arc;

    public void Render(DrawingContext context, Entity entity, RenderContext renderContext)
    {
        if (entity is not Arc arc)
            return;

        var pen = CreatePen(arc, renderContext);
        var geometry = CreateArcGeometry(arc, renderContext);

        context.DrawGeometry(null, pen, geometry);
    }

    private static StreamGeometry CreateArcGeometry(Arc arc, RenderContext renderContext)
    {
        double radius = renderContext.TransformDistance(arc.Radius);

        double startX = arc.Center.X + arc.Radius * Math.Cos(arc.StartAngle);
        double startY = arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngle);
        double endX = arc.Center.X + arc.Radius * Math.Cos(arc.EndAngle);
        double endY = arc.Center.Y + arc.Radius * Math.Sin(arc.EndAngle);

        var start = renderContext.Transform(startX, startY);
        var end = renderContext.Transform(endX, endY);

        double sweepAngle = arc.EndAngle - arc.StartAngle;
        if (sweepAngle < 0)
            sweepAngle += 2 * Math.PI;

        bool isLargeArc = sweepAngle > Math.PI;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.ArcTo(
                end,
                new Size(radius, radius),
                0,
                isLargeArc,
                SweepDirection.Counterclockwise,
                true,
                false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Pen CreatePen(Arc arc, RenderContext renderContext)
    {
        Color color;
        double thickness = renderContext.LineThickness;

        if (renderContext.IsSelected(arc))
        {
            color = Colors.Cyan;
            thickness *= 2;
        }
        else
        {
            color = ColorHelper.GetEntityColor(arc, renderContext.DefaultColor);
        }

        var pen = new Pen(new SolidColorBrush(color), thickness);
        pen.Freeze();
        return pen;
    }
}
