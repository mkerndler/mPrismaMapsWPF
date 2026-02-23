using System.IO;
using ACadSharp;
using ACadSharp.IO;
using ACadSharp.Tables;
using Microsoft.Extensions.Logging;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;

namespace mPrismaMapsWPF.Services;

public class DocumentService : IDocumentService
{
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(ILogger<DocumentService> logger)
    {
        _logger = logger;
    }

    public CadDocumentModel CurrentDocument { get; } = new();

    public event EventHandler<DocumentLoadedEventArgs>? DocumentLoaded;
    public event EventHandler? DocumentClosed;

    public bool HasUnsavedChanges => CurrentDocument.IsDirty;

    public async Task<IReadOnlyList<(Type EntityType, int Count)>> ScanEntityTypesAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scanning entity types in {FilePath}", filePath);

        return await Task.Run(() =>
        {
            CadDocument doc = ReadFile(filePath);

            // Use PLINQ for parallel grouping on large entity sets
            return doc.Entities
                .AsParallel()
                .WithCancellation(cancellationToken)
                .GroupBy(e => e.GetType())
                .Select(g => (g.Key, g.Count()))
                .OrderBy(x => x.Key.Name)
                .ToList();
        }, cancellationToken);
    }

    private static CadDocument ReadFile(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            if (extension == ".dxf")
            {
                using var reader = new DxfReader(filePath);
                return reader.Read();
            }
            else
            {
                using var reader = new DwgReader(filePath);
                return reader.Read();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot read '{Path.GetFileName(filePath)}'. " +
                "The file may be in an unsupported DWG version, contain duplicate object handles, " +
                "or be corrupt. Try opening the file in your CAD application and exporting it again " +
                "(File → Save As, choosing a DWG 2013 or DXF format)." +
                $"\n\nACadSharp: {ex.Message}", ex);
        }
    }

    public Task<bool> OpenAsync(string filePath, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        return OpenAsync(filePath, null, progress, cancellationToken);
    }

    public async Task<bool> OpenAsync(string filePath, ISet<Type>? excludedTypes, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Opening file {FilePath}", filePath);

        try
        {
            var document = await Task.Run(() =>
            {
                progress?.Report(10);

                CadDocument doc = ReadFile(filePath);

                progress?.Report(50);

                // Filter out excluded entity types using PLINQ for parallel filtering
                if (excludedTypes != null && excludedTypes.Count > 0)
                {
                    var entitiesToRemove = doc.Entities
                        .AsParallel()
                        .WithCancellation(cancellationToken)
                        .Where(e => excludedTypes.Contains(e.GetType()))
                        .ToList();

                    foreach (var entity in entitiesToRemove)
                    {
                        if (entity.Owner is BlockRecord owner)
                        {
                            owner.Entities.Remove(entity);
                        }
                    }
                }

                progress?.Report(100);
                return doc;
            }, cancellationToken);

            CurrentDocument.Load(document, filePath);

            int entityCount = CurrentDocument.ModelSpaceEntities.Count();
            int layerCount = CurrentDocument.Layers.Count();

            _logger.LogInformation("Opened {FilePath}: {EntityCount} entities, {LayerCount} layers", filePath, entityCount, layerCount);

            BoundingBoxHelper.InvalidateCache();
            DocumentLoaded?.Invoke(this, new DocumentLoadedEventArgs(filePath, entityCount, layerCount));

            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Open cancelled for {FilePath}", filePath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file {FilePath}", filePath);
            return false;
        }
    }

    public void LoadImported(ACadSharp.CadDocument document, string displayPath)
    {
        // FilePath is null so that Ctrl+S routes to Save As (DWG/DXF), not back to the JSON path.
        CurrentDocument.Load(document, null);
        CurrentDocument.IsDirty = true;

        int entityCount = CurrentDocument.ModelSpaceEntities.Count();
        int layerCount  = CurrentDocument.Layers.Count();

        _logger.LogInformation(
            "Loaded imported document from {DisplayPath}: {EntityCount} entities, {LayerCount} layers",
            displayPath, entityCount, layerCount);

        BoundingBoxHelper.InvalidateCache();
        DocumentLoaded?.Invoke(this, new DocumentLoadedEventArgs(displayPath, entityCount, layerCount));
    }

    public async Task<bool> SaveAsync(string? filePath = null, CancellationToken cancellationToken = default)
    {
        if (CurrentDocument.Document == null)
            return false;

        string targetPath = filePath ?? CurrentDocument.FilePath ?? throw new InvalidOperationException("No file path specified");

        _logger.LogInformation("Saving file to {FilePath}", targetPath);

        try
        {
            await Task.Run(() =>
            {
                // ACadSharp's header writer looks up CurrentLayerName in the layers table.
                // If that layer was deleted (e.g. via DeleteLayerCommand), the lookup throws
                // KeyNotFoundException. Reset to "0" — the default layer that always exists.
                string currentLayerName = CurrentDocument.Document.Header.CurrentLayerName;
                if (!string.IsNullOrEmpty(currentLayerName) &&
                    !CurrentDocument.Document.Layers.Any(l => l.Name == currentLayerName))
                {
                    _logger.LogWarning(
                        "Header CurrentLayer '{LayerName}' no longer exists; resetting to '0' before save",
                        currentLayerName);
                    CurrentDocument.Document.Header.CurrentLayerName = "0";
                }

                string extension = Path.GetExtension(targetPath).ToLowerInvariant();

                // Write to a temp file first so that a failed write never corrupts
                // (or replaces) the original file.  Move to the real path on success.
                string tempPath = targetPath + ".tmp";
                try
                {
                    if (extension == ".dxf")
                    {
                        using var writer = new DxfWriter(tempPath, CurrentDocument.Document, false);
                        writer.Write();
                    }
                    else
                    {
                        // ACadSharp's DwgWriter can produce duplicate object handles when
                        // entities/layers have been removed from a loaded document, making
                        // the output file unreadable.  Round-tripping through an in-memory
                        // DXF forces the DXF reader to re-assign all handles cleanly before
                        // the DwgWriter runs.
                        // DxfWriter closes the MemoryStream on disposal, so capture the
                        // bytes via ToArray() (which works on a disposed MemoryStream)
                        // and feed them to a fresh stream for the DxfReader.
                        CadDocument cleanDoc;
                        byte[] dxfBytes;
                        using (var ms = new MemoryStream())
                        {
                            using (var dxfWriter = new DxfWriter(ms, CurrentDocument.Document, false))
                                dxfWriter.Write();
                            dxfBytes = ms.ToArray();
                        }
                        using (var readMs = new MemoryStream(dxfBytes))
                        using (var dxfReader = new DxfReader(readMs))
                            cleanDoc = dxfReader.Read();
                        using var dwgWriter = new DwgWriter(tempPath, cleanDoc);
                        dwgWriter.Write();
                    }

                    File.Move(tempPath, targetPath, overwrite: true);
                }
                catch
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                    throw;
                }
            }, cancellationToken);

            CurrentDocument.IsDirty = false;
            _logger.LogInformation("Saved file to {FilePath}", targetPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file to {FilePath}", targetPath);
            return false;
        }
    }

    public void Close()
    {
        _logger.LogInformation("Closing document");
        CurrentDocument.Clear();
        SkiaRenderCache.Clear();
        BoundingBoxHelper.InvalidateCache();
        DocumentClosed?.Invoke(this, EventArgs.Empty);
    }
}
