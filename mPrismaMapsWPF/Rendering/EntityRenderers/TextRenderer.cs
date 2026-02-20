using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;
using SkiaSharp;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class TextRenderer : IEntityRenderer
{
    public bool CanRender(Entity entity) => entity is TextEntity or MText;

    public void Render(SKCanvas canvas, Entity entity, RenderContext renderContext)
    {
        switch (entity)
        {
            case TextEntity text:
                RenderText(canvas, text, renderContext);
                break;
            case MText mtext:
                RenderMText(canvas, mtext, renderContext);
                break;
        }
    }

    private static void RenderText(SKCanvas canvas, TextEntity text, RenderContext renderContext)
    {
        if (string.IsNullOrEmpty(text.Value))
            return;

        var position = renderContext.Transform(text.InsertPoint.X, text.InsertPoint.Y);
        float fontSize = (float)renderContext.TransformDistance(text.Height);
        if (fontSize < 1) fontSize = 1;

        SKColor color = renderContext.IsSelected(text) ? SKColors.Cyan
            : ColorHelper.GetEntityColor(text, renderContext.DefaultColor).ToSKColor();

        double rotDeg = text.Rotation * 180 / Math.PI;

        using var font = SkiaRenderCache.MakeFont(fontSize);
        using var paint = new SKPaint { Color = color, IsAntialias = true };

        float x = (float)position.X;
        float y = (float)position.Y;

        if (Math.Abs(rotDeg) > 0.01)
        {
            canvas.Save();
            canvas.RotateDegrees(-(float)rotDeg, x, y);
        }

        canvas.DrawText(text.Value, x, y, font, paint);

        if (Math.Abs(rotDeg) > 0.01)
            canvas.Restore();
    }

    private static void RenderMText(SKCanvas canvas, MText mtext, RenderContext renderContext)
    {
        if (string.IsNullOrEmpty(mtext.Value))
            return;

        var position = renderContext.Transform(mtext.InsertPoint.X, mtext.InsertPoint.Y);
        float fontSize = (float)renderContext.TransformDistance(mtext.Height);

        if (mtext.Layer?.Name == CadDocumentModel.UnitNumbersLayerName)
            fontSize = Math.Max(fontSize, 8f);

        if (fontSize < 1) fontSize = 1;

        SKColor color = renderContext.IsSelected(mtext) ? SKColors.Cyan
            : ColorHelper.GetEntityColor(mtext, renderContext.DefaultColor).ToSKColor();

        string cleanText = StripMTextFormatting(mtext.Value);
        double rotDeg = mtext.Rotation * 180 / Math.PI;

        using var font = SkiaRenderCache.MakeFont(fontSize);
        using var paint = new SKPaint { Color = color, IsAntialias = true };

        float x = (float)position.X;
        float y = (float)position.Y;

        if (Math.Abs(rotDeg) > 0.01)
        {
            canvas.Save();
            canvas.RotateDegrees(-(float)rotDeg, x, y);
        }

        // Multi-line support: split on \n, advance by font.Spacing per line
        var lines = cleanText.Split('\n');
        float lineY = y;
        foreach (var line in lines)
        {
            canvas.DrawText(line, x, lineY, font, paint);
            lineY += font.Spacing;
        }

        if (Math.Abs(rotDeg) > 0.01)
            canvas.Restore();
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
