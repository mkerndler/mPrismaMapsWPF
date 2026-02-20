using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;
using SkiaSharp;

namespace mPrismaMapsWPF.Rendering.EntityRenderers;

public class PolylineRenderer : IEntityRenderer
{
    public bool CanRender(Entity entity) => entity is LwPolyline or Polyline2D;

    public void Render(SKCanvas canvas, Entity entity, RenderContext renderContext)
    {
        switch (entity)
        {
            case LwPolyline lwPolyline:
                RenderLwPolyline(canvas, lwPolyline, renderContext);
                break;
            case Polyline2D polyline2D:
                RenderPolyline2D(canvas, polyline2D, renderContext);
                break;
        }
    }

    private static void RenderLwPolyline(SKCanvas canvas, LwPolyline polyline, RenderContext renderContext)
    {
        var vertices = polyline.Vertices.ToList();
        if (vertices.Count < 2)
            return;

        bool shouldFill = polyline.IsClosed &&
            (polyline.Layer?.Name == CadDocumentModel.UnitAreasLayerName ||
             polyline.Layer?.Name == CadDocumentModel.BackgroundContoursLayerName);

        using var path = new SKPath();
        var first = renderContext.Transform(vertices[0].Location.X, vertices[0].Location.Y);
        path.MoveTo((float)first.X, (float)first.Y);

        for (int i = 1; i < vertices.Count; i++)
        {
            var pt = renderContext.Transform(vertices[i].Location.X, vertices[i].Location.Y);
            double bulge = vertices[i - 1].Bulge;

            if (Math.Abs(bulge) > 0.0001)
            {
                var prev = renderContext.Transform(vertices[i - 1].Location.X, vertices[i - 1].Location.Y);
                var s = new SKPoint((float)prev.X, (float)prev.Y);
                var e = new SKPoint((float)pt.X, (float)pt.Y);
                AddBulgeArc(path, s, e, bulge);
            }
            else
            {
                path.LineTo((float)pt.X, (float)pt.Y);
            }
        }

        if (polyline.IsClosed && vertices.Count > 2)
        {
            double lastBulge = vertices[^1].Bulge;
            if (Math.Abs(lastBulge) > 0.0001)
            {
                var last = renderContext.Transform(vertices[^1].Location.X, vertices[^1].Location.Y);
                var s = new SKPoint((float)last.X, (float)last.Y);
                var e = new SKPoint((float)first.X, (float)first.Y);
                AddBulgeArc(path, s, e, lastBulge);
            }
            else
            {
                path.LineTo((float)first.X, (float)first.Y);
            }
        }

        if (shouldFill)
        {
            var ec = ColorHelper.GetEntityColor(polyline, renderContext.DefaultColor).ToSKColor();
            canvas.DrawPath(path, SkiaRenderCache.GetFillPaint(ec.WithAlpha(60)));
        }

        canvas.DrawPath(path, GetStrokePaint(polyline, renderContext));
    }

    private static void AddBulgeArc(SKPath path, SKPoint s, SKPoint e, double bulge)
    {
        double dx = e.X - s.X, dy = e.Y - s.Y;
        double chord = Math.Sqrt(dx * dx + dy * dy);
        if (chord < 1e-10) return;

        double sag = Math.Abs(bulge) * chord / 2;
        double radius = (chord * chord / 4 + sag * sag) / (2 * sag);
        double midX = (s.X + e.X) / 2, midY = (s.Y + e.Y) / 2;
        double perpX = -dy / chord, perpY = dx / chord;
        double d = Math.Sqrt(Math.Max(0, radius * radius - chord * chord / 4));
        double sign = bulge > 0 ? -1 : 1;
        double cx = midX + sign * d * perpX, cy = midY + sign * d * perpY;

        double startDeg = Math.Atan2(s.Y - cy, s.X - cx) * 180 / Math.PI;
        double endDeg = Math.Atan2(e.Y - cy, e.X - cx) * 180 / Math.PI;
        double sweep = endDeg - startDeg;

        if (bulge > 0)
        {
            while (sweep > 0) sweep -= 360;
            if (sweep == 0) sweep = -360;
        }
        else
        {
            while (sweep < 0) sweep += 360;
            if (sweep == 0) sweep = 360;
        }

        float fr = (float)radius;
        var oval = new SKRect((float)(cx - fr), (float)(cy - fr), (float)(cx + fr), (float)(cy + fr));
        path.ArcTo(oval, (float)startDeg, (float)sweep, false);
    }

    private static void RenderPolyline2D(SKCanvas canvas, Polyline2D polyline, RenderContext renderContext)
    {
        var vertices = polyline.Vertices.ToList();
        if (vertices.Count < 2)
            return;

        using var path = new SKPath();
        var first = renderContext.Transform(vertices[0].Location.X, vertices[0].Location.Y);
        path.MoveTo((float)first.X, (float)first.Y);

        for (int i = 1; i < vertices.Count; i++)
        {
            var pt = renderContext.Transform(vertices[i].Location.X, vertices[i].Location.Y);
            path.LineTo((float)pt.X, (float)pt.Y);
        }

        if (polyline.IsClosed)
            path.LineTo((float)first.X, (float)first.Y);

        canvas.DrawPath(path, GetStrokePaint(polyline, renderContext));
    }

    private static SKPaint GetStrokePaint(Entity entity, RenderContext rc)
    {
        SKColor color = rc.IsSelected(entity) ? SKColors.Cyan
            : ColorHelper.GetEntityColor(entity, rc.DefaultColor).ToSKColor();
        float thickness = rc.IsSelected(entity)
            ? (float)rc.LineThickness * 2 : (float)rc.LineThickness;
        return SkiaRenderCache.GetStrokePaint(color, thickness);
    }
}
