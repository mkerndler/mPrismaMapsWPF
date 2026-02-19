namespace mPrismaMapsWPF.ViewModels;

public class ScaleMapRequestedEventArgs : EventArgs
{
    public double ScaleFactor { get; set; } = 1.0;
    public bool Confirmed { get; set; }
}
