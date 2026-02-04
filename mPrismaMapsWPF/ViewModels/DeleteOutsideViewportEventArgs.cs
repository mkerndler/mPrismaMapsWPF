using System.Windows;

namespace mPrismaMapsWPF.ViewModels;

public class DeleteOutsideViewportEventArgs : EventArgs
{
    public Rect ViewportBounds { get; set; }
    public bool Cancelled { get; set; }

    public DeleteOutsideViewportEventArgs()
    {
        ViewportBounds = Rect.Empty;
        Cancelled = true;
    }
}
