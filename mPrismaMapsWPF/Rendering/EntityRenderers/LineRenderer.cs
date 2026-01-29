using System.Windows.Media;
using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class LineRenderer : IEntityRenderer
{
    public bool CanRender(Entity entity) => entity is Line;

    public void Render(DrawingContext context, Entity entity, RenderContext renderContext)
    {
        if (entity is not Line line)
            return;

        var start = renderContext.Transform(line.StartPoint.X, line.StartPoint.Y);
        var end = renderContext.Transform(line.EndPoint.X, line.EndPoint.Y);

        var pen = GetPen(line, renderContext);
        context.DrawLine(pen, start, end);
    }

    private static Pen GetPen(Line line, RenderContext renderContext)
    {
        Color color;
        double thickness = renderContext.LineThickness;

        if (renderContext.IsSelected(line))
        {
            color = Colors.Cyan;
            thickness *= 2;
        }
        else
        {
            color = ColorHelper.GetEntityColor(line, renderContext.DefaultColor);
        }

        return RenderCache.GetPen(color, thickness);
    }
}
