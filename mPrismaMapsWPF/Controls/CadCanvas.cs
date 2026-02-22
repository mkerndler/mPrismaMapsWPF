using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ACadSharp.Entities;
using mPrismaMapsWPF.Drawing;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Rendering;
using SkiaSharp;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;

namespace mPrismaMapsWPF.Controls;

public class CadCanvas : FrameworkElement
{
    private readonly RenderService _renderService;

    // Thin overlay visual updated synchronously on every mouse move so drawing
    // preview responds immediately without waiting for the next vsync tick.
    private readonly DrawingVisual _previewVisual;
    private readonly VisualCollection _visuals;

    private WpfPoint _panStart;
    private bool _isPanning;
    private WpfPoint _offset;
    private double _scale = 1.0;
    private Extents? _extents;

    // View transforms
    private bool _flipX;
    private bool _flipY;
    private double _viewRotation;

    // Entity cache: Skia renders into a WriteableBitmap → WPF uploads to GPU once →
    // subsequent DrawImage calls are free GPU texture blits.
    private WriteableBitmap? _cachedBitmap;
    private double _cachedScale;
    private WpfPoint _cachedOffset;
    private int _cachedWidth;
    private int _cachedHeight;
    private bool _cacheValid;
    private HashSet<string>? _cachedHiddenLayers;

    // Overlay cache: selection + path-highlight, transparent background.
    // Also a WriteableBitmap → GPU texture. Rebuilt only when selection/highlight changes.
    private WriteableBitmap? _overlayBitmap;
    private bool _overlayValid;
    private HashSet<ulong>? _cachedSelectedHandles;
    private HashSet<ulong>? _cachedHighlightHandles;

    // Spatial index for hit testing
    private SpatialGrid? _spatialGrid;
    private Dictionary<ulong, Entity>? _entityByHandle;
    private HashSet<ulong> _selectedHandlesSet = new();
    private HashSet<ulong> _lockedHandlesSet = new();
    private HashSet<string> _lockedLayersSet = new();
    private HashSet<string> _currentHiddenLayers = new(); // cached to avoid per-frame allocation

    // Cached viewport bounds per frame
    private Rect? _frameViewportBounds;

    // Drawing support
    private IDrawingTool? _currentTool;
    private WpfPoint _currentCadPoint;

    // Marquee selection
    private bool _isMarqueeSelecting;
    private WpfPoint _marqueeStartScreen;
    private WpfPoint _marqueeCurrentScreen;

    // Move drag
    private bool _isMoving;
    private WpfPoint _moveStartCadPoint;
    private WpfPoint _moveCurrentCadPoint;

    // Transform drag state
    private bool _isTransformDragging;
    private TransformHandle _activeTransformHandle;
    private TransformHandle _hoveredTransformHandle;
    private WpfPoint _transformStartScreenPoint;
    private Rect _transformBoundingBoxCad;
    private Rect _transformBoundingBoxScreen;
    private double _transformStartAngle;
    private double _transformCurrentAngle;
    private double _transformScaleX = 1.0;
    private double _transformScaleY = 1.0;

    // Static WPF pens for GPU-rendered overlays
    private static readonly Pen GridPenMinor = CreateGridPen(WpfColor.FromArgb(40, 128, 128, 128));
    private static readonly Pen GridPenMajor = CreateGridPen(WpfColor.FromArgb(80, 128, 128, 128));
    private static readonly Pen PreviewPen = CreatePreviewPen();

    private static Pen CreateGridPen(WpfColor color)
    {
        var pen = new Pen(new SolidColorBrush(color), 1);
        pen.Freeze();
        return pen;
    }

    private static Pen CreatePreviewPen()
    {
        var pen = new Pen(Brushes.Cyan, 1.5) { DashStyle = DashStyles.Dash };
        pen.Freeze();
        return pen;
    }

    public CadCanvas()
    {
        _renderService = new RenderService();
        _previewVisual = new DrawingVisual();
        _visuals = new VisualCollection(this) { _previewVisual };

        ClipToBounds = true;
        Focusable = true;

        MouseWheel += OnMouseWheel;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseRightButtonDown += OnMouseRightButtonDown;
        MouseRightButtonUp += OnMouseRightButtonUp;
        MouseMove += OnMouseMove;
        SizeChanged += OnSizeChanged;
        KeyDown += OnKeyDown;
    }

    protected override int VisualChildrenCount => _visuals.Count;
    protected override Visual GetVisualChild(int index) => _visuals[index];

    protected override void OnRender(DrawingContext dc) => RenderFrame(dc);

    // ── Dependency properties ────────────────────────────────────────────────

    public static readonly DependencyProperty EntitiesProperty =
        DependencyProperty.Register(nameof(Entities), typeof(IEnumerable<Entity>),
            typeof(CadCanvas), new PropertyMetadata(null, OnEntitiesChanged));

    public IEnumerable<Entity>? Entities
    {
        get => (IEnumerable<Entity>?)GetValue(EntitiesProperty);
        set => SetValue(EntitiesProperty, value);
    }

    public static readonly DependencyProperty SelectedHandlesProperty =
        DependencyProperty.Register(nameof(SelectedHandles), typeof(IEnumerable<ulong>),
            typeof(CadCanvas), new PropertyMetadata(null, OnSelectedHandlesChanged));

    public IEnumerable<ulong>? SelectedHandles
    {
        get => (IEnumerable<ulong>?)GetValue(SelectedHandlesProperty);
        set => SetValue(SelectedHandlesProperty, value);
    }

    public static readonly DependencyProperty HiddenLayersProperty =
        DependencyProperty.Register(nameof(HiddenLayers), typeof(IEnumerable<string>),
            typeof(CadCanvas), new PropertyMetadata(null, OnHiddenLayersChanged));

    public IEnumerable<string>? HiddenLayers
    {
        get => (IEnumerable<string>?)GetValue(HiddenLayersProperty);
        set => SetValue(HiddenLayersProperty, value);
    }

    public static readonly DependencyProperty LockedLayersProperty =
        DependencyProperty.Register(nameof(LockedLayers), typeof(IEnumerable<string>),
            typeof(CadCanvas), new PropertyMetadata(null, OnLockedLayersChanged));

    public IEnumerable<string>? LockedLayers
    {
        get => (IEnumerable<string>?)GetValue(LockedLayersProperty);
        set => SetValue(LockedLayersProperty, value);
    }

    public static readonly DependencyProperty LockedHandlesProperty =
        DependencyProperty.Register(nameof(LockedHandles), typeof(IEnumerable<ulong>),
            typeof(CadCanvas), new PropertyMetadata(null, OnLockedHandlesChanged));

    public IEnumerable<ulong>? LockedHandles
    {
        get => (IEnumerable<ulong>?)GetValue(LockedHandlesProperty);
        set => SetValue(LockedHandlesProperty, value);
    }

    public static readonly DependencyProperty IsPanModeProperty =
        DependencyProperty.Register(nameof(IsPanMode), typeof(bool),
            typeof(CadCanvas), new PropertyMetadata(false));

    public bool IsPanMode
    {
        get => (bool)GetValue(IsPanModeProperty);
        set => SetValue(IsPanModeProperty, value);
    }

    public static readonly DependencyProperty ExtentsProperty =
        DependencyProperty.Register(nameof(Extents), typeof(Extents),
            typeof(CadCanvas), new PropertyMetadata(null, OnExtentsChanged));

    public Extents? Extents
    {
        get => (Extents?)GetValue(ExtentsProperty);
        set => SetValue(ExtentsProperty, value);
    }

    public static readonly DependencyProperty DrawingModeProperty =
        DependencyProperty.Register(nameof(DrawingMode), typeof(DrawingMode),
            typeof(CadCanvas), new PropertyMetadata(DrawingMode.Select, OnDrawingModeChanged));

    public DrawingMode DrawingMode
    {
        get => (DrawingMode)GetValue(DrawingModeProperty);
        set => SetValue(DrawingModeProperty, value);
    }

    public static readonly DependencyProperty GridSettingsProperty =
        DependencyProperty.Register(nameof(GridSettings), typeof(GridSnapSettings),
            typeof(CadCanvas), new PropertyMetadata(null, OnGridSettingsChanged));

    public GridSnapSettings? GridSettings
    {
        get => (GridSnapSettings?)GetValue(GridSettingsProperty);
        set => SetValue(GridSettingsProperty, value);
    }

    public static readonly DependencyProperty FlipXProperty =
        DependencyProperty.Register(nameof(FlipX), typeof(bool),
            typeof(CadCanvas), new PropertyMetadata(false, OnViewTransformChanged));

    public bool FlipX
    {
        get => (bool)GetValue(FlipXProperty);
        set => SetValue(FlipXProperty, value);
    }

    public static readonly DependencyProperty FlipYProperty =
        DependencyProperty.Register(nameof(FlipY), typeof(bool),
            typeof(CadCanvas), new PropertyMetadata(false, OnViewTransformChanged));

    public bool FlipY
    {
        get => (bool)GetValue(FlipYProperty);
        set => SetValue(FlipYProperty, value);
    }

    public static readonly DependencyProperty ViewRotationProperty =
        DependencyProperty.Register(nameof(ViewRotation), typeof(double),
            typeof(CadCanvas), new PropertyMetadata(0.0, OnViewTransformChanged));

    public double ViewRotation
    {
        get => (double)GetValue(ViewRotationProperty);
        set => SetValue(ViewRotationProperty, value);
    }

    public static readonly DependencyProperty HighlightedPathHandlesProperty =
        DependencyProperty.Register(nameof(HighlightedPathHandles), typeof(HashSet<ulong>),
            typeof(CadCanvas), new PropertyMetadata(null, OnHighlightedPathHandlesChanged));

    public HashSet<ulong>? HighlightedPathHandles
    {
        get => (HashSet<ulong>?)GetValue(HighlightedPathHandlesProperty);
        set => SetValue(HighlightedPathHandlesProperty, value);
    }

    private static void OnHighlightedPathHandlesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas canvas) { canvas._overlayValid = false; canvas.Render(); }
    }

    // ── Events ───────────────────────────────────────────────────────────────

    public event EventHandler<CadMouseEventArgs>? CadMouseMove;
    public event EventHandler<CadEntityClickEventArgs>? EntityClicked;
    public event EventHandler<CadEntityClickEventArgs>? EntityDoubleClicked;
    public event EventHandler<DrawingCompletedEventArgs>? DrawingCompleted;
    public event EventHandler<MarqueeSelectionEventArgs>? MarqueeSelectionCompleted;
    public event EventHandler<MoveCompletedEventArgs>? MoveCompleted;
    public event EventHandler<TransformCompletedEventArgs>? TransformCompleted;
    public event EventHandler<ToggleEntranceEventArgs>? ToggleEntranceRequested;

    // ── Public API ───────────────────────────────────────────────────────────

    public double Scale
    {
        get => _scale;
        set { _scale = Math.Max(0.001, Math.Min(1000, value)); InvalidateCache(); Render(); UpdatePreviewVisual(); }
    }

    public void ZoomToFit()
    {
        var extents = Extents ?? _extents;
        if (extents == null || !extents.IsValid || ActualWidth <= 0 || ActualHeight <= 0) return;

        double margin = 50;
        double scaleX = (ActualWidth - margin * 2) / extents.Width;
        double scaleY = (ActualHeight - margin * 2) / extents.Height;
        _scale = Math.Min(scaleX, scaleY);
        _offset = new WpfPoint(-extents.CenterX + (ActualWidth / 2) / _scale,
                                extents.CenterY + (ActualHeight / 2) / _scale);
        InvalidateCache();
        Render();
        UpdatePreviewVisual();
    }

    public void CenterOnOrigin()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        _offset = new WpfPoint((ActualWidth / 2) / _scale, (ActualHeight / 2) / _scale);
        InvalidateCache();
        Render();
        UpdatePreviewVisual();
    }

    public void ResetViewTransforms() { FlipX = false; FlipY = false; ViewRotation = 0; }

    public void ZoomIn()  { ZoomAtPoint(new WpfPoint(ActualWidth / 2, ActualHeight / 2), 1.25); }
    public void ZoomOut() { ZoomAtPoint(new WpfPoint(ActualWidth / 2, ActualHeight / 2), 1.0 / 1.25); }

    public void ZoomToEntity(Entity entity)
    {
        if (entity == null || ActualWidth <= 0 || ActualHeight <= 0) return;
        var e = new Extents(); e.Expand(entity);
        if (e.IsValid) ZoomToRect(e.MinX, e.MinY, e.MaxX, e.MaxY);
    }

    public void ZoomToRect(double minX, double minY, double maxX, double maxY)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        double w = maxX - minX, h = maxY - minY;
        if (w < 1) w = 100; if (h < 1) h = 100;
        double margin = 50;
        _scale = Math.Min(Math.Min((ActualWidth - margin * 2) / w, (ActualHeight - margin * 2) / h), 50);
        _offset = new WpfPoint(-(minX + maxX) / 2 + (ActualWidth / 2) / _scale,
                                (minY + maxY) / 2 + (ActualHeight / 2) / _scale);
        InvalidateCache();
        Render();
        UpdatePreviewVisual();
    }

    public WpfPoint ScreenToCad(WpfPoint screenPoint) =>
        new WpfPoint(screenPoint.X / _scale - _offset.X, -(screenPoint.Y / _scale - _offset.Y));

    public WpfPoint GetSnappedCadPoint(WpfPoint screenPoint)
    {
        var cad = ScreenToCad(screenPoint);
        return (GridSettings != null && GridSettings.IsEnabled) ? SnapHelper.SnapToGrid(cad, GridSettings) : cad;
    }

    // InvalidateVisual() coalesces all rapid Render() calls into at most one
    // OnRender() call per vsync tick, eliminating render-thread sync stalls.
    public void Render() => InvalidateVisual();

    public void InvalidateCache()
    {
        _cacheValid = false;
        _cachedBitmap = null;
        _overlayValid = false;
        _overlayBitmap = null;
    }

    public Rect GetViewportBounds() => CalculateViewportBounds();

    // ── Core rendering pipeline ──────────────────────────────────────────────

    private void RenderFrame(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, ActualWidth, ActualHeight));

        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        _frameViewportBounds = CalculateViewportBounds();
        int w = (int)ActualWidth, h = (int)ActualHeight;

        ApplyViewTransform(dc, w, h);

        // ── Entity cache + overlay (only when a document is loaded) ───────────
        var entities = Entities;
        if (entities != null)
        {
            bool scaleChanged     = Math.Abs(_scale - _cachedScale) > 0.0001;
            bool sizeChanged      = w != _cachedWidth || h != _cachedHeight;
            bool layersChanged    = !HiddenLayersMatchCache();
            bool selectionChanged = !SelectionMatchesCache();
            bool highlightChanged = !HighlightMatchesCache();

            // Fast pan path: both GPU textures are valid, just offset them
            if (_isPanning && _cacheValid && _overlayValid && !scaleChanged && !sizeChanged)
            {
                double dx = (_offset.X - _cachedOffset.X) * _scale;
                double dy = (_offset.Y - _cachedOffset.Y) * _scale;
                dc.DrawImage(_cachedBitmap, new Rect(dx, dy, w, h));
                if (_selectedHandlesSet.Count > 0 || HighlightedPathHandles?.Count > 0)
                    dc.DrawImage(_overlayBitmap, new Rect(dx, dy, w, h));
                RestoreViewTransform(dc);
                return;
            }

            if (!_cacheValid || scaleChanged || sizeChanged || layersChanged)
            {
                RebuildEntityCache(entities, w, h);
                _overlayValid = false;
            }

            if (!_overlayValid || selectionChanged || highlightChanged)
                RebuildOverlay(entities, w, h);

            if (_cachedBitmap != null)
                dc.DrawImage(_cachedBitmap, new Rect(0, 0, w, h));

            if (_overlayBitmap != null && (_selectedHandlesSet.Count > 0 || HighlightedPathHandles?.Count > 0))
                dc.DrawImage(_overlayBitmap, new Rect(0, 0, w, h));
        }

        // Grid stays in the entity layer (depends on scale/offset, not cursor)
        RenderGrid(dc);

        RestoreViewTransform(dc);
    }

    // ── Synchronous preview visual ───────────────────────────────────────────

    /// <summary>
    /// Redraws the preview DrawingVisual immediately (no vsync deferral).
    /// Called directly from mouse-move handlers so the cursor preview is always
    /// pixel-current regardless of the entity-layer redraw schedule.
    /// </summary>
    private void UpdatePreviewVisual()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        using var dc = _previewVisual.RenderOpen();
        int w = (int)ActualWidth, h = (int)ActualHeight;
        ApplyViewTransform(dc, w, h);
        RenderDrawingPreview(dc);
        RenderMarqueePreview(dc);
        RenderMovePreview(dc);
        RenderTransformHandles(dc);
        RenderTransformPreview(dc);
        RestoreViewTransform(dc);
    }

    // ── Skia entity cache ────────────────────────────────────────────────────

    /// <summary>
    /// Renders all entities (no selection color) into a WriteableBitmap via Skia.
    /// After this call the bitmap is uploaded to the GPU as a texture on the next
    /// DrawingContext.DrawImage and cached there by WPF's compositor.
    /// </summary>
    private void RebuildEntityCache(IEnumerable<Entity> entities, int w, int h)
    {
        if (_cachedBitmap == null || _cachedBitmap.PixelWidth != w || _cachedBitmap.PixelHeight != h)
            _cachedBitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Pbgra32, null);

        _cachedBitmap.Lock();
        try
        {
            var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info, _cachedBitmap.BackBuffer, _cachedBitmap.BackBufferStride);
            surface.Canvas.Clear(SKColors.Black);

            var rc = new RenderContext
            {
                Scale = _scale,
                Offset = _offset,
                DefaultColor = System.Windows.Media.Colors.White,
                LineThickness = 1.0,
                ShowSelection = false,
                ViewportBounds = _frameViewportBounds ?? CalculateViewportBounds()
            };
            foreach (var layer in _currentHiddenLayers) rc.HiddenLayers.Add(layer);

            _renderService.RenderEntities(surface.Canvas, entities, rc);
            _cachedBitmap.AddDirtyRect(new Int32Rect(0, 0, w, h));
        }
        finally { _cachedBitmap.Unlock(); }

        _cachedScale = _scale;
        _cachedOffset = _offset;
        _cachedWidth = w;
        _cachedHeight = h;
        _cachedHiddenLayers = HiddenLayers?.ToHashSet() ?? new HashSet<string>();
        _cacheValid = true;
    }

    // ── Skia overlay cache ───────────────────────────────────────────────────

    /// <summary>
    /// Renders selected entities (cyan) and highlighted path entities (orange) into a
    /// WriteableBitmap with a fully-transparent background. WPF alpha-blends this over
    /// the entity cache via DrawingContext.DrawImage.
    /// </summary>
    private void RebuildOverlay(IEnumerable<Entity> entities, int w, int h)
    {
        if (_overlayBitmap == null || _overlayBitmap.PixelWidth != w || _overlayBitmap.PixelHeight != h)
            _overlayBitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Pbgra32, null);

        _overlayBitmap.Lock();
        try
        {
            var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info, _overlayBitmap.BackBuffer, _overlayBitmap.BackBufferStride);
            surface.Canvas.Clear(SKColors.Transparent);

            var viewport = _frameViewportBounds ?? CalculateViewportBounds();

            // Selection overlay
            if (_selectedHandlesSet.Count > 0)
            {
                var rc = new RenderContext
                {
                    Scale = _scale, Offset = _offset,
                    DefaultColor = System.Windows.Media.Colors.White,
                    LineThickness = 1.0, ShowSelection = true,
                    ViewportBounds = viewport
                };
                foreach (var h2 in _selectedHandlesSet) rc.SelectedHandles.Add(h2);
                foreach (var l in _currentHiddenLayers) rc.HiddenLayers.Add(l);

                if (_entityByHandle != null)
                {
                    var sel = new List<Entity>(_selectedHandlesSet.Count);
                    foreach (var handle in _selectedHandlesSet)
                        if (_entityByHandle.TryGetValue(handle, out var e)) sel.Add(e);
                    _renderService.RenderEntities(surface.Canvas, sel, rc);
                }
                else
                {
                    _renderService.RenderEntities(surface.Canvas,
                        entities.Where(e => _selectedHandlesSet.Contains(e.Handle)), rc);
                }
            }

            // Path-highlight overlay
            var highlights = HighlightedPathHandles;
            if (highlights != null && highlights.Count > 0 && _entityByHandle != null)
            {
                var rc = new RenderContext
                {
                    Scale = _scale, Offset = _offset,
                    DefaultColor = System.Windows.Media.Colors.Orange,
                    LineThickness = 3.0, ShowSelection = false,
                    ViewportBounds = viewport
                };
                var highlighted = new List<Entity>();
                foreach (var handle in highlights)
                    if (_entityByHandle.TryGetValue(handle, out var e)) highlighted.Add(e);
                _renderService.RenderEntities(surface.Canvas, highlighted, rc);
            }

            _overlayBitmap.AddDirtyRect(new Int32Rect(0, 0, w, h));
        }
        finally { _overlayBitmap.Unlock(); }

        _overlayValid = true;
        _cachedSelectedHandles = new HashSet<ulong>(_selectedHandlesSet);
        _cachedHighlightHandles = HighlightedPathHandles != null
            ? new HashSet<ulong>(HighlightedPathHandles) : null;
    }

    // ── WPF DrawingContext overlays (GPU-rendered) ───────────────────────────

    private void ApplyViewTransform(DrawingContext dc, int width, int height)
    {
        if (!_flipX && !_flipY && Math.Abs(_viewRotation) < 0.001) return;

        double cx = width / 2.0, cy = height / 2.0;
        var tg = new TransformGroup();
        if (_flipX || _flipY)
            tg.Children.Add(new ScaleTransform(_flipX ? -1 : 1, _flipY ? -1 : 1, cx, cy));
        if (Math.Abs(_viewRotation) >= 0.001)
            tg.Children.Add(new RotateTransform(_viewRotation, cx, cy));
        dc.PushTransform(tg);
    }

    private void RestoreViewTransform(DrawingContext dc)
    {
        if (!_flipX && !_flipY && Math.Abs(_viewRotation) < 0.001) return;
        dc.Pop();
    }

    private void RenderGrid(DrawingContext dc)
    {
        if (GridSettings == null || !GridSettings.ShowGrid) return;

        var viewport = _frameViewportBounds ?? CalculateViewportBounds();
        double spacingX = GridSettings.SpacingX, spacingY = GridSettings.SpacingY;
        if (spacingX <= 0 || spacingY <= 0) return;
        if (spacingX * _scale < 5 || spacingY * _scale < 5) return;

        double startX = Math.Floor((viewport.Left  - GridSettings.OriginX) / spacingX) * spacingX + GridSettings.OriginX;
        double startY = Math.Floor((viewport.Top   - GridSettings.OriginY) / spacingY) * spacingY + GridSettings.OriginY;
        int lineCount = 0, maxLines = 200;

        for (double x = startX; x <= viewport.Right && lineCount < maxLines; x += spacingX, lineCount++)
        {
            bool major = Math.Abs(x % (spacingX * 5)) < 0.0001;
            dc.DrawLine(major ? GridPenMajor : GridPenMinor,
                CadToScreen(x, viewport.Top), CadToScreen(x, viewport.Bottom));
        }
        for (double y = startY; y <= viewport.Bottom && lineCount < maxLines; y += spacingY, lineCount++)
        {
            bool major = Math.Abs(y % (spacingY * 5)) < 0.0001;
            dc.DrawLine(major ? GridPenMajor : GridPenMinor,
                CadToScreen(viewport.Left, y), CadToScreen(viewport.Right, y));
        }
    }

    private void RenderDrawingPreview(DrawingContext dc)
    {
        if (_currentTool is FairwayTool)
        {
            RenderFairwayPreview(dc);
            if (!_currentTool.IsDrawing) return;
        }

        if (_currentTool == null || !_currentTool.IsDrawing) return;

        if (_currentTool is UnitNumberTool unitTool) { RenderUnitNumberPreview(dc, unitTool); return; }
        if (_currentTool is FairwayTool) return;

        var pts = _currentTool.GetPreviewPoints();
        if (pts == null || pts.Count < 2) return;

        var screen = pts.Select(p => CadToScreen(p.X, p.Y)).ToList();
        for (int i = 0; i < screen.Count - 1; i++)
            dc.DrawLine(PreviewPen, screen[i], screen[i + 1]);

        if (_currentTool.IsPreviewClosed && screen.Count > 2)
            dc.DrawLine(PreviewPen, screen[^1], screen[0]);

        foreach (var pt in screen)
            dc.DrawEllipse(Brushes.Cyan, null, pt, 4, 4);
    }

    private void RenderMarqueePreview(DrawingContext dc)
    {
        if (!_isMarqueeSelecting) return;

        double x = Math.Min(_marqueeStartScreen.X, _marqueeCurrentScreen.X);
        double y = Math.Min(_marqueeStartScreen.Y, _marqueeCurrentScreen.Y);
        double w = Math.Abs(_marqueeCurrentScreen.X - _marqueeStartScreen.X);
        double h = Math.Abs(_marqueeCurrentScreen.Y - _marqueeStartScreen.Y);
        if (w < 1 && h < 1) return;

        var rect = new Rect(x, y, w, h);
        bool isWindow = _marqueeCurrentScreen.X >= _marqueeStartScreen.X;

        if (isWindow)
        {
            var pen = new Pen(Brushes.Cyan, 1.0); pen.Freeze();
            dc.DrawRectangle(null, pen, rect);
        }
        else
        {
            var pen = new Pen(Brushes.LimeGreen, 1.0) { DashStyle = DashStyles.Dash }; pen.Freeze();
            var fill = new SolidColorBrush(WpfColor.FromArgb(30, 0, 255, 0)); fill.Freeze();
            dc.DrawRectangle(fill, pen, rect);
        }
    }

    /// <summary>
    /// Reuses the overlay bitmap at a screen offset to show a 50%-opacity ghost of the
    /// selected entities at their move destination. GPU-only: just two DrawImage calls.
    /// </summary>
    private void RenderMovePreview(DrawingContext dc)
    {
        if (!_isMoving || _selectedHandlesSet.Count == 0 || _overlayBitmap == null) return;

        var startScreen   = CadToScreen(_moveStartCadPoint.X, _moveStartCadPoint.Y);
        var currentScreen = CadToScreen(_moveCurrentCadPoint.X, _moveCurrentCadPoint.Y);
        double dx = currentScreen.X - startScreen.X;
        double dy = currentScreen.Y - startScreen.Y;
        if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5) return;

        int w = _overlayBitmap.PixelWidth, h = _overlayBitmap.PixelHeight;
        dc.PushOpacity(0.5);
        dc.DrawImage(_overlayBitmap, new Rect(dx, dy, w, h));
        dc.Pop();
    }

    private void RenderFairwayPreview(DrawingContext dc)
    {
        if (_currentTool is not FairwayTool fairwayTool) return;

        var pts = fairwayTool.GetPreviewPoints();
        if (pts != null && pts.Count >= 2)
            dc.DrawLine(PreviewPen, CadToScreen(pts[0].X, pts[0].Y), CadToScreen(pts[1].X, pts[1].Y));

        var cursor = CadToScreen(_currentCadPoint.X, _currentCadPoint.Y);
        double r = Math.Clamp(fairwayTool.NodeRadius * _scale, 4, 20);
        dc.DrawEllipse(null, PreviewPen, cursor, r, r);

        double cs = 8;
        dc.DrawLine(PreviewPen, new WpfPoint(cursor.X - cs, cursor.Y), new WpfPoint(cursor.X + cs, cursor.Y));
        dc.DrawLine(PreviewPen, new WpfPoint(cursor.X, cursor.Y - cs), new WpfPoint(cursor.X, cursor.Y + cs));
    }

    private void RenderUnitNumberPreview(DrawingContext dc, UnitNumberTool unitTool)
    {
        var pts = unitTool.GetPreviewPoints();
        if (pts == null || pts.Count == 0) return;

        string text = unitTool.CurrentText;
        if (string.IsNullOrEmpty(text)) return;

        double fontSize = Math.Max(unitTool.TextHeight * _scale, 4);
        var screenPoint = CadToScreen(pts[0].X, pts[0].Y);

        var ft = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface("Arial"), fontSize, Brushes.Cyan, 1.0);

        dc.PushOpacity(0.7);
        dc.DrawText(ft, new WpfPoint(screenPoint.X, screenPoint.Y - ft.Height));
        dc.Pop();

        double cs = 6;
        dc.DrawLine(PreviewPen, new WpfPoint(screenPoint.X - cs, screenPoint.Y), new WpfPoint(screenPoint.X + cs, screenPoint.Y));
        dc.DrawLine(PreviewPen, new WpfPoint(screenPoint.X, screenPoint.Y - cs), new WpfPoint(screenPoint.X, screenPoint.Y + cs));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private WpfPoint CadToScreen(double cadX, double cadY) =>
        new WpfPoint((cadX + _offset.X) * _scale, (-cadY + _offset.Y) * _scale);

    private bool SelectionMatchesCache()
    {
        if (_cachedSelectedHandles == null) return _selectedHandlesSet.Count == 0;
        return _cachedSelectedHandles.SetEquals(_selectedHandlesSet);
    }

    private bool HiddenLayersMatchCache()
    {
        if (_cachedHiddenLayers == null) return _currentHiddenLayers.Count == 0;
        return _cachedHiddenLayers.SetEquals(_currentHiddenLayers);
    }

    private bool HighlightMatchesCache()
    {
        var cur = HighlightedPathHandles;
        if (_cachedHighlightHandles == null) return cur == null || cur.Count == 0;
        if (cur == null) return _cachedHighlightHandles.Count == 0;
        return _cachedHighlightHandles.SetEquals(cur);
    }

    private Rect CalculateViewportBounds()
    {
        var tl = ScreenToCad(new WpfPoint(0, 0));
        var br = ScreenToCad(new WpfPoint(ActualWidth, ActualHeight));
        return new Rect(
            Math.Min(tl.X, br.X), Math.Min(tl.Y, br.Y),
            Math.Abs(br.X - tl.X), Math.Abs(br.Y - tl.Y));
    }

    // ── Dependency property callbacks ────────────────────────────────────────

    private static void OnEntitiesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas c) { c.CalculateExtents(); c.RebuildSpatialIndex(); c.InvalidateCache(); c.Render(); }
    }

    private static void OnSelectedHandlesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas c)
        {
            c._selectedHandlesSet = c.SelectedHandles?.ToHashSet() ?? new HashSet<ulong>();
            c._overlayValid = false; // selection changed → overlay needs rebuild
            c.Render();
        }
    }

    private static void OnHiddenLayersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas c)
        {
            c._currentHiddenLayers = c.HiddenLayers?.ToHashSet() ?? new HashSet<string>();
            c.InvalidateCache();
            c.Render();
        }
    }

    private static void OnLockedLayersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas c) c._lockedLayersSet = c.LockedLayers?.ToHashSet() ?? new HashSet<string>();
    }

    private static void OnLockedHandlesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas c) c._lockedHandlesSet = c.LockedHandles?.ToHashSet() ?? new HashSet<ulong>();
    }

    private static void OnExtentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas c && e.NewValue is Extents ext) c._extents = ext;
    }

    private static void OnDrawingModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas c) c.UpdateDrawingTool();
    }

    private static void OnGridSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas c)
        {
            if (e.OldValue is GridSnapSettings old) old.PropertyChanged -= c.OnGridSettingsPropertyChanged;
            if (e.NewValue is GridSnapSettings nw)  nw.PropertyChanged  += c.OnGridSettingsPropertyChanged;
            c.Render();
        }
    }

    private static void OnViewTransformChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas c)
        {
            c._flipX = c.FlipX; c._flipY = c.FlipY; c._viewRotation = c.ViewRotation;
            c.InvalidateCache();
            c.Render();
        }
    }

    private void OnGridSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => Render();

    // ── Drawing tool management ──────────────────────────────────────────────

    public void SetDrawingTool(IDrawingTool? tool)
    {
        if (_currentTool != null)
        {
            _currentTool.Completed -= OnDrawingToolCompleted;
            _currentTool.Cancelled -= OnDrawingToolCancelled;
            _currentTool.Reset();
        }
        _currentTool = tool;
        if (_currentTool != null)
        {
            _currentTool.Completed += OnDrawingToolCompleted;
            _currentTool.Cancelled += OnDrawingToolCancelled;
        }
        UpdateCursor();
        UpdatePreviewVisual();
        Render();
    }


    private void UpdateDrawingTool()
    {
        IDrawingTool? tool = DrawingMode switch
        {
            DrawingMode.DrawLine       => new LineTool(),
            DrawingMode.DrawPolyline   => new PolylineTool(),
            DrawingMode.DrawPolygon    => new PolygonTool(),
            DrawingMode.ZoomToArea     => new ZoomAreaTool(),
            DrawingMode.PlaceUnitNumber => new UnitNumberTool(),
            DrawingMode.DrawFairway    => new FairwayTool(),
            _ => null
        };
        SetDrawingTool(tool);
    }

    private void OnDrawingToolCompleted(object? sender, DrawingCompletedEventArgs e)
    {
        DrawingCompleted?.Invoke(this, e);
        if (_currentTool is not FairwayTool) _currentTool?.Reset();
        UpdatePreviewVisual();
        Render();
    }

    private void OnDrawingToolCancelled(object? sender, EventArgs e)
    {
        UpdatePreviewVisual();
        Render();
    }

    private void UpdateCursor()
    {
        Cursor = DrawingMode switch
        {
            DrawingMode.Pan => Cursors.Hand,
            DrawingMode.DrawLine or DrawingMode.DrawPolyline or DrawingMode.DrawPolygon
                or DrawingMode.ZoomToArea or DrawingMode.PlaceUnitNumber or DrawingMode.DrawFairway
                => Cursors.Cross,
            DrawingMode.Transform => Cursors.Arrow,
            _ => Cursors.Arrow
        };
    }

    public void ConfigureUnitNumberTool(string prefix, int nextNumber, string format, double textHeight)
    {
        if (_currentTool is UnitNumberTool t)
        {
            t.Prefix = prefix; t.NextNumber = nextNumber;
            t.FormatString = format; t.TextHeight = textHeight;
            Render();
        }
    }

    public void ConfigureFairwayTool(List<(ulong handle, double x, double y)> existingNodes,
        double snapDistance = 5.0, double nodeRadius = 2.0)
    {
        if (_currentTool is FairwayTool ft)
        {
            ft.SetExistingNodes(existingNodes);
            ft.SnapDistance = snapDistance;
            ft.NodeRadius = nodeRadius;
        }
    }

    // ── Spatial index ────────────────────────────────────────────────────────

    private void CalculateExtents()
    {
        var entities = Entities;
        if (entities == null) { _extents = null; return; }
        _extents = new Extents();
        foreach (var e in entities) _extents.Expand(e);
    }

    public void RebuildSpatialIndex()
    {
        var entities = Entities;
        if (entities == null) { _spatialGrid = null; _entityByHandle = null; return; }

        var list = entities.ToList();
        _entityByHandle = new Dictionary<ulong, Entity>(list.Count);
        foreach (var e in list) _entityByHandle[e.Handle] = e;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        bool hasBounds = false;

        foreach (var e in list)
        {
            var b = BoundingBoxHelper.GetBounds(e);
            if (!b.HasValue) continue;
            hasBounds = true;
            minX = Math.Min(minX, b.Value.Left);  minY = Math.Min(minY, b.Value.Top);
            maxX = Math.Max(maxX, b.Value.Right); maxY = Math.Max(maxY, b.Value.Bottom);
        }

        _spatialGrid = hasBounds && maxX > minX && maxY > minY
            ? SpatialGrid.Build(list, new Rect(minX, minY, maxX - minX, maxY - minY))
            : null;
    }

    // ── Input handlers ───────────────────────────────────────────────────────

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_isMarqueeSelecting) { _isMarqueeSelecting = false; ReleaseMouseCapture(); UpdatePreviewVisual(); Render(); e.Handled = true; return; }
            if (_isMoving)           { _isMoving = false;           ReleaseMouseCapture(); UpdatePreviewVisual(); Render(); e.Handled = true; return; }

            if (_isTransformDragging)
            {
                _isTransformDragging = false;
                _transformScaleX = 1.0;
                _transformScaleY = 1.0;
                _transformCurrentAngle = 0;
                ReleaseMouseCapture();
                UpdatePreviewVisual();
                Render();
                e.Handled = true;
                return;
            }
        }
        if (_currentTool != null) { _currentTool.OnKeyDown(e.Key); UpdatePreviewVisual(); Render(); e.Handled = true; }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        => ZoomAtPoint(e.GetPosition(this), e.Delta > 0 ? 1.1 : 1.0 / 1.1);

    private void ZoomAtPoint(WpfPoint screenPoint, double factor)
    {
        var cad = ScreenToCad(screenPoint);
        _scale = Math.Max(0.001, Math.Min(1000, _scale * factor));
        _offset = new WpfPoint(screenPoint.X / _scale - cad.X, screenPoint.Y / _scale + cad.Y);
        // Invalidate so the cache is rebuilt at the new scale.
        // With InvalidateVisual() coalescing, rapid scroll events trigger only one rebuild.
        InvalidateCache();
        Render();
        UpdatePreviewVisual();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        if (e.ClickCount == 2 && DrawingMode == DrawingMode.Select) HandleDoubleClick(e);

        var screenPoint = e.GetPosition(this);

        if (_currentTool != null && DrawingMode != DrawingMode.Select && DrawingMode != DrawingMode.Pan)
        {
            _currentTool.OnMouseDown(GetSnappedCadPoint(screenPoint), MouseButton.Left);
            Render(); return;
        }

        if (IsPanMode || DrawingMode == DrawingMode.Pan) { StartPan(screenPoint); return; }

        // Transform mode - handle hit testing
        if (DrawingMode == DrawingMode.Transform && _selectedHandlesSet.Count > 0)
        {
            var bbox = GetSelectedEntitiesBoundingBox();
            if (bbox.HasValue)
            {
                var bboxScreen = CadBoundsToScreenRect(bbox.Value);
                var handle = TransformHitTestHelper.HitTest(screenPoint, bboxScreen);
                if (handle != TransformHandle.None)
                {
                    _isTransformDragging = true;
                    _activeTransformHandle = handle;
                    _transformStartScreenPoint = screenPoint;
                    _transformBoundingBoxCad = bbox.Value;
                    _transformBoundingBoxScreen = bboxScreen;
                    _transformScaleX = 1.0;
                    _transformScaleY = 1.0;
                    _transformCurrentAngle = 0;

                    if (handle == TransformHandle.Rotation)
                    {
                        double centerX = (_transformBoundingBoxScreen.Left + _transformBoundingBoxScreen.Right) / 2;
                        double centerY = (_transformBoundingBoxScreen.Top + _transformBoundingBoxScreen.Bottom) / 2;
                        _transformStartAngle = Math.Atan2(screenPoint.Y - centerY, screenPoint.X - centerX);
                    }

                    CaptureMouse();
                    return;
                }
            }
        }


        var cadPoint = ScreenToCad(screenPoint);
        var entities = Entities; if (entities == null) return;

        double tol = 5.0 / _scale;
        Entity? hit = null;

        if (_spatialGrid != null)
        {
            foreach (var ent in _spatialGrid.Query(cadPoint, tol))
                if (HitTestHelper.HitTest(ent, cadPoint, tol) && !IsEntityLocked(ent) && !IsWalkwayEdge(ent))
                { hit = ent; break; }
        }
        else
        {
            foreach (var ent in entities)
                if (HitTestHelper.HitTest(ent, cadPoint, tol) && !IsEntityLocked(ent) && !IsWalkwayEdge(ent))
                { hit = ent; break; }
        }

        bool addToSel = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

        if (hit != null && _selectedHandlesSet.Contains(hit.Handle))
        {
            _isMoving = true; _moveStartCadPoint = cadPoint; _moveCurrentCadPoint = cadPoint; CaptureMouse();
        }
        else if (hit != null) EntityClicked?.Invoke(this, new CadEntityClickEventArgs(hit, addToSel));
        else { _isMarqueeSelecting = true; _marqueeStartScreen = screenPoint; _marqueeCurrentScreen = screenPoint; CaptureMouse(); }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning && IsPanMode) { EndPan(); return; }

        if (_currentTool != null && _currentTool.IsDrawing)
        {
            _currentTool.OnMouseUp(GetSnappedCadPoint(e.GetPosition(this)), MouseButton.Left);
            Render(); return;
        }

        if (_isMarqueeSelecting) { CompleteMarqueeSelection(e); return; }
        if (_isMoving)           { CompleteMoveOperation(e); return; }

        if (_isTransformDragging)
        {
            CompleteTransformOperation();
            return;
        }
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var screenPoint = e.GetPosition(this);
        if (_currentTool != null && _currentTool.IsDrawing)
        {
            _currentTool.OnMouseDown(GetSnappedCadPoint(screenPoint), MouseButton.Right);
            Render(); return;
        }
        if (DrawingMode == DrawingMode.Select && HandleRightClickContextMenu(screenPoint)) return;
        StartPan(screenPoint);
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning) EndPan();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var screenPoint = e.GetPosition(this);
        var cadPoint = GetSnappedCadPoint(screenPoint);
        _currentCadPoint = cadPoint;
        CadMouseMove?.Invoke(this, new CadMouseEventArgs(cadPoint.X, cadPoint.Y));

        if (_currentTool != null && (_currentTool.IsDrawing || _currentTool is FairwayTool))
        {
            _currentTool.OnMouseMove(cadPoint);
            UpdatePreviewVisual(); // synchronous — zero vsync latency
        }

        if (_isPanning)
        {
            _offset = new WpfPoint(
                _offset.X + (screenPoint.X - _panStart.X) / _scale,
                _offset.Y + (screenPoint.Y - _panStart.Y) / _scale);
            _panStart = screenPoint;
            Render(); // entity layer (fast pan path — bitmap blit only)
            UpdatePreviewVisual();
        }

        if (_isMarqueeSelecting) { _marqueeCurrentScreen = screenPoint; UpdatePreviewVisual(); }
        if (_isMoving)           { _moveCurrentCadPoint = ScreenToCad(screenPoint); UpdatePreviewVisual(); }

        if (_isTransformDragging)
        {
            if (_activeTransformHandle == TransformHandle.Rotation)
            {
                double centerX = (_transformBoundingBoxScreen.Left + _transformBoundingBoxScreen.Right) / 2;
                double centerY = (_transformBoundingBoxScreen.Top + _transformBoundingBoxScreen.Bottom) / 2;
                double currentAngle = Math.Atan2(screenPoint.Y - centerY, screenPoint.X - centerX);
                _transformCurrentAngle = currentAngle - _transformStartAngle;
            }
            else
            {
                ComputeScaleFromHandle(screenPoint);
            }
            UpdatePreviewVisual();
        }
        else if (DrawingMode == DrawingMode.Transform && _selectedHandlesSet.Count > 0 && !_isPanning)
        {
            var bbox = GetSelectedEntitiesBoundingBox();
            if (bbox.HasValue)
            {
                var bboxScreen = CadBoundsToScreenRect(bbox.Value);
                var prevHovered = _hoveredTransformHandle;
                _hoveredTransformHandle = TransformHitTestHelper.HitTest(screenPoint, bboxScreen);
                if (_hoveredTransformHandle != prevHovered)
                {
                    UpdateTransformCursor();
                    UpdatePreviewVisual();
                }
            }
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) { InvalidateCache(); Render(); UpdatePreviewVisual(); }

    // ── Selection / move completion ──────────────────────────────────────────

    private void CompleteMarqueeSelection(MouseButtonEventArgs e)
    {
        _isMarqueeSelecting = false;
        ReleaseMouseCapture();
        var screenPoint = e.GetPosition(this);
        bool add = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

        double drag = Math.Max(Math.Abs(screenPoint.X - _marqueeStartScreen.X),
                               Math.Abs(screenPoint.Y - _marqueeStartScreen.Y));
        if (drag < 3) { EntityClicked?.Invoke(this, new CadEntityClickEventArgs(null, add)); Render(); return; }

        var cadStart = ScreenToCad(_marqueeStartScreen);
        var cadEnd   = ScreenToCad(screenPoint);
        var selRect  = new Rect(
            Math.Min(cadStart.X, cadEnd.X), Math.Min(cadStart.Y, cadEnd.Y),
            Math.Abs(cadEnd.X - cadStart.X), Math.Abs(cadEnd.Y - cadStart.Y));
        bool isWindow = cadEnd.X > cadStart.X;

        var entities = Entities; if (entities == null) { Render(); return; }
        var matched = new List<Entity>();

        foreach (var ent in entities)
        {
            if (HiddenLayers != null && ent.Layer != null && HiddenLayers.Any(l => l == ent.Layer.Name)) continue;
            if (IsEntityLocked(ent) || IsWalkwayEdge(ent)) continue;
            var b = BoundingBoxHelper.GetBounds(ent); if (b == null) continue;
            if (isWindow ? selRect.Contains(b.Value) : selRect.IntersectsWith(b.Value))
                matched.Add(ent);
        }

        MarqueeSelectionCompleted?.Invoke(this, new MarqueeSelectionEventArgs(matched, add));
        UpdatePreviewVisual();
        Render();
    }

    private void CompleteMoveOperation(MouseButtonEventArgs e)
    {
        _isMoving = false;
        ReleaseMouseCapture();
        var cadEnd = ScreenToCad(e.GetPosition(this));
        double dx = cadEnd.X - _moveStartCadPoint.X, dy = cadEnd.Y - _moveStartCadPoint.Y;
        double thr = 1.0 / _scale;
        if (Math.Abs(dx) >= thr || Math.Abs(dy) >= thr)
            MoveCompleted?.Invoke(this, new MoveCompletedEventArgs(dx, dy));
        UpdatePreviewVisual();
        Render();
    }

    private void HandleDoubleClick(MouseButtonEventArgs e)
    {
        var cad = ScreenToCad(e.GetPosition(this));
        double tol = 5.0 / _scale;
        var entities = Entities; if (entities == null) return;

        Entity? hit = null;
        var candidates = _spatialGrid != null
            ? (IEnumerable<Entity>)_spatialGrid.Query(cad, tol)
            : entities;

        foreach (var ent in candidates)
            if (ent is MText mt && mt.Layer?.Name == CadDocumentModel.UnitNumbersLayerName
                && HitTestHelper.HitTest(ent, cad, tol))
            { hit = ent; break; }

        if (hit != null) { EntityDoubleClicked?.Invoke(this, new CadEntityClickEventArgs(hit, false)); e.Handled = true; }
    }

    private bool HandleRightClickContextMenu(WpfPoint screenPoint)
    {
        if (DrawingMode != DrawingMode.Select) return false;
        var cad = ScreenToCad(screenPoint);
        double tol = 5.0 / _scale;
        Entity? hit = null;

        if (_spatialGrid != null)
            foreach (var ent in _spatialGrid.Query(cad, tol))
                if (ent is Circle c and not Arc && c.Layer?.Name == Models.CadDocumentModel.WalkwaysLayerName
                    && HitTestHelper.HitTest(ent, cad, tol))
                { hit = ent; break; }

        if (hit is Circle wc)
        {
            var menu = new System.Windows.Controls.ContextMenu();
            var item = new System.Windows.Controls.MenuItem
            {
                Header = wc.Color.Index == 3 ? "Set as Regular Node" : "Set as Entrance"
            };
            item.Click += (_, _) => ToggleEntranceRequested?.Invoke(this, new ToggleEntranceEventArgs(wc.Handle));
            menu.Items.Add(item);
            menu.PlacementTarget = this;
            menu.IsOpen = true;
            return true;
        }
        return false;
    }

    private void StartPan(WpfPoint start)
    {
        _isPanning = true; _panStart = start; CaptureMouse(); Cursor = Cursors.Hand;
    }

    private void EndPan()
    {
        _isPanning = false; ReleaseMouseCapture();
        Cursor = IsPanMode ? Cursors.Hand : Cursors.Arrow;
        InvalidateCache();
        Render();
        UpdatePreviewVisual();
    }

    private bool IsEntityLocked(Entity e) =>
        _lockedHandlesSet.Contains(e.Handle) || (e.Layer != null && _lockedLayersSet.Contains(e.Layer.Name));

    private static bool IsWalkwayEdge(Entity e) =>
        e is Line && e.Layer?.Name == Models.CadDocumentModel.WalkwaysLayerName;

    private Rect? GetSelectedEntitiesBoundingBox()
    {
        if (_selectedHandlesSet.Count == 0 || _entityByHandle == null)
            return null;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        bool hasBounds = false;

        foreach (var handle in _selectedHandlesSet)
        {
            if (_entityByHandle.TryGetValue(handle, out var entity))
            {
                var bounds = BoundingBoxHelper.GetBounds(entity);
                if (bounds.HasValue)
                {
                    hasBounds = true;
                    minX = Math.Min(minX, bounds.Value.Left);
                    minY = Math.Min(minY, bounds.Value.Top);
                    maxX = Math.Max(maxX, bounds.Value.Right);
                    maxY = Math.Max(maxY, bounds.Value.Bottom);
                }
            }
        }

        return hasBounds ? new Rect(minX, minY, maxX - minX, maxY - minY) : null;
    }

    private Rect CadBoundsToScreenRect(Rect cadBounds)
    {
        var topLeft = CadToScreen(cadBounds.Left, cadBounds.Bottom);
        var bottomRight = CadToScreen(cadBounds.Right, cadBounds.Top);
        double left = Math.Min(topLeft.X, bottomRight.X);
        double top = Math.Min(topLeft.Y, bottomRight.Y);
        double width = Math.Abs(bottomRight.X - topLeft.X);
        double height = Math.Abs(bottomRight.Y - topLeft.Y);
        return new Rect(left, top, width, height);
    }

    private void ComputeScaleFromHandle(WpfPoint currentScreen)
    {
        double bboxW = _transformBoundingBoxScreen.Width;
        double bboxH = _transformBoundingBoxScreen.Height;
        if (bboxW < 1) bboxW = 1;
        if (bboxH < 1) bboxH = 1;

        double dx = currentScreen.X - _transformStartScreenPoint.X;
        double dy = currentScreen.Y - _transformStartScreenPoint.Y;

        bool uniformConstraint = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        switch (_activeTransformHandle)
        {
            case TransformHandle.BottomRight:
                _transformScaleX = (bboxW + dx) / bboxW;
                _transformScaleY = (bboxH + dy) / bboxH;
                break;
            case TransformHandle.BottomLeft:
                _transformScaleX = (bboxW - dx) / bboxW;
                _transformScaleY = (bboxH + dy) / bboxH;
                break;
            case TransformHandle.TopRight:
                _transformScaleX = (bboxW + dx) / bboxW;
                _transformScaleY = (bboxH - dy) / bboxH;
                break;
            case TransformHandle.TopLeft:
                _transformScaleX = (bboxW - dx) / bboxW;
                _transformScaleY = (bboxH - dy) / bboxH;
                break;
            case TransformHandle.MiddleRight:
                _transformScaleX = (bboxW + dx) / bboxW;
                _transformScaleY = 1.0;
                break;
            case TransformHandle.MiddleLeft:
                _transformScaleX = (bboxW - dx) / bboxW;
                _transformScaleY = 1.0;
                break;
            case TransformHandle.BottomCenter:
                _transformScaleX = 1.0;
                _transformScaleY = (bboxH + dy) / bboxH;
                break;
            case TransformHandle.TopCenter:
                _transformScaleX = 1.0;
                _transformScaleY = (bboxH - dy) / bboxH;
                break;
        }

        // Prevent zero/negative scale
        if (Math.Abs(_transformScaleX) < 0.01) _transformScaleX = 0.01;
        if (Math.Abs(_transformScaleY) < 0.01) _transformScaleY = 0.01;

        if (uniformConstraint && (_activeTransformHandle == TransformHandle.TopLeft ||
            _activeTransformHandle == TransformHandle.TopRight ||
            _activeTransformHandle == TransformHandle.BottomLeft ||
            _activeTransformHandle == TransformHandle.BottomRight))
        {
            double uniform = Math.Max(_transformScaleX, _transformScaleY);
            _transformScaleX = uniform;
            _transformScaleY = uniform;
        }
    }

    private void CompleteTransformOperation()
    {
        _isTransformDragging = false;
        ReleaseMouseCapture();

        bool isRotation = _activeTransformHandle == TransformHandle.Rotation;
        bool hasSignificantTransform = isRotation
            ? Math.Abs(_transformCurrentAngle) > 0.001
            : Math.Abs(_transformScaleX - 1.0) > 0.001 || Math.Abs(_transformScaleY - 1.0) > 0.001;

        if (hasSignificantTransform)
        {
            double pivotCadX = (_transformBoundingBoxCad.Left + _transformBoundingBoxCad.Right) / 2;
            double pivotCadY = (_transformBoundingBoxCad.Top + _transformBoundingBoxCad.Bottom) / 2;

            if (!isRotation)
            {
                // For resize, determine pivot based on opposite corner/edge
                var (px, py) = GetResizePivot(_activeTransformHandle, _transformBoundingBoxCad);
                pivotCadX = px;
                pivotCadY = py;
            }

            var args = new TransformCompletedEventArgs(
                pivotCadX, pivotCadY,
                isRotation ? null : _transformScaleX,
                isRotation ? null : _transformScaleY,
                isRotation ? -_transformCurrentAngle : null);

            TransformCompleted?.Invoke(this, args);
        }

        _transformScaleX = 1.0;
        _transformScaleY = 1.0;
        _transformCurrentAngle = 0;
        Render();
    }

    private static (double x, double y) GetResizePivot(TransformHandle handle, Rect bbox)
    {
        // The CAD bounding box Rect uses WPF convention: Top = minCADY (screen bottom),
        // Bottom = maxCADY (screen top). Pivot is always the opposite corner/edge.
        return handle switch
        {
            TransformHandle.TopLeft => (bbox.Right, bbox.Top),
            TransformHandle.TopCenter => ((bbox.Left + bbox.Right) / 2, bbox.Top),
            TransformHandle.TopRight => (bbox.Left, bbox.Top),
            TransformHandle.MiddleLeft => (bbox.Right, (bbox.Top + bbox.Bottom) / 2),
            TransformHandle.MiddleRight => (bbox.Left, (bbox.Top + bbox.Bottom) / 2),
            TransformHandle.BottomLeft => (bbox.Right, bbox.Bottom),
            TransformHandle.BottomCenter => ((bbox.Left + bbox.Right) / 2, bbox.Bottom),
            TransformHandle.BottomRight => (bbox.Left, bbox.Bottom),
            _ => ((bbox.Left + bbox.Right) / 2, (bbox.Top + bbox.Bottom) / 2)
        };
    }

    private void RenderTransformHandles(DrawingContext dc)
    {
        if (DrawingMode != DrawingMode.Transform || _selectedHandlesSet.Count == 0 || _isTransformDragging)
            return;

        var bbox = GetSelectedEntitiesBoundingBox();
        if (!bbox.HasValue)
            return;

        var bboxScreen = CadBoundsToScreenRect(bbox.Value);
        var handles = TransformHitTestHelper.GetHandlePositions(bboxScreen);

        // Draw dashed bounding box
        var dashedPen = new Pen(Brushes.CornflowerBlue, 1.0) { DashStyle = DashStyles.Dash };
        dashedPen.Freeze();
        dc.DrawRectangle(null, dashedPen, bboxScreen);

        // Draw rotation handle line and circle
        var topCenter = handles[TransformHandle.TopCenter];
        var rotationPos = handles[TransformHandle.Rotation];
        var rotLinePen = new Pen(Brushes.CornflowerBlue, 1.0);
        rotLinePen.Freeze();
        dc.DrawLine(rotLinePen, topCenter, rotationPos);

        double handleSize = 4.0;
        var defaultBrush = Brushes.White;
        var hoverBrush = Brushes.Yellow;
        var handlePen = new Pen(Brushes.Gray, 1.0);
        handlePen.Freeze();

        foreach (var (handle, pos) in handles)
        {
            var brush = handle == _hoveredTransformHandle ? hoverBrush : defaultBrush;
            if (handle == TransformHandle.Rotation)
            {
                dc.DrawEllipse(brush, handlePen, pos, handleSize + 1, handleSize + 1);
            }
            else
            {
                dc.DrawRectangle(brush, handlePen,
                    new Rect(pos.X - handleSize, pos.Y - handleSize, handleSize * 2, handleSize * 2));
            }
        }
    }

    private void RenderTransformPreview(DrawingContext dc)
    {
        if (!_isTransformDragging || _selectedHandlesSet.Count == 0 || _overlayBitmap == null)
            return;

        bool isRotation = _activeTransformHandle == TransformHandle.Rotation;
        int w = _overlayBitmap.PixelWidth, h = _overlayBitmap.PixelHeight;

        if (isRotation)
        {
            double cx = (_transformBoundingBoxScreen.Left + _transformBoundingBoxScreen.Right) / 2;
            double cy = (_transformBoundingBoxScreen.Top + _transformBoundingBoxScreen.Bottom) / 2;
            dc.PushTransform(new RotateTransform(
                _transformCurrentAngle * (180.0 / Math.PI), cx, cy));
        }
        else
        {
            var (pivotCadX, pivotCadY) = GetResizePivot(_activeTransformHandle, _transformBoundingBoxCad);
            var pivotScreen = CadToScreen(pivotCadX, pivotCadY);
            var tg = new TransformGroup();
            tg.Children.Add(new TranslateTransform(-pivotScreen.X, -pivotScreen.Y));
            tg.Children.Add(new ScaleTransform(_transformScaleX, _transformScaleY));
            tg.Children.Add(new TranslateTransform(pivotScreen.X, pivotScreen.Y));
            dc.PushTransform(tg);
        }

        dc.PushOpacity(0.5);
        dc.DrawImage(_overlayBitmap, new Rect(0, 0, w, h));
        dc.Pop(); // opacity
        dc.Pop(); // transform
    }

    private void UpdateTransformCursor()
    {
        Cursor = _hoveredTransformHandle switch
        {
            TransformHandle.TopLeft or TransformHandle.BottomRight => Cursors.SizeNWSE,
            TransformHandle.TopRight or TransformHandle.BottomLeft => Cursors.SizeNESW,
            TransformHandle.MiddleLeft or TransformHandle.MiddleRight => Cursors.SizeWE,
            TransformHandle.TopCenter or TransformHandle.BottomCenter => Cursors.SizeNS,
            TransformHandle.Rotation => Cursors.Hand,
            _ => Cursors.Arrow
        };
    }
}

// ── Event arg classes ────────────────────────────────────────────────────────

public class CadMouseEventArgs(double x, double y) : EventArgs
{
    public double X { get; } = x;
    public double Y { get; } = y;
}

public class CadEntityClickEventArgs(Entity? entity, bool addToSelection) : EventArgs
{
    public Entity? Entity { get; } = entity;
    public bool AddToSelection { get; } = addToSelection;
}

public class MarqueeSelectionEventArgs(IReadOnlyList<Entity> selectedEntities, bool addToSelection) : EventArgs
{
    public IReadOnlyList<Entity> SelectedEntities { get; } = selectedEntities;
    public bool AddToSelection { get; } = addToSelection;
}

public class MoveCompletedEventArgs(double deltaX, double deltaY) : EventArgs
{
    public double DeltaX { get; } = deltaX;
    public double DeltaY { get; } = deltaY;
}

public class TransformCompletedEventArgs : EventArgs
{
    public double PivotX { get; }
    public double PivotY { get; }
    public double? ScaleX { get; }
    public double? ScaleY { get; }
    public double? AngleRadians { get; }

    public TransformCompletedEventArgs(double pivotX, double pivotY, double? scaleX, double? scaleY, double? angleRadians)
    {
        PivotX = pivotX;
        PivotY = pivotY;
        ScaleX = scaleX;
        ScaleY = scaleY;
        AngleRadians = angleRadians;
    }
}

public class ToggleEntranceEventArgs(ulong handle) : EventArgs
{
    public ulong Handle { get; } = handle;
}
