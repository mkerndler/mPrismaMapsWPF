using ACadSharp.Tables;
using CommunityToolkit.Mvvm.ComponentModel;

namespace mPrismaMapsWPF.Models;

public partial class LayerModel : ObservableObject
{
    private readonly Layer _layer;

    public LayerModel(Layer layer)
    {
        _layer = layer;
        _isVisible = true;
        _isFrozen = layer.Flags.HasFlag(LayerFlags.Frozen);
    }

    public string Name => _layer.Name;
    public Layer Layer => _layer;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private bool _isFrozen;

    public System.Windows.Media.Color Color => AciToColor(_layer.Color.Index);

    private static System.Windows.Media.Color AciToColor(short aciIndex)
    {
        return aciIndex switch
        {
            1 => System.Windows.Media.Colors.Red,
            2 => System.Windows.Media.Colors.Yellow,
            3 => System.Windows.Media.Colors.Lime,
            4 => System.Windows.Media.Colors.Cyan,
            5 => System.Windows.Media.Colors.Blue,
            6 => System.Windows.Media.Colors.Magenta,
            7 => System.Windows.Media.Colors.White,
            8 => System.Windows.Media.Colors.Gray,
            9 => System.Windows.Media.Colors.LightGray,
            _ => System.Windows.Media.Colors.White
        };
    }
}
