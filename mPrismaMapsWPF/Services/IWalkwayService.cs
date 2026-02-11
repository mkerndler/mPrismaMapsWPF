using mPrismaMapsWPF.Models;

namespace mPrismaMapsWPF.Services;

public interface IWalkwayService
{
    WalkwayGraph Graph { get; }
    void RebuildGraph(IEnumerable<EntityModel> entities);
    HashSet<ulong>? GetPathHighlightsForUnit(double unitX, double unitY);
}
