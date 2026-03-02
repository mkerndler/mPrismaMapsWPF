# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the project
dotnet build

# Build for Release
dotnet build --configuration Release

# Run the application
dotnet run --project mPrismaMapsWPF

# Run all tests
dotnet test

# Run a single test by name
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Run tests in a specific category
dotnet test --filter "FullyQualifiedName~Commands"
```

## Project Overview

**mPrismaMapsWPF** is a WPF desktop application for viewing, editing, and managing CAD drawings (DWG/DXF files). Its primary purpose is to prepare mall/store floor plan maps for the **MPOL** indoor positioning system. It targets .NET 10.0 for Windows with nullable reference types and implicit usings enabled.

The solution (`mPrismaMapsWPF.slnx`) contains two projects:
- `mPrismaMapsWPF/` — Main WPF application (WinExe)
- `mPrismaMapsWPF.Tests/` — xUnit test project (with FluentAssertions and Moq)

For deep reference, see `DOCUMENTATION.md` in the repo root.

## Architecture

**Pattern:** MVVM with service-based dependency injection (CommunityToolkit.Mvvm + Microsoft.Extensions.DependencyInjection).

### Key Dependencies
- **ACadSharp** — CAD file reading/writing (DWG/DXF)
- **SkiaSharp** — Graphics rendering and bitmap caching
- **CommunityToolkit.Mvvm** — ObservableObject, RelayCommand
- **Microsoft.Data.SqlClient** — SQL Server for MPOL deployment
- **Serilog** — Structured logging to daily rolling files in `logs/`

### Layered Structure

**App.xaml.cs** — Entry point: configures Serilog, registers all services in DI container, sets up global exception handlers.

**Views** — `MainWindow.xaml/.cs` plus dialogs: DeleteLayerDialog, DeleteMultipleLayersDialog, DeployMpolDialog, EditUnitNumberDialog, ExportMpolDialog, MergeOptionsDialog, ResizeUnitNumbersDialog, RestoreBackupDialog, RotateViewDialog, ScaleMapDialog, SelectEntityTypesDialog.

**ViewModels** — `MainWindowViewModel` (primary orchestrator), `LayerPanelViewModel`, `PropertiesPanelViewModel`, `EntityViewerViewModel`.

**Models** — `CadDocumentModel`, `EntityModel` (observable wrapper for CAD entities), `LayerModel`, `EntityGroupModel`, `WalkwayGraph`, `MpolExportModel`, `MergeOptions`.

**Services** (all dependency-injected):
- `DocumentService` — CAD file I/O
- `SelectionService` — Entity selection state
- `UndoRedoService` — Command history (100-item limit)
- `RenderService` — Two-pass entity rendering with viewport culling
- `WalkwayService`, `DeployService`, `BackupService`, `MpolExportService`
- `MergeDocumentService` — DWG file merging
- `LegacyMapImportExport` — Legacy format support

**Commands** — 21 `IUndoableCommand` implementations for all user actions (entity add/delete/move/paste/transform, layer operations, property changes, generation operations, walkway editing). Every user action goes through this pattern.

**Controls** — `CadCanvas` is a custom FrameworkElement using DrawingVisual + RenderTargetBitmap with spatial indexing (`SpatialGrid`) for hit testing and `SkiaRenderCache` for bitmap caching.

**Drawing Tools** — `IDrawingTool` interface with implementations: LineTool, PolylineTool, PolygonTool, FairwayTool, UnitNumberTool, ZoomAreaTool.

**Rendering** — `IEntityRenderer` implementations per entity type (Line, Circle, Arc, Polyline, Text, Ellipse, Point, Insert). Background layers render first.

**Helpers** — BoundingBoxHelper, ColorHelper, EntityCloner, EntityTransformHelper, FloodFillGrid, HitTestHelper, SkiaRenderCache, SnapHelper, SpatialGrid, BulkObservableCollection, TransformHitTestHelper.

### Coordinate System

CAD coordinates have the Y-axis inverted relative to screen space. Transforms between the two coordinate systems are applied throughout `CadCanvas` and the rendering pipeline. Be mindful of this when working with hit testing, entity positions, or drawing tools.

### Special Layers
Generated content uses dedicated layer names: `Unit Areas`, `Background Contours`, `Walkways`, `Unit Numbers`.

## Testing

Tests use **xUnit** + **FluentAssertions** + **Moq**. Test files are organized by category: Commands/, Drawing/, Helpers/, Models/, Services/, ViewModels/. `EntityFactory` provides test entity creation utilities.

## CI

GitHub Actions (`.github/workflows/ci.yml`): Triggers on push/PR to master. Runs on windows-latest with .NET 10.0 preview. Steps: restore, build Release, run tests, upload test results.
