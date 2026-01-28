using System.IO;
using ACadSharp;
using ACadSharp.IO;
using mPrismaMapsWPF.Models;

namespace mPrismaMapsWPF.Services;

public class DocumentService : IDocumentService
{
    public CadDocumentModel CurrentDocument { get; } = new();

    public event EventHandler<DocumentLoadedEventArgs>? DocumentLoaded;
    public event EventHandler? DocumentClosed;

    public bool HasUnsavedChanges => CurrentDocument.IsDirty;

    public async Task<bool> OpenAsync(string filePath, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await Task.Run(() =>
            {
                progress?.Report(10);

                CadDocument doc;
                string extension = Path.GetExtension(filePath).ToLowerInvariant();

                if (extension == ".dxf")
                {
                    using var reader = new DxfReader(filePath);
                    doc = reader.Read();
                }
                else
                {
                    using var reader = new DwgReader(filePath);
                    doc = reader.Read();
                }

                progress?.Report(100);
                return doc;
            }, cancellationToken);

            CurrentDocument.Load(document, filePath);

            int entityCount = CurrentDocument.ModelSpaceEntities.Count();
            int layerCount = CurrentDocument.Layers.Count();

            DocumentLoaded?.Invoke(this, new DocumentLoadedEventArgs(filePath, entityCount, layerCount));

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> SaveAsync(string? filePath = null, CancellationToken cancellationToken = default)
    {
        if (CurrentDocument.Document == null)
            return false;

        string targetPath = filePath ?? CurrentDocument.FilePath ?? throw new InvalidOperationException("No file path specified");

        try
        {
            await Task.Run(() =>
            {
                string extension = Path.GetExtension(targetPath).ToLowerInvariant();

                if (extension == ".dxf")
                {
                    using var writer = new DxfWriter(targetPath, CurrentDocument.Document, false);
                    writer.Write();
                }
                else
                {
                    using var writer = new DwgWriter(targetPath, CurrentDocument.Document);
                    writer.Write();
                }
            }, cancellationToken);

            CurrentDocument.IsDirty = false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Close()
    {
        CurrentDocument.Clear();
        DocumentClosed?.Invoke(this, EventArgs.Empty);
    }
}
