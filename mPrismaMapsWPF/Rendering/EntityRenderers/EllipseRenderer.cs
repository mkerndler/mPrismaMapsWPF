using System.Windows.Media;
using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class EllipseRenderer : IEntityRenderer
{
    public bool CanRender(Entity entity) => entity is Ellipse;

    public void Render(DrawingContext context, Entity entity, RenderContext renderContext)
    {
        if (entity is not Ellipse ellipse)
            return;

        var pen = CreatePen(ellipse, renderContext);

        var center = renderContext.Transform(ellipse.Center.X, ellipse.Center.Y);

        double majorRadius = Math.Sqrt(
            ellipse.MajorAxisEndPoint.X * ellipse.MajorAxisEndPoint.X +
            ellipse.MajorAxisEndPoint.Y * ellipse.MajorAxisEndPoint.Y);
        double minorRadius = majorRadius * ellipse.RadiusRatio;

        double radiusX = renderContext.TransformDistance(majorRadius);
        double radiusY = renderContext.TransformDistance(minorRadius);

        double rotation = Math.Atan2(ellipse.MajorAxisEndPoint.Y, ellipse.MajorAxisEndPoint.X) * 180 / Math.PI;

        context.PushTransform(new RotateTransform(-rotation, center.X, center.Y));
        context.DrawEllipse(null, pen, center, radiusX, radiusY);
        context.Pop();
    }

    private static Pen CreatePen(Ellipse ellipse, RenderContext renderContext)
    {
        Color color;
        double thickness = renderContext.LineThickness;

        if (renderContext.IsSelected(ellipse))
        {
            color = Colors.Cyan;
            thickness *= 2;
        }
        else
        {
            color = ColorHelper.GetEntityColor(ellipse, renderContext.DefaultColor);
        }

        var pen = new Pen(new SolidColorBrush(color), thickness);
        pen.Freeze();
        return pen;
    }
}
