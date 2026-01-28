using System.Windows.Media;
using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;
using Point = ACadSharp.Entities.Point;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class PointRenderer : IEntityRenderer
{
    private const double PointSize = 4.0;

    public bool CanRender(Entity entity) => entity is Point;

    public void Render(DrawingContext context, Entity entity, RenderContext renderContext)
    {
        if (entity is not Point point)
            return;

        var center = renderContext.Transform(point.Location.X, point.Location.Y);

        Color color;
        if (renderContext.IsSelected(point))
        {
            color = Colors.Cyan;
        }
        else
        {
            color = ColorHelper.GetEntityColor(point, renderContext.DefaultColor);
        }

        var brush = new SolidColorBrush(color);
        brush.Freeze();

        context.DrawEllipse(brush, null, center, PointSize / 2, PointSize / 2);

        var pen = new Pen(brush, 1);
        pen.Freeze();

        context.DrawLine(pen,
            new System.Windows.Point(center.X - PointSize, center.Y),
            new System.Windows.Point(center.X + PointSize, center.Y));
        context.DrawLine(pen,
            new System.Windows.Point(center.X, center.Y - PointSize),
            new System.Windows.Point(center.X, center.Y + PointSize));
    }
}
