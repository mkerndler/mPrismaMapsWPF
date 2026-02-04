namespace mPrismaMapsWPF.ViewModels;

public class RotateViewEventArgs : EventArgs
{
    public double CurrentAngle { get; }

    public RotateViewEventArgs(double currentAngle)
    {
        CurrentAngle = currentAngle;
    }
}
