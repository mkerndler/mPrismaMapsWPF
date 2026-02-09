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
        RenderCache.Clear();
        DocumentClosed?.Invoke(this, EventArgs.Empty);
    }
}
