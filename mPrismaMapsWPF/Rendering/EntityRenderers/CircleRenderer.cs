using System.Windows.Media;
using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class CircleRenderer : IEntityRenderer
{
    public bool CanRender(Entity entity) => entity is Circle;

    public void Render(DrawingContext context, Entity entity, RenderContext renderContext)
    {
        if (entity is not Circle circle)
            return;

        var center = renderContext.Transform(circle.Center.X, circle.Center.Y);
        double radius = renderContext.TransformDistance(circle.Radius);

        var pen = CreatePen(circle, renderContext);
        context.DrawEllipse(null, pen, center, radius, radius);
    }

    private static Pen CreatePen(Circle circle, RenderContext renderContext)
    {
        Color color;
        double thickness = renderContext.LineThickness;

        if (renderContext.IsSelected(circle))
        {
            color = Colors.Cyan;
            thickness *= 2;
        }
        else
        {
            color = ColorHelper.GetEntityColor(circle, renderContext.DefaultColor);
        }

        var pen = new Pen(new SolidColorBrush(color), thickness);
        pen.Freeze();
        return pen;
    }
}
