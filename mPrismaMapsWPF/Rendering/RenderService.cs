using System.Windows.Media;
using ACadSharp.Entities;
using mPrismaMapsWPF.Rendering.EntityRenderers;

namespace mPrismaMapsWPF.Rendering;

public class RenderService
{
    private readonly List<IEntityRenderer> _renderers;

    public RenderService()
    {
        _renderers = new List<IEntityRenderer>
        {
            new LineRenderer(),
            new CircleRenderer(),
            new ArcRenderer(),
            new PolylineRenderer(),
            new TextRenderer(),
            new EllipseRenderer(),
            new PointRenderer(),
            new InsertRenderer(this)
        };
    }

    public void RenderEntities(DrawingContext context, IEnumerable<Entity> entities, RenderContext renderContext)
    {
        foreach (var entity in entities)
        {
            if (!renderContext.IsLayerVisible(entity))
                continue;

            RenderEntity(context, entity, renderContext);
        }
    }

    public void RenderEntity(DrawingContext context, Entity entity, RenderContext renderContext)
    {
        foreach (var renderer in _renderers)
        {
            if (renderer.CanRender(entity))
            {
                renderer.Render(context, entity, renderContext);
                return;
            }
        }
    }
}
