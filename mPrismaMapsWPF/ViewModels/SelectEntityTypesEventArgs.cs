namespace mPrismaMapsWPF.ViewModels;

public class SelectEntityTypesEventArgs : EventArgs
{
    public string FilePath { get; }
    public IReadOnlyList<(Type EntityType, int Count)> ScanResults { get; }

    public SelectEntityTypesEventArgs(string filePath, IReadOnlyList<(Type EntityType, int Count)> scanResults)
    {
        FilePath = filePath;
        ScanResults = scanResults;
    }
}
