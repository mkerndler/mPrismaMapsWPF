using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;
using SkiaSharp;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class EllipseRenderer : IEntityRenderer
{
    public bool CanRender(Entity entity) => entity is Ellipse;

    public void Render(SKCanvas canvas, Entity entity, RenderContext renderContext)
    {
        if (entity is not Ellipse ellipse)
            return;

        var center = renderContext.Transform(ellipse.Center.X, ellipse.Center.Y);

        double majorRadius = Math.Sqrt(
            ellipse.MajorAxisEndPoint.X * ellipse.MajorAxisEndPoint.X +
            ellipse.MajorAxisEndPoint.Y * ellipse.MajorAxisEndPoint.Y);
        double minorRadius = majorRadius * ellipse.RadiusRatio;

        float rx = (float)renderContext.TransformDistance(majorRadius);
        float ry = (float)renderContext.TransformDistance(minorRadius);

        double rotDeg = Math.Atan2(ellipse.MajorAxisEndPoint.Y, ellipse.MajorAxisEndPoint.X) * 180 / Math.PI;

        canvas.Save();
        canvas.RotateDegrees((float)-rotDeg, (float)center.X, (float)center.Y);
        canvas.DrawOval((float)center.X, (float)center.Y, rx, ry, GetStrokePaint(ellipse, renderContext));
        canvas.Restore();
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
