using System.IO;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using Microsoft.Extensions.Logging;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;

namespace mPrismaMapsWPF.Services;

/// <summary>
/// Merges two CAD documents by copying layers, block records, and model-space
/// entities from the secondary document into the primary document.
/// </summary>
public class MergeDocumentService : IMergeDocumentService
{
    private readonly ILogger<MergeDocumentService> _logger;

    // Block names that are structural parts of any DWG and must not be merged.
    private static readonly HashSet<string> ReservedBlockNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "*Model_Space", "*Paper_Space", "*Paper_Space0",
    };

    public MergeDocumentService(ILogger<MergeDocumentService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public CadDocument ReadFile(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            if (ext == ".dxf")
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

    /// <inheritdoc/>
    public MergeResult Merge(CadDocument primary, CadDocument secondary, MergeOptions options)
    {
        _logger.LogInformation(
            "Merging secondary document ({SecEntities} entities) into primary ({PrimEntities} entities)",
            secondary.Entities.Count(), primary.Entities.Count());

        var addedEntities = new List<Entity>();
        var addedLayers   = new List<Layer>();
        var addedBlocks   = new List<BlockRecord>();
        var updatedLayers = new List<(Layer Layer, Color OriginalColor)>();
        int entitiesSkipped   = 0;
        int layerConflicts    = 0;
        int blockConflicts    = 0;

        // ── Step 1: Merge layers ─────────────────────────────────────────────
        // Maps secondary layer name → resolved layer name in primary.
        var layerNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var secLayer in secondary.Layers)
        {
            if (secLayer.Name == "0")
            {
                // "0" always exists in primary; just map it.
                layerNameMap["0"] = "0";
                continue;
            }

            var existing = primary.Layers.FirstOrDefault(
                l => string.Equals(l.Name, secLayer.Name, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                var newLayer = new Layer(secLayer.Name)
                {
                    Color      = secLayer.Color,
                    Flags      = secLayer.Flags,
                    LineWeight = secLayer.LineWeight,
                };
                primary.Layers.Add(newLayer);
                layerNameMap[secLayer.Name] = secLayer.Name;
                addedLayers.Add(newLayer);
                _logger.LogDebug("Added layer '{LayerName}'", secLayer.Name);
            }
            else
            {
                layerConflicts++;
                switch (options.LayerConflictStrategy)
                {
                    case LayerConflictStrategy.KeepPrimary:
                        layerNameMap[secLayer.Name] = existing.Name;
                        break;

                    case LayerConflictStrategy.KeepSecondary:
                        updatedLayers.Add((existing, existing.Color));
                        existing.Color = secLayer.Color;
                        layerNameMap[secLayer.Name] = existing.Name;
                        break;

                    case LayerConflictStrategy.RenameSecondary:
                        string renamed = UniqueLayerName(primary, secLayer.Name);
                        var renamedLayer = new Layer(renamed)
                        {
                            Color      = secLayer.Color,
                            Flags      = secLayer.Flags,
                            LineWeight = secLayer.LineWeight,
                        };
                        primary.Layers.Add(renamedLayer);
                        layerNameMap[secLayer.Name] = renamed;
                        addedLayers.Add(renamedLayer);
                        break;
                }
            }
        }

        // ── Step 2: Merge block records ──────────────────────────────────────
        // Maps secondary block name → resolved block name in primary.
        var blockNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var secBlock in secondary.BlockRecords)
        {
            if (ReservedBlockNames.Contains(secBlock.Name))
                continue;

            var existing = primary.BlockRecords.FirstOrDefault(
                b => string.Equals(b.Name, secBlock.Name, StringComparison.OrdinalIgnoreCase));

            string targetName;
            if (existing == null)
            {
                targetName = secBlock.Name;
            }
            else
            {
                blockConflicts++;
                targetName = UniqueBlockName(primary, secBlock.Name);
                _logger.LogDebug(
                    "Block name conflict '{BlockName}' → renamed to '{NewName}'",
                    secBlock.Name, targetName);
            }

            var newBlock = new BlockRecord(targetName);
            primary.BlockRecords.Add(newBlock);
            CopyBlockEntities(secBlock, newBlock, primary, layerNameMap, options);
            blockNameMap[secBlock.Name] = targetName;
            addedBlocks.Add(newBlock);
        }

        // ── Step 3: Copy model-space entities ────────────────────────────────
        var primaryModelSpace = primary.ModelSpace;

        foreach (var entity in secondary.Entities)
        {
            var cloned = CloneEntity(entity, primary, layerNameMap, blockNameMap, options);

            if (cloned != null)
            {
                primaryModelSpace.Entities.Add(cloned);
                addedEntities.Add(cloned);
            }
            else
            {
                entitiesSkipped++;
                _logger.LogDebug("Skipped unsupported entity type {EntityType}", entity.GetType().Name);
            }
        }

        _logger.LogInformation(
            "Merge complete: {EntitiesAdded} entities added, {Skipped} skipped, " +
            "{LayersAdded} layers added ({LayerConflicts} conflicts), " +
            "{BlocksAdded} blocks added ({BlockConflicts} conflicts)",
            addedEntities.Count, entitiesSkipped,
            addedLayers.Count, layerConflicts,
            addedBlocks.Count, blockConflicts);

        return new MergeResult
        {
            AddedEntities         = addedEntities,
            AddedLayers           = addedLayers,
            AddedBlocks           = addedBlocks,
            UpdatedLayers         = updatedLayers,
            EntitiesSkipped       = entitiesSkipped,
            LayerConflictsResolved = layerConflicts,
            BlockConflictsResolved = blockConflicts,
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Entity? CloneEntity(
        Entity entity,
        CadDocument primary,
        Dictionary<string, string> layerNameMap,
        Dictionary<string, string> blockNameMap,
        MergeOptions options)
    {
        var targetLayer = ResolveLayer(primary, entity.Layer?.Name, layerNameMap);

        if (entity is Insert insert)
        {
            return CloneInsert(insert, targetLayer, primary, blockNameMap, options);
        }

        return EntityCloner.Clone(entity, targetLayer, options.OffsetX, options.OffsetY);
    }

    private static Insert? CloneInsert(
        Insert src,
        Layer targetLayer,
        CadDocument primary,
        Dictionary<string, string> blockNameMap,
        MergeOptions options)
    {
        string srcBlockName = src.Block?.Name ?? string.Empty;
        blockNameMap.TryGetValue(srcBlockName, out string? mappedName);
        mappedName ??= srcBlockName;

        var targetBlock = primary.BlockRecords.FirstOrDefault(
            b => string.Equals(b.Name, mappedName, StringComparison.OrdinalIgnoreCase));

        if (targetBlock == null)
            return null;

        return new Insert(targetBlock)
        {
            InsertPoint = new CSMath.XYZ(
                src.InsertPoint.X + options.OffsetX,
                src.InsertPoint.Y + options.OffsetY,
                src.InsertPoint.Z),
            Rotation = src.Rotation,
            XScale   = src.XScale,
            YScale   = src.YScale,
            ZScale   = src.ZScale,
            Layer    = targetLayer,
            Color    = src.Color,
        };
    }

    private void CopyBlockEntities(
        BlockRecord src,
        BlockRecord dest,
        CadDocument primary,
        Dictionary<string, string> layerNameMap,
        MergeOptions options)
    {
        foreach (var entity in src.Entities)
        {
            var targetLayer = ResolveLayer(primary, entity.Layer?.Name, layerNameMap);
            // Block-internal entities use (0,0) offset; the block origin handles positioning.
            var cloned = entity is Insert ins
                ? CloneInsert(ins, targetLayer, primary, new Dictionary<string, string>(),
                    new MergeOptions { LayerConflictStrategy = options.LayerConflictStrategy, OffsetX = 0, OffsetY = 0 })
                : EntityCloner.Clone(entity, targetLayer, 0, 0);

            if (cloned != null)
                dest.Entities.Add(cloned);
        }
    }

    private static Layer ResolveLayer(
        CadDocument primary,
        string? layerName,
        Dictionary<string, string> layerNameMap)
    {
        if (string.IsNullOrWhiteSpace(layerName))
            return primary.Layers.First(l => l.Name == "0");

        layerNameMap.TryGetValue(layerName, out string? mappedName);
        mappedName ??= layerName;

        return primary.Layers.FirstOrDefault(l =>
            string.Equals(l.Name, mappedName, StringComparison.OrdinalIgnoreCase))
            ?? primary.Layers.First(l => l.Name == "0");
    }

    private static string UniqueLayerName(CadDocument primary, string baseName)
    {
        string candidate = baseName + "_merged";
        int index = 2;
        while (primary.Layers.Any(l => string.Equals(l.Name, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseName}_merged{index++}";
        }
        return candidate;
    }

    private static string UniqueBlockName(CadDocument primary, string baseName)
    {
        string candidate = baseName + "_merged";
        int index = 2;
        while (primary.BlockRecords.Any(b => string.Equals(b.Name, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseName}_merged{index++}";
        }
        return candidate;
    }
}
