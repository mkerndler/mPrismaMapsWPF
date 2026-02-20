using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;
using SkiaSharp;
using Point = ACadSharp.Entities.Point;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class PointRenderer : IEntityRenderer
{
    private const float PointSize = 4f;

    public bool CanRender(Entity entity) => entity is Point;

    public void Render(SKCanvas canvas, Entity entity, RenderContext renderContext)
    {
        if (entity is not Point point)
            return;

        var center = renderContext.Transform(point.Location.X, point.Location.Y);
        float cx = (float)center.X;
        float cy = (float)center.Y;

        SKColor color = renderContext.IsSelected(point) ? SKColors.Cyan
            : ColorHelper.GetEntityColor(point, renderContext.DefaultColor).ToSKColor();

        canvas.DrawCircle(cx, cy, PointSize / 2, SkiaRenderCache.GetFillPaint(color));

        var strokePaint = SkiaRenderCache.GetStrokePaint(color, 1f);
        canvas.DrawLine(cx - PointSize, cy, cx + PointSize, cy, strokePaint);
        canvas.DrawLine(cx, cy - PointSize, cx, cy + PointSize, strokePaint);
    }
}
