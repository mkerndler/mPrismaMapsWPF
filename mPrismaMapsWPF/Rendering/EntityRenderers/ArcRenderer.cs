using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;
using SkiaSharp;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class ArcRenderer : IEntityRenderer
{
    public bool CanRender(Entity entity) => entity is Arc;

    public void Render(SKCanvas canvas, Entity entity, RenderContext renderContext)
    {
        if (entity is not Arc arc)
            return;

        var center = renderContext.Transform(arc.Center.X, arc.Center.Y);
        float r = (float)renderContext.TransformDistance(arc.Radius);

        // ACadSharp exposes arc angles in degrees (DXF convention).
        // Negate to convert from CAD (CCW, Y-up) to Skia screen (CW, Y-down).
        float startDeg = -(float)arc.StartAngle;
        double cadSweep = arc.EndAngle - arc.StartAngle;
        if (cadSweep < 0) cadSweep += 360.0;
        float sweepDeg = -(float)cadSweep;

        var oval = new SKRect(
            (float)center.X - r, (float)center.Y - r,
            (float)center.X + r, (float)center.Y + r);

        using var path = new SKPath();
        path.ArcTo(oval, startDeg, sweepDeg, true);

        canvas.DrawPath(path, GetStrokePaint(arc, renderContext));
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
