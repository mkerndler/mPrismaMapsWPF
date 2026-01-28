using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class TextRenderer : IEntityRenderer
{
    private static readonly Typeface DefaultTypeface = new("Arial");

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

        var brush = new SolidColorBrush(color);
        brush.Freeze();

        var formattedText = new FormattedText(
            text.Value,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            DefaultTypeface,
            fontSize,
            brush,
            1.0);

        context.PushTransform(new ScaleTransform(1, -1, position.X, position.Y));

        double rotation = text.Rotation * 180 / Math.PI;
        if (Math.Abs(rotation) > 0.01)
        {
            context.PushTransform(new RotateTransform(-rotation, position.X, position.Y));
        }

        context.DrawText(formattedText, position);

        if (Math.Abs(rotation) > 0.01)
        {
            context.Pop();
        }

        context.Pop();
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

        var brush = new SolidColorBrush(color);
        brush.Freeze();

        string cleanText = StripMTextFormatting(mtext.Value);

        var formattedText = new FormattedText(
            cleanText,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            DefaultTypeface,
            fontSize,
            brush,
            1.0);

        if (mtext.RectangleWidth > 0)
        {
            formattedText.MaxTextWidth = renderContext.TransformDistance(mtext.RectangleWidth);
        }

        context.PushTransform(new ScaleTransform(1, -1, position.X, position.Y));

        double rotation = mtext.Rotation * 180 / Math.PI;
        if (Math.Abs(rotation) > 0.01)
        {
            context.PushTransform(new RotateTransform(-rotation, position.X, position.Y));
        }

        context.DrawText(formattedText, position);

        if (Math.Abs(rotation) > 0.01)
        {
            context.Pop();
        }

        context.Pop();
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
