using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Rendering.EntityRenderers;
using SkiaSharp;

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

    public void RenderEntities(SKCanvas canvas, IEnumerable<Entity> entities, RenderContext renderContext)
    {
        // Two-pass rendering: Unit Areas and Background Contours first (underneath), then everything else
        // Pass 1: Render Unit Areas and Background Contours layer entities
        foreach (var entity in entities)
        {
            if (entity.Layer?.Name != CadDocumentModel.UnitAreasLayerName &&
                entity.Layer?.Name != CadDocumentModel.BackgroundContoursLayerName)
                continue;

            if (!renderContext.IsLayerVisible(entity))
                continue;

            if (renderContext.ViewportBounds.HasValue)
            {
                var bounds = BoundingBoxHelper.GetBounds(entity);
                if (bounds.HasValue && !renderContext.IsInViewport(bounds.Value))
                    continue;
            }

            RenderEntity(canvas, entity, renderContext);
        }

        // Pass 2: Render all other entities
        foreach (var entity in entities)
        {
            if (entity.Layer?.Name == CadDocumentModel.UnitAreasLayerName ||
                entity.Layer?.Name == CadDocumentModel.BackgroundContoursLayerName)
                continue;

            if (!renderContext.IsLayerVisible(entity))
                continue;

            if (renderContext.ViewportBounds.HasValue)
            {
                var bounds = BoundingBoxHelper.GetBounds(entity);
                if (bounds.HasValue && !renderContext.IsInViewport(bounds.Value))
                    continue;
            }

            RenderEntity(canvas, entity, renderContext);
        }
    }

    public void RenderEntity(SKCanvas canvas, Entity entity, RenderContext renderContext)
    {
        foreach (var renderer in _renderers)
        {
            if (renderer.CanRender(entity))
            {
                renderer.Render(canvas, entity, renderContext);
                return;
            }
        }
    }
}
