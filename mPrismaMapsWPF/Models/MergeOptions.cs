using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;

namespace mPrismaMapsWPF.Models;

/// <summary>
/// Determines how to handle layer name collisions when merging two documents.
/// </summary>
public enum LayerConflictStrategy
{
    /// <summary>Keep the primary document's layer properties.</summary>
    KeepPrimary,

    /// <summary>Overwrite the primary layer's color with the secondary layer's color.</summary>
    KeepSecondary,

    /// <summary>Add the secondary layer with a "_merged" suffix instead of overwriting.</summary>
    RenameSecondary,
}

/// <summary>
/// Options that control how two CAD documents are merged.
/// </summary>
public class MergeOptions
{
    public LayerConflictStrategy LayerConflictStrategy { get; set; } = LayerConflictStrategy.KeepPrimary;

    /// <summary>Optional X offset applied to every entity copied from the secondary document.</summary>
    public double OffsetX { get; set; } = 0;

    /// <summary>Optional Y offset applied to every entity copied from the secondary document.</summary>
    public double OffsetY { get; set; } = 0;
}

/// <summary>
/// Result of a merge operation. Holds the lists needed for undo.
/// </summary>
public class MergeResult
{
    public required List<Entity> AddedEntities { get; init; }
    public required List<Layer> AddedLayers { get; init; }
    public required List<BlockRecord> AddedBlocks { get; init; }

    /// <summary>Layers whose color was overwritten (KeepSecondary strategy). Holds original colors for undo.</summary>
    public required List<(Layer Layer, Color OriginalColor)> UpdatedLayers { get; init; }

    public int EntitiesSkipped { get; init; }
    public int LayerConflictsResolved { get; init; }
    public int BlockConflictsResolved { get; init; }
}
