using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;
using SkiaSharp;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class LineRenderer : IEntityRenderer
{
    public bool CanRender(Entity entity) => entity is Line;

    public void Render(SKCanvas canvas, Entity entity, RenderContext renderContext)
    {
        if (entity is not Line line)
            return;

        var start = renderContext.Transform(line.StartPoint.X, line.StartPoint.Y);
        var end = renderContext.Transform(line.EndPoint.X, line.EndPoint.Y);

        canvas.DrawLine(
            (float)start.X, (float)start.Y,
            (float)end.X, (float)end.Y,
            GetStrokePaint(line, renderContext));
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
