using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;
using SkiaSharp;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class CircleRenderer : IEntityRenderer
{
    private const float WalkwayNodeMinRadius = 4f;
    private const float WalkwayNodeMaxRadius = 20f;

    public bool CanRender(Entity entity) => entity is Circle and not Arc;

    public void Render(SKCanvas canvas, Entity entity, RenderContext renderContext)
    {
        if (entity is not Circle circle)
            return;

        var center = renderContext.Transform(circle.Center.X, circle.Center.Y);
        float radius = (float)renderContext.TransformDistance(circle.Radius);

        // Clamp walkway node circles to a fixed screen-size range so they remain
        // visible and not oversized regardless of zoom level.
        if (circle is not Arc && circle.Layer?.Name == CadDocumentModel.WalkwaysLayerName)
            radius = Math.Clamp(radius, WalkwayNodeMinRadius, WalkwayNodeMaxRadius);

        canvas.DrawCircle((float)center.X, (float)center.Y, radius, GetStrokePaint(circle, renderContext));
    }

    private static SKPaint GetStrokePaint(Entity entity, RenderContext rc)
    {
        SKColor color = rc.IsSelected(entity) ? SKColors.Cyan
            : ColorHelper.GetEntityColor(entity, rc.DefaultColor).ToSKColor();
        float thickness = rc.IsSelected(entity)
            ? (float)rc.LineThickness * 2 : (float)rc.LineThickness;
        return SkiaRenderCache.GetStrokePaint(color, thickness);
    }
}
