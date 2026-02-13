using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;
using mPrismaMapsWPF.ViewModels;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.ViewModels;

public class EntityViewerViewModelTests
{
    private readonly Mock<ISelectionService> _selectionService;
    private readonly EntityViewerViewModel _viewModel;

    public EntityViewerViewModelTests()
    {
        _selectionService = new Mock<ISelectionService>();
        _viewModel = new EntityViewerViewModel(_selectionService.Object);
    }

    [Fact]
    public void SetEntities_PopulatesFilteredEntities()
    {
        var entities = new ObservableCollection<EntityModel>
        {
            EntityFactory.CreateEntityModel(EntityFactory.CreateLine(0, 0, 10, 10)),
            EntityFactory.CreateEntityModel(EntityFactory.CreateCircle(5, 5, 3))
        };

        _viewModel.SetEntities(entities);

        _viewModel.FilteredEntities.Should().HaveCount(2);
        _viewModel.TotalCount.Should().Be(2);
    }

    [Fact]
    public void Refresh_UpdatesTotalCount()
    {
        var entities = new ObservableCollection<EntityModel>
        {
            EntityFactory.CreateEntityModel(EntityFactory.CreateLine(0, 0, 10, 10))
        };
        _viewModel.SetEntities(entities);

        entities.Add(EntityFactory.CreateEntityModel(EntityFactory.CreateCircle(5, 5, 3)));
        _viewModel.Refresh();

        _viewModel.TotalCount.Should().Be(2);
    }

    [Fact]
    public void GroupingMode_ByType_GroupsEntities()
    {
        var entities = new ObservableCollection<EntityModel>
        {
            EntityFactory.CreateEntityModel(EntityFactory.CreateLine(0, 0, 10, 10)),
            EntityFactory.CreateEntityModel(EntityFactory.CreateLine(20, 20, 30, 30)),
            EntityFactory.CreateEntityModel(EntityFactory.CreateCircle(5, 5, 3))
        };
        _viewModel.SetEntities(entities);

        _viewModel.GroupingMode = EntityGroupingMode.ByType;

        _viewModel.GroupedEntities.Should().HaveCount(2); // Line and Circle groups
        _viewModel.FilteredEntities.Should().BeEmpty(); // Grouped mode clears flat list
    }

    [Fact]
    public void GroupingMode_None_FlatList()
    {
        var entities = new ObservableCollection<EntityModel>
        {
            EntityFactory.CreateEntityModel(EntityFactory.CreateLine(0, 0, 10, 10)),
            EntityFactory.CreateEntityModel(EntityFactory.CreateCircle(5, 5, 3))
        };
        _viewModel.SetEntities(entities);

        _viewModel.GroupingMode = EntityGroupingMode.None;

        _viewModel.FilteredEntities.Should().HaveCount(2);
        _viewModel.GroupedEntities.Should().BeEmpty();
    }

    [Fact]
    public void IsGroupByType_SetsGroupingMode()
    {
        _viewModel.IsGroupByType = true;
        _viewModel.GroupingMode.Should().Be(EntityGroupingMode.ByType);
    }

    [Fact]
    public void IsGroupByLayer_SetsGroupingMode()
    {
        _viewModel.IsGroupByLayer = true;
        _viewModel.GroupingMode.Should().Be(EntityGroupingMode.ByLayer);
    }

    [Fact]
    public void SuppressRefresh_PreventsAutoRefresh()
    {
        var entities = new ObservableCollection<EntityModel>();
        _viewModel.SetEntities(entities);
        _viewModel.SuppressRefresh = true;

        entities.Add(EntityFactory.CreateEntityModel(EntityFactory.CreateLine(0, 0, 10, 10)));

        // FilteredEntities should NOT have been updated since refresh is suppressed
        // (the initial SetEntities populated it, but the Add should not trigger refresh)
        _viewModel.FilteredEntities.Should().BeEmpty();
    }
}
