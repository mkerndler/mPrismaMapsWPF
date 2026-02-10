using ACadSharp.Entities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace mPrismaMapsWPF.Models;

public partial class EntityModel : ObservableObject
{
    public Entity Entity { get; }

    public EntityModel(Entity entity)
    {
        Entity = entity;
    }

    public string TypeName => Entity.GetType().Name;
    public string LayerName => Entity.Layer?.Name ?? "0";
    public ulong Handle => Entity.Handle;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isLocked;

    public string DisplayName => $"{TypeName} ({Handle:X})";

    /// <summary>
    /// Gets a Unicode icon representing the entity type.
    /// </summary>
    public string TypeIcon => GetTypeIcon();

    private string GetTypeIcon()
    {
        // Arc must come before Circle since Arc inherits from Circle
        return Entity switch
        {
            Line => "/",
            Arc => "◜",
            Circle => "○",
            LwPolyline => "∿",
            Polyline2D => "∿",
            Polyline3D => "∿",
            TextEntity => "A",
            MText => "A",
            Point => "•",
            Ellipse => "⬭",
            Spline => "~",
            Hatch => "▧",
            Insert => "⊞",
            _ when TypeName.Contains("Dimension") => "↔",
            _ => "◇"
        };
    }

    public string? GetProperty(string propertyName)
    {
        return propertyName switch
        {
            "Type" => TypeName,
            "Layer" => LayerName,
            "Handle" => Handle.ToString("X"),
            "Color" => GetColorDescription(),
            _ => GetEntitySpecificProperty(propertyName)
        };
    }

    private string GetColorDescription()
    {
        if (Entity.Color.IsByLayer)
            return "ByLayer";
        if (Entity.Color.IsByBlock)
            return "ByBlock";
        return $"ACI {Entity.Color.Index}";
    }

    private string? GetEntitySpecificProperty(string propertyName)
    {
        return Entity switch
        {
            Line line => propertyName switch
            {
                "StartX" => line.StartPoint.X.ToString("F4"),
                "StartY" => line.StartPoint.Y.ToString("F4"),
                "EndX" => line.EndPoint.X.ToString("F4"),
                "EndY" => line.EndPoint.Y.ToString("F4"),
                "Length" => GetLineLength(line).ToString("F4"),
                _ => null
            },
            Arc arc => propertyName switch
            {
                "CenterX" => arc.Center.X.ToString("F4"),
                "CenterY" => arc.Center.Y.ToString("F4"),
                "Radius" => arc.Radius.ToString("F4"),
                "StartAngle" => (arc.StartAngle * 180 / Math.PI).ToString("F2"),
                "EndAngle" => (arc.EndAngle * 180 / Math.PI).ToString("F2"),
                _ => null
            },
            Circle circle => propertyName switch
            {
                "CenterX" => circle.Center.X.ToString("F4"),
                "CenterY" => circle.Center.Y.ToString("F4"),
                "Radius" => circle.Radius.ToString("F4"),
                _ => null
            },
            TextEntity text => propertyName switch
            {
                "Text" => text.Value,
                "Height" => text.Height.ToString("F4"),
                "InsertX" => text.InsertPoint.X.ToString("F4"),
                "InsertY" => text.InsertPoint.Y.ToString("F4"),
                _ => null
            },
            _ => null
        };
    }

    private static double GetLineLength(Line line)
    {
        double dx = line.EndPoint.X - line.StartPoint.X;
        double dy = line.EndPoint.Y - line.StartPoint.Y;
        double dz = line.EndPoint.Z - line.StartPoint.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
