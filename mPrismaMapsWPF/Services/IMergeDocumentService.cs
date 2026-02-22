using ACadSharp;
using mPrismaMapsWPF.Models;

namespace mPrismaMapsWPF.Services;

public interface IMergeDocumentService
{
    /// <summary>
    /// Merges all model-space content from <paramref name="secondary"/> into <paramref name="primary"/>.
    /// Layers and blocks are merged/deconflicted according to <paramref name="options"/>.
    /// </summary>
    MergeResult Merge(CadDocument primary, CadDocument secondary, MergeOptions options);

    /// <summary>
    /// Reads a DWG or DXF file from disk without loading it into the current document.
    /// </summary>
    CadDocument ReadFile(string filePath);
}
