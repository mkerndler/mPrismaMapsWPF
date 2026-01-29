using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Rendering;
using WpfPoint = System.Windows.Point;

namespace mPrismaMapsWPF.Controls;

public class CadCanvas : FrameworkElement
{
    private readonly RenderService _renderService;
    private readonly DrawingVisual _drawingVisual;
    private readonly VisualCollection _visuals;

    private WpfPoint _panStart;
    private bool _isPanning;
    private WpfPoint _offset;
    private double _scale = 1.0;
    private Extents? _extents;

    public CadCanvas()
    {
        _renderService = new RenderService();
        _drawingVisual = new DrawingVisual();
        _visuals = new VisualCollection(this) { _drawingVisual };

        ClipToBounds = true;
        Focusable = true;

        MouseWheel += OnMouseWheel;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseRightButtonDown += OnMouseRightButtonDown;
        MouseRightButtonUp += OnMouseRightButtonUp;
        MouseMove += OnMouseMove;
        SizeChanged += OnSizeChanged;
    }

    public static readonly DependencyProperty EntitiesProperty =
        DependencyProperty.Register(
            nameof(Entities),
            typeof(IEnumerable<Entity>),
            typeof(CadCanvas),
            new PropertyMetadata(null, OnEntitiesChanged));

    public IEnumerable<Entity>? Entities
    {
        get => (IEnumerable<Entity>?)GetValue(EntitiesProperty);
        set => SetValue(EntitiesProperty, value);
    }

    public static readonly DependencyProperty SelectedHandlesProperty =
        DependencyProperty.Register(
            nameof(SelectedHandles),
            typeof(IEnumerable<ulong>),
            typeof(CadCanvas),
            new PropertyMetadata(null, OnSelectedHandlesChanged));

    public IEnumerable<ulong>? SelectedHandles
    {
        get => (IEnumerable<ulong>?)GetValue(SelectedHandlesProperty);
        set => SetValue(SelectedHandlesProperty, value);
    }

    public static readonly DependencyProperty HiddenLayersProperty =
        DependencyProperty.Register(
            nameof(HiddenLayers),
            typeof(IEnumerable<string>),
            typeof(CadCanvas),
            new PropertyMetadata(null, OnHiddenLayersChanged));

    public IEnumerable<string>? HiddenLayers
    {
        get => (IEnumerable<string>?)GetValue(HiddenLayersProperty);
        set => SetValue(HiddenLayersProperty, value);
    }

    public static readonly DependencyProperty IsPanModeProperty =
        DependencyProperty.Register(
            nameof(IsPanMode),
            typeof(bool),
            typeof(CadCanvas),
            new PropertyMetadata(false));

    public bool IsPanMode
    {
        get => (bool)GetValue(IsPanModeProperty);
        set => SetValue(IsPanModeProperty, value);
    }

    public static readonly DependencyProperty ExtentsProperty =
        DependencyProperty.Register(
            nameof(Extents),
            typeof(Extents),
            typeof(CadCanvas),
            new PropertyMetadata(null, OnExtentsChanged));

    public Extents? Extents
    {
        get => (Extents?)GetValue(ExtentsProperty);
        set => SetValue(ExtentsProperty, value);
    }

    public event EventHandler<CadMouseEventArgs>? CadMouseMove;
    public event EventHandler<CadEntityClickEventArgs>? EntityClicked;

    public double Scale
    {
        get => _scale;
        set
        {
            _scale = Math.Max(0.001, Math.Min(1000, value));
            Render();
        }
    }

    public void ZoomToFit()
    {
        var extents = Extents ?? _extents;
        if (extents == null || !extents.IsValid || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        double margin = 50;
        double availableWidth = ActualWidth - margin * 2;
        double availableHeight = ActualHeight - margin * 2;

        double scaleX = availableWidth / extents.Width;
        double scaleY = availableHeight / extents.Height;
        _scale = Math.Min(scaleX, scaleY);

        _offset = new WpfPoint(
            -extents.CenterX + (ActualWidth / 2) / _scale,
            extents.CenterY + (ActualHeight / 2) / _scale
        );

        Render();
    }

    public void ZoomIn()
    {
        WpfPoint center = new(ActualWidth / 2, ActualHeight / 2);
        ZoomAtPoint(center, 1.25);
    }

    public void ZoomOut()
    {
        WpfPoint center = new(ActualWidth / 2, ActualHeight / 2);
        ZoomAtPoint(center, 1.0 / 1.25);
    }

    public WpfPoint ScreenToCad(WpfPoint screenPoint)
    {
        return new WpfPoint(
            screenPoint.X / _scale - _offset.X,
            -(screenPoint.Y / _scale - _offset.Y)
        );
    }

    public void Render()
    {
        using var dc = _drawingVisual.RenderOpen();

        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, ActualWidth, ActualHeight));

        var entities = Entities;
        if (entities == null)
            return;

        var renderContext = new RenderContext
        {
            Scale = _scale,
            Offset = _offset,
            DefaultColor = Colors.White,
            LineThickness = 1.0,
            ShowSelection = true,
            ViewportBounds = CalculateViewportBounds()
        };

        if (SelectedHandles != null)
        {
            foreach (var handle in SelectedHandles)
            {
                renderContext.SelectedHandles.Add(handle);
            }
        }

        if (HiddenLayers != null)
        {
            foreach (var layer in HiddenLayers)
            {
                renderContext.HiddenLayers.Add(layer);
            }
        }

        _renderService.RenderEntities(dc, entities, renderContext);
    }

    private Rect CalculateViewportBounds()
    {
        // Convert screen corners to CAD coordinates
        var topLeft = ScreenToCad(new WpfPoint(0, 0));
        var bottomRight = ScreenToCad(new WpfPoint(ActualWidth, ActualHeight));

        double minX = Math.Min(topLeft.X, bottomRight.X);
        double maxX = Math.Max(topLeft.X, bottomRight.X);
        double minY = Math.Min(topLeft.Y, bottomRight.Y);
        double maxY = Math.Max(topLeft.Y, bottomRight.Y);

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index) => _visuals[index];

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(Brushes.Black, null, new Rect(RenderSize));
    }

    private static void OnEntitiesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas canvas)
        {
            canvas.CalculateExtents();
            canvas.Render();
        }
    }

    private static void OnSelectedHandlesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas canvas)
        {
            canvas.Render();
        }
    }

    private static void OnHiddenLayersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas canvas)
        {
            canvas.Render();
        }
    }

    private static void OnExtentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas canvas && e.NewValue is Extents extents)
        {
            canvas._extents = extents;
        }
    }

    private void CalculateExtents()
    {
        var entities = Entities;
        if (entities == null)
        {
            _extents = null;
            return;
        }

        _extents = new Extents();
        foreach (var entity in entities)
        {
            _extents.Expand(entity);
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        double factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
        WpfPoint mousePos = e.GetPosition(this);
        ZoomAtPoint(mousePos, factor);
    }

    private void ZoomAtPoint(WpfPoint screenPoint, double factor)
    {
        var cadPoint = ScreenToCad(screenPoint);

        _scale *= factor;
        _scale = Math.Max(0.001, Math.Min(1000, _scale));

        _offset = new WpfPoint(
            screenPoint.X / _scale - cadPoint.X,
            screenPoint.Y / _scale + cadPoint.Y
        );

        Render();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Focus();

        if (IsPanMode)
        {
            StartPan(e.GetPosition(this));
            return;
        }

        var screenPoint = e.GetPosition(this);
        var cadPoint = ScreenToCad(screenPoint);

        var entities = Entities;
        if (entities == null)
            return;

        double tolerance = 5.0 / _scale;

        Entity? hitEntity = null;
        foreach (var entity in entities)
        {
            if (HitTestHelper.HitTest(entity, cadPoint, tolerance))
            {
                hitEntity = entity;
                break;
            }
        }

        bool addToSelection = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        EntityClicked?.Invoke(this, new CadEntityClickEventArgs(hitEntity, addToSelection));
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning && IsPanMode)
        {
            EndPan();
        }
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        StartPan(e.GetPosition(this));
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndPan();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var screenPoint = e.GetPosition(this);
        var cadPoint = ScreenToCad(screenPoint);

        CadMouseMove?.Invoke(this, new CadMouseEventArgs(cadPoint.X, cadPoint.Y));

        if (_isPanning)
        {
            var currentPos = screenPoint;
            var delta = new WpfPoint(
                (currentPos.X - _panStart.X) / _scale,
                (currentPos.Y - _panStart.Y) / _scale
            );

            _offset = new WpfPoint(_offset.X + delta.X, _offset.Y + delta.Y);
            _panStart = currentPos;

            Render();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Render();
    }

    private void StartPan(WpfPoint startPoint)
    {
        _isPanning = true;
        _panStart = startPoint;
        CaptureMouse();
        Cursor = Cursors.Hand;
    }

    private void EndPan()
    {
        _isPanning = false;
        ReleaseMouseCapture();
        Cursor = IsPanMode ? Cursors.Hand : Cursors.Arrow;
    }
}

public class CadMouseEventArgs : EventArgs
{
    public double X { get; }
    public double Y { get; }

    public CadMouseEventArgs(double x, double y)
    {
        X = x;
        Y = y;
    }
}

public class CadEntityClickEventArgs : EventArgs
{
    public Entity? Entity { get; }
    public bool AddToSelection { get; }

    public CadEntityClickEventArgs(Entity? entity, bool addToSelection)
    {
        Entity = entity;
        AddToSelection = addToSelection;
    }
}
