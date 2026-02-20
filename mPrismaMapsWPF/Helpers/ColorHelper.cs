using System.Windows.Media;
using ACadSharp.Entities;
using ACadSharp.Tables;
using SkiaSharp;
using WpfColor = System.Windows.Media.Color;

namespace mPrismaMapsWPF.Helpers;

public static class ColorHelper
{
    private static readonly WpfColor[] AciColors = InitializeAciColors();

    public static WpfColor GetEntityColor(Entity entity, WpfColor defaultColor)
    {
        var color = entity.Color;

        if (color.IsByLayer && entity.Layer != null)
        {
            return GetLayerColor(entity.Layer);
        }

        if (color.IsByBlock)
        {
            return defaultColor;
        }

        return AciToColor(color.Index);
    }

    public static WpfColor GetLayerColor(Layer layer)
    {
        return AciToColor(layer.Color.Index);
    }

    public static WpfColor AciToColor(short aciIndex)
    {
        if (aciIndex >= 0 && aciIndex < AciColors.Length)
        {
            return AciColors[aciIndex];
        }
        return Colors.White;
    }

    private static WpfColor[] InitializeAciColors()
    {
        var colors = new WpfColor[256];

        colors[0] = Colors.Black;       // ByBlock
        colors[1] = Colors.Red;
        colors[2] = Colors.Yellow;
        colors[3] = Colors.Lime;
        colors[4] = Colors.Cyan;
        colors[5] = Colors.Blue;
        colors[6] = Colors.Magenta;
        colors[7] = Colors.White;
        colors[8] = WpfColor.FromRgb(128, 128, 128);
        colors[9] = WpfColor.FromRgb(192, 192, 192);

        colors[10] = WpfColor.FromRgb(255, 0, 0);
        colors[11] = WpfColor.FromRgb(255, 127, 127);
        colors[12] = WpfColor.FromRgb(204, 0, 0);
        colors[13] = WpfColor.FromRgb(204, 102, 102);
        colors[14] = WpfColor.FromRgb(153, 0, 0);
        colors[15] = WpfColor.FromRgb(153, 76, 76);
        colors[16] = WpfColor.FromRgb(127, 0, 0);
        colors[17] = WpfColor.FromRgb(127, 63, 63);
        colors[18] = WpfColor.FromRgb(76, 0, 0);
        colors[19] = WpfColor.FromRgb(76, 38, 38);

        colors[20] = WpfColor.FromRgb(255, 63, 0);
        colors[21] = WpfColor.FromRgb(255, 159, 127);
        colors[22] = WpfColor.FromRgb(204, 51, 0);
        colors[23] = WpfColor.FromRgb(204, 127, 102);
        colors[24] = WpfColor.FromRgb(153, 38, 0);
        colors[25] = WpfColor.FromRgb(153, 95, 76);
        colors[26] = WpfColor.FromRgb(127, 31, 0);
        colors[27] = WpfColor.FromRgb(127, 79, 63);
        colors[28] = WpfColor.FromRgb(76, 19, 0);
        colors[29] = WpfColor.FromRgb(76, 47, 38);

        colors[30] = WpfColor.FromRgb(255, 127, 0);
        colors[31] = WpfColor.FromRgb(255, 191, 127);
        colors[32] = WpfColor.FromRgb(204, 102, 0);
        colors[33] = WpfColor.FromRgb(204, 153, 102);
        colors[34] = WpfColor.FromRgb(153, 76, 0);
        colors[35] = WpfColor.FromRgb(153, 114, 76);
        colors[36] = WpfColor.FromRgb(127, 63, 0);
        colors[37] = WpfColor.FromRgb(127, 95, 63);
        colors[38] = WpfColor.FromRgb(76, 38, 0);
        colors[39] = WpfColor.FromRgb(76, 57, 38);

        colors[40] = WpfColor.FromRgb(255, 191, 0);
        colors[41] = WpfColor.FromRgb(255, 223, 127);
        colors[42] = WpfColor.FromRgb(204, 153, 0);
        colors[43] = WpfColor.FromRgb(204, 178, 102);
        colors[44] = WpfColor.FromRgb(153, 114, 0);
        colors[45] = WpfColor.FromRgb(153, 133, 76);
        colors[46] = WpfColor.FromRgb(127, 95, 0);
        colors[47] = WpfColor.FromRgb(127, 111, 63);
        colors[48] = WpfColor.FromRgb(76, 57, 0);
        colors[49] = WpfColor.FromRgb(76, 66, 38);

        colors[50] = WpfColor.FromRgb(255, 255, 0);
        colors[51] = WpfColor.FromRgb(255, 255, 127);
        colors[52] = WpfColor.FromRgb(204, 204, 0);
        colors[53] = WpfColor.FromRgb(204, 204, 102);
        colors[54] = WpfColor.FromRgb(153, 153, 0);
        colors[55] = WpfColor.FromRgb(153, 153, 76);
        colors[56] = WpfColor.FromRgb(127, 127, 0);
        colors[57] = WpfColor.FromRgb(127, 127, 63);
        colors[58] = WpfColor.FromRgb(76, 76, 0);
        colors[59] = WpfColor.FromRgb(76, 76, 38);

        colors[60] = WpfColor.FromRgb(191, 255, 0);
        colors[61] = WpfColor.FromRgb(223, 255, 127);
        colors[62] = WpfColor.FromRgb(153, 204, 0);
        colors[63] = WpfColor.FromRgb(178, 204, 102);
        colors[64] = WpfColor.FromRgb(114, 153, 0);
        colors[65] = WpfColor.FromRgb(133, 153, 76);
        colors[66] = WpfColor.FromRgb(95, 127, 0);
        colors[67] = WpfColor.FromRgb(111, 127, 63);
        colors[68] = WpfColor.FromRgb(57, 76, 0);
        colors[69] = WpfColor.FromRgb(66, 76, 38);

        colors[70] = WpfColor.FromRgb(127, 255, 0);
        colors[71] = WpfColor.FromRgb(191, 255, 127);
        colors[72] = WpfColor.FromRgb(102, 204, 0);
        colors[73] = WpfColor.FromRgb(153, 204, 102);
        colors[74] = WpfColor.FromRgb(76, 153, 0);
        colors[75] = WpfColor.FromRgb(114, 153, 76);
        colors[76] = WpfColor.FromRgb(63, 127, 0);
        colors[77] = WpfColor.FromRgb(95, 127, 63);
        colors[78] = WpfColor.FromRgb(38, 76, 0);
        colors[79] = WpfColor.FromRgb(57, 76, 38);

        colors[80] = WpfColor.FromRgb(63, 255, 0);
        colors[81] = WpfColor.FromRgb(159, 255, 127);
        colors[82] = WpfColor.FromRgb(51, 204, 0);
        colors[83] = WpfColor.FromRgb(127, 204, 102);
        colors[84] = WpfColor.FromRgb(38, 153, 0);
        colors[85] = WpfColor.FromRgb(95, 153, 76);
        colors[86] = WpfColor.FromRgb(31, 127, 0);
        colors[87] = WpfColor.FromRgb(79, 127, 63);
        colors[88] = WpfColor.FromRgb(19, 76, 0);
        colors[89] = WpfColor.FromRgb(47, 76, 38);

        colors[90] = WpfColor.FromRgb(0, 255, 0);
        colors[91] = WpfColor.FromRgb(127, 255, 127);
        colors[92] = WpfColor.FromRgb(0, 204, 0);
        colors[93] = WpfColor.FromRgb(102, 204, 102);
        colors[94] = WpfColor.FromRgb(0, 153, 0);
        colors[95] = WpfColor.FromRgb(76, 153, 76);
        colors[96] = WpfColor.FromRgb(0, 127, 0);
        colors[97] = WpfColor.FromRgb(63, 127, 63);
        colors[98] = WpfColor.FromRgb(0, 76, 0);
        colors[99] = WpfColor.FromRgb(38, 76, 38);

        for (int i = 100; i < 256; i++)
        {
            colors[i] = Colors.White;
        }

        return colors;
    }

    public static Brush GetEntityBrush(Entity entity, WpfColor defaultColor)
    {
        var color = GetEntityColor(entity, defaultColor);
        return new SolidColorBrush(color);
    }

    public static Pen GetEntityPen(Entity entity, WpfColor defaultColor, double thickness = 1.0)
    {
        var brush = GetEntityBrush(entity, defaultColor);
        return new Pen(brush, thickness);
    }

    public static SKColor ToSKColor(this WpfColor c) => new SKColor(c.R, c.G, c.B, c.A);
}
