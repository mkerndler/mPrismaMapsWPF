using System.Windows.Media;
using ACadSharp.Entities;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class InsertRenderer : IEntityRenderer
{
    private readonly RenderService _renderService;

    public InsertRenderer(RenderService renderService)
    {
        _renderService = renderService;
    }

    public bool CanRender(Entity entity) => entity is Insert;

    public void Render(DrawingContext context, Entity entity, RenderContext renderContext)
    {
        if (entity is not Insert insert)
            return;

        if (insert.Block == null)
            return;

        var position = renderContext.Transform(insert.InsertPoint.X, insert.InsertPoint.Y);

        context.PushTransform(new TranslateTransform(position.X, position.Y));

        context.PushTransform(new ScaleTransform(
            insert.XScale * renderContext.Scale,
            insert.YScale * renderContext.Scale));

        double rotation = insert.Rotation * 180 / Math.PI;
        if (Math.Abs(rotation) > 0.01)
        {
            context.PushTransform(new RotateTransform(-rotation));
        }

        context.PushTransform(new TranslateTransform(-position.X, -position.Y));

        Color blockColor = renderContext.IsSelected(insert) ? Colors.Cyan : renderContext.DefaultColor;

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
                _renderService.RenderEntity(context, blockEntity, blockContext);
            }
        }

        context.Pop();

        if (Math.Abs(rotation) > 0.01)
        {
            context.Pop();
        }

        context.Pop();
        context.Pop();
    }
}
