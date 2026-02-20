using ACadSharp.Entities;
using SkiaSharp;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class InsertRenderer : IEntityRenderer
{
    private readonly RenderService _renderService;

    public InsertRenderer(RenderService renderService)
    {
        _renderService = renderService;
    }

    public bool CanRender(Entity entity) => entity is Insert;

    public void Render(SKCanvas canvas, Entity entity, RenderContext renderContext)
    {
        if (entity is not Insert insert)
            return;

        if (insert.Block == null)
            return;

        var position = renderContext.Transform(insert.InsertPoint.X, insert.InsertPoint.Y);
        float px = (float)position.X;
        float py = (float)position.Y;

        double rotation = insert.Rotation * 180 / Math.PI;

        canvas.Save();

        // Build composed transform: translate to pos, scale, rotate, translate back
        var m = SKMatrix.CreateTranslation(px, py);
        m = SKMatrix.Concat(m, SKMatrix.CreateScale(
            (float)(insert.XScale * renderContext.Scale),
            (float)(insert.YScale * renderContext.Scale)));
        if (Math.Abs(rotation) > 0.01)
            m = SKMatrix.Concat(m, SKMatrix.CreateRotationDegrees(-(float)rotation));
        m = SKMatrix.Concat(m, SKMatrix.CreateTranslation(-px, -py));
        canvas.Concat(in m);

        var blockColor = renderContext.IsSelected(insert)
            ? System.Windows.Media.Colors.Cyan
            : renderContext.DefaultColor;

        var blockContext = new RenderContext
        {
            Scale = 1.0,
            Offset = new System.Windows.Point(
                insert.InsertPoint.X,
                -insert.InsertPoint.Y),
            DefaultColor = blockColor,
            LineThickness = renderContext.LineThickness,
            ShowSelection = renderContext.ShowSelection
        };

        if (renderContext.IsSelected(insert))
        {
            foreach (var blockEntity in insert.Block.Entities)
            {
                blockContext.SelectedHandles.Add(blockEntity.Handle);
            }
        }

        foreach (var blockEntity in insert.Block.Entities)
        {
            if (blockContext.IsLayerVisible(blockEntity))
            {
                _renderService.RenderEntity(canvas, blockEntity, blockContext);
            }
        }

        canvas.Restore();
    }
}
