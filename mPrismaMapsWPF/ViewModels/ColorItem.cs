using System.Windows.Media;
using AcadColor = ACadSharp.Color;

namespace mPrismaMapsWPF.ViewModels;

/// <summary>
/// Represents an ACI (AutoCAD Color Index) color item for use in color pickers.
/// </summary>
public class ColorItem
{
    /// <summary>
    /// The ACI color index (0-255, plus special values).
    /// </summary>
    public short AciIndex { get; }

    /// <summary>
    /// The color name for display.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The WPF color for display.
    /// </summary>
    public System.Windows.Media.Color WpfColor { get; }

    /// <summary>
    /// The ACadSharp color value.
    /// </summary>
    public AcadColor AcadColor { get; }

    /// <summary>
    /// Whether this is the ByLayer special color.
    /// </summary>
    public bool IsByLayer => AciIndex == 256;

    /// <summary>
    /// Whether this is the ByBlock special color.
    /// </summary>
    public bool IsByBlock => AciIndex == 0;

    public ColorItem(short aciIndex)
    {
        AciIndex = aciIndex;
        Name = GetColorName(aciIndex);
        WpfColor = AciToWpfColor(aciIndex);
        AcadColor = aciIndex switch
        {
            256 => AcadColor.ByLayer,
            0 => AcadColor.ByBlock,
            _ => new AcadColor(aciIndex)
        };
    }

    private static string GetColorName(short aciIndex)
    {
        return aciIndex switch
        {
            0 => "ByBlock",
            256 => "ByLayer",
            1 => "Red (1)",
            2 => "Yellow (2)",
            3 => "Green (3)",
            4 => "Cyan (4)",
            5 => "Blue (5)",
            6 => "Magenta (6)",
            7 => "White (7)",
            8 => "Dark Gray (8)",
            9 => "Light Gray (9)",
            _ => $"Color {aciIndex}"
        };
    }

    private static System.Windows.Media.Color AciToWpfColor(short aciIndex)
    {
        return aciIndex switch
        {
            0 => Colors.Gray, // ByBlock placeholder
            256 => Colors.Gray, // ByLayer placeholder
            1 => Colors.Red,
            2 => Colors.Yellow,
            3 => Colors.Lime,
            4 => Colors.Cyan,
            5 => Colors.Blue,
            6 => Colors.Magenta,
            7 => Colors.White,
            8 => System.Windows.Media.Color.FromRgb(128, 128, 128),
            9 => System.Windows.Media.Color.FromRgb(192, 192, 192),
            10 => Colors.Red,
            11 => Colors.OrangeRed,
            12 => Colors.Orange,
            13 => Colors.Gold,
            14 => Colors.Yellow,
            15 => Colors.GreenYellow,
            20 => Colors.LimeGreen,
            30 => Colors.Green,
            40 => Colors.Teal,
            50 => Colors.Cyan,
            60 => Colors.DeepSkyBlue,
            70 => Colors.DodgerBlue,
            80 => Colors.Blue,
            90 => Colors.BlueViolet,
            100 => Colors.Purple,
            _ => GetExtendedAciColor(aciIndex)
        };
    }

    private static System.Windows.Media.Color GetExtendedAciColor(short aciIndex)
    {
        // Simplified approximation for extended ACI colors (10-255)
        // Real implementation would use a proper ACI color table
        if (aciIndex is >= 10 and <= 249)
        {
            // Create a gradient-based approximation
            int hue = (aciIndex - 10) * 360 / 240;
            return HsvToRgb(hue, 1.0, 1.0);
        }

        return Colors.White;
    }

    private static System.Windows.Media.Color HsvToRgb(int h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        double m = v - c;

        double r, g, b;

        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return System.Windows.Media.Color.FromRgb(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255));
    }

    /// <summary>
    /// Gets the standard color items for a color picker.
    /// </summary>
    public static IReadOnlyList<ColorItem> StandardColors { get; } = new List<ColorItem>
    {
        new(256), // ByLayer
        new(0),   // ByBlock
        new(1),   // Red
        new(2),   // Yellow
        new(3),   // Green
        new(4),   // Cyan
        new(5),   // Blue
        new(6),   // Magenta
        new(7),   // White
        new(8),   // Dark Gray
        new(9),   // Light Gray
    };

    public override string ToString() => Name;
}
