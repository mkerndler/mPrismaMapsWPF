using ACadSharp;
using mPrismaMapsWPF.Models;

namespace mPrismaMapsWPF.Services;

public interface IDocumentService
{
    CadDocumentModel CurrentDocument { get; }
    event EventHandler<DocumentLoadedEventArgs>? DocumentLoaded;
    event EventHandler? DocumentClosed;

    Task<IReadOnlyList<(Type EntityType, int Count)>> ScanEntityTypesAsync(
        string filePath,
        CancellationToken cancellationToken = default);
    Task<bool> OpenAsync(string filePath, IProgress<int>? progress = null, CancellationToken cancellationToken = default);
    Task<bool> OpenAsync(string filePath, ISet<Type>? excludedTypes, IProgress<int>? progress = null, CancellationToken cancellationToken = default);
    Task<bool> SaveAsync(string? filePath = null, CancellationToken cancellationToken = default);
    void Close();
    bool HasUnsavedChanges { get; }
}

public class DocumentLoadedEventArgs : EventArgs
{
    public string FilePath { get; }
    public int EntityCount { get; }
    public int LayerCount { get; }

    public DocumentLoadedEventArgs(string filePath, int entityCount, int layerCount)
    {
        FilePath = filePath;
        EntityCount = entityCount;
        LayerCount = layerCount;
    }
}
