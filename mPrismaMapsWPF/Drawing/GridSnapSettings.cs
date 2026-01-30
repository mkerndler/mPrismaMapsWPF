using CommunityToolkit.Mvvm.ComponentModel;

namespace mPrismaMapsWPF.Drawing;

public partial class GridSnapSettings : ObservableObject
{
    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private double _spacingX = 10.0;

    [ObservableProperty]
    private double _spacingY = 10.0;

    [ObservableProperty]
    private double _originX = 0.0;

    [ObservableProperty]
    private double _originY = 0.0;

    [ObservableProperty]
    private bool _showGrid = true;

    /// <summary>
    /// Calculates a sensible grid spacing based on drawing extents.
    /// Targets roughly 20-50 grid divisions across the view.
    /// </summary>
    public static double CalculateAutoGridSpacing(double maxDimension)
    {
        if (maxDimension <= 0)
            return 10.0;

        double rawSpacing = maxDimension / 30.0;

        // Round to a "nice" number (1, 2, 5, 10, 20, 50, 100, etc.)
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawSpacing)));
        double normalized = rawSpacing / magnitude;

        double niceSpacing = normalized switch
        {
            < 1.5 => 1,
            < 3.5 => 2,
            < 7.5 => 5,
            _ => 10
        };

        return niceSpacing * magnitude;
    }

    /// <summary>
    /// Sets both X and Y spacing to the same value.
    /// </summary>
    public void SetUniformSpacing(double spacing)
    {
        SpacingX = spacing;
        SpacingY = spacing;
    }
}
