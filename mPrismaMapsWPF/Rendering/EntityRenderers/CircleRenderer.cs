using System.Windows.Media;
using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class CircleRenderer : IEntityRenderer
{
    private const double WalkwayNodeMinRadius = 4.0;
    private const double WalkwayNodeMaxRadius = 20.0;

    public bool CanRender(Entity entity) => entity is Circle;

    public void Render(DrawingContext context, Entity entity, RenderContext renderContext)
    {
        if (entity is not Circle circle)
            return;

        var center = renderContext.Transform(circle.Center.X, circle.Center.Y);
        double radius = renderContext.TransformDistance(circle.Radius);

        // Clamp walkway node circles to a fixed screen-size range so they remain
        // visible and not oversized regardless of zoom level.
        if (circle is not Arc && circle.Layer?.Name == CadDocumentModel.WalkwaysLayerName)
            radius = Math.Clamp(radius, WalkwayNodeMinRadius, WalkwayNodeMaxRadius);

        var pen = GetPen(circle, renderContext);
        context.DrawEllipse(null, pen, center, radius, radius);
    }

    private static Pen GetPen(Circle circle, RenderContext renderContext)
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

        return RenderCache.GetPen(color, thickness);
    }
}
