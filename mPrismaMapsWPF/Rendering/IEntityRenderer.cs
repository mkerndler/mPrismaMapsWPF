using System.Windows;
using System.Windows.Media;
using ACadSharp.Entities;
using WpfPoint = System.Windows.Point;

namespace mPrismaMapsWPF.Rendering;

public interface IEntityRenderer
{
    bool CanRender(Entity entity);
    void Render(DrawingContext context, Entity entity, RenderContext renderContext);
}

public class RenderContext
{
    public double Scale { get; set; } = 1.0;
    public WpfPoint Offset { get; set; }
    public Color DefaultColor { get; set; } = Colors.White;
    public double LineThickness { get; set; } = 1.0;
    public bool ShowSelection { get; set; } = true;
    public HashSet<ulong> SelectedHandles { get; } = new();
    public HashSet<string> HiddenLayers { get; } = new();
    public Rect? ViewportBounds { get; set; }

    public WpfPoint Transform(double x, double y)
    {
        return new WpfPoint(
            (x + Offset.X) * Scale,
            (-y + Offset.Y) * Scale
        );
    }

    public double TransformDistance(double distance)
    {
        return distance * Scale;
    }

    public bool IsLayerVisible(Entity entity)
    {
        if (entity.Layer == null)
            return true;

        return !HiddenLayers.Contains(entity.Layer.Name);
    }

    public bool IsSelected(Entity entity)
    {
        return SelectedHandles.Contains(entity.Handle);
    }

    public bool IsInViewport(Rect entityBounds)
    {
        if (!ViewportBounds.HasValue)
            return true;

        return ViewportBounds.Value.IntersectsWith(entityBounds) ||
               ViewportBounds.Value.Contains(entityBounds) ||
               entityBounds.Contains(ViewportBounds.Value);
    }
}
