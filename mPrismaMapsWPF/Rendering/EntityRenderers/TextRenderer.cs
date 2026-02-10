using System.Windows;
using System.Windows.Media;
using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class TextRenderer : IEntityRenderer
{
    public bool CanRender(Entity entity) => entity is TextEntity or MText;

    public void Render(DrawingContext context, Entity entity, RenderContext renderContext)
    {
        switch (entity)
        {
            case TextEntity text:
                RenderText(context, text, renderContext);
                break;
            case MText mtext:
                RenderMText(context, mtext, renderContext);
                break;
        }
    }

    private static void RenderText(DrawingContext context, TextEntity text, RenderContext renderContext)
    {
        if (string.IsNullOrEmpty(text.Value))
            return;

        var position = renderContext.Transform(text.InsertPoint.X, text.InsertPoint.Y);
        double fontSize = renderContext.TransformDistance(text.Height);

        if (fontSize < 1)
            fontSize = 1;

        Color color;
        if (renderContext.IsSelected(text))
        {
            color = Colors.Cyan;
        }
        else
        {
            color = ColorHelper.GetEntityColor(text, renderContext.DefaultColor);
        }

        var formattedText = RenderCache.GetFormattedText(
            text.Handle,
            text.Value,
            fontSize,
            color);

        // Offset upward so the baseline (bottom of text) aligns with the insert point
        var textPosition = new System.Windows.Point(position.X, position.Y - formattedText.Height);

        double rotation = text.Rotation * 180 / Math.PI;
        if (Math.Abs(rotation) > 0.01)
        {
            context.PushTransform(new RotateTransform(rotation, position.X, position.Y));
        }

        context.DrawText(formattedText, textPosition);

        if (Math.Abs(rotation) > 0.01)
        {
            context.Pop();
        }
    }

    private static void RenderMText(DrawingContext context, MText mtext, RenderContext renderContext)
    {
        if (string.IsNullOrEmpty(mtext.Value))
            return;

        var position = renderContext.Transform(mtext.InsertPoint.X, mtext.InsertPoint.Y);
        double fontSize = renderContext.TransformDistance(mtext.Height);

        if (fontSize < 1)
            fontSize = 1;

        Color color;
        if (renderContext.IsSelected(mtext))
        {
            color = Colors.Cyan;
        }
        else
        {
            color = ColorHelper.GetEntityColor(mtext, renderContext.DefaultColor);
        }

        string cleanText = StripMTextFormatting(mtext.Value);
        double maxWidth = mtext.RectangleWidth > 0 ? renderContext.TransformDistance(mtext.RectangleWidth) : 0;

        var formattedText = RenderCache.GetFormattedText(
            mtext.Handle,
            cleanText,
            fontSize,
            color,
            maxWidth);

        // Offset upward so the bottom of the text aligns with the insert point
        var textPosition = new System.Windows.Point(position.X, position.Y - formattedText.Height);

        double rotation = mtext.Rotation * 180 / Math.PI;
        if (Math.Abs(rotation) > 0.01)
        {
            context.PushTransform(new RotateTransform(rotation, position.X, position.Y));
        }

        context.DrawText(formattedText, textPosition);

        if (Math.Abs(rotation) > 0.01)
        {
            context.Pop();
        }
    }

    private static string StripMTextFormatting(string mtext)
    {
        var result = mtext;

        result = System.Text.RegularExpressions.Regex.Replace(result, @"\\[A-Za-z][^;]*;", "");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\{|\}", "");
        result = result.Replace("\\P", "\n");
        result = result.Replace("%%c", "Ø");
        result = result.Replace("%%d", "°");
        result = result.Replace("%%p", "±");

        return result;
    }
}
