using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace StdfViewer
{
    public sealed class WaferMapView : FrameworkElement
    {
        private static readonly SolidColorBrush WaferFill = Frozen(0xF8, 0xFA, 0xFC);
        private static readonly SolidColorBrush CirclePenBrush = Frozen(0xE3, 0xE7, 0xEC);
        private static readonly SolidColorBrush MutedBrush = Frozen(0x6B, 0x72, 0x80);
        private static readonly SolidColorBrush LabelFill = Frozen(0xFF, 0xFF, 0xFF);
        private static readonly SolidColorBrush HoverBrush = Frozen(0x2F, 0x6F, 0xED);

        private readonly List<KeyValuePair<SolidColorBrush, StreamGeometry>> _shapes =
            new List<KeyValuePair<SolidColorBrush, StreamGeometry>>();

        private WaferMapWafer _wafer;
        private char _notch = 'D';
        private bool _tooLarge;
        private int _cols;
        private int _rows;
        private double _cellWidth;
        private double _cellHeight;
        private double _gap;
        private double _gridLeft;
        private double _gridTop;
        private double _layoutWidth = -1;
        private double _layoutHeight = -1;
        private Point _circleCenter;
        private double _circleRadius;
        private WaferDie _hovered;
        private double _zoom = 1;
        private double _panX;
        private double _panY;
        private bool _dragging;
        private Point _dragStart;
        private double _dragPanX;
        private double _dragPanY;

        public WaferMapView()
        {
            ClipToBounds = true;
        }

        public static readonly DependencyProperty WaferProperty = DependencyProperty.Register(
            "Wafer", typeof(WaferMapWafer), typeof(WaferMapView),
            new FrameworkPropertyMetadata(null, OnWaferChanged));

        public static readonly DependencyProperty NotchProperty = DependencyProperty.Register(
            "Notch", typeof(char), typeof(WaferMapView),
            new FrameworkPropertyMetadata('D', OnNotchChanged));

        public static readonly DependencyProperty HoveredDieProperty = DependencyProperty.Register(
            "HoveredDie", typeof(WaferDie), typeof(WaferMapView),
            new FrameworkPropertyMetadata(null, OnHoveredDieChanged));

        public WaferMapWafer Wafer
        {
            get { return (WaferMapWafer)GetValue(WaferProperty); }
            set { SetValue(WaferProperty, value); }
        }

        public char Notch
        {
            get { return (char)GetValue(NotchProperty); }
            set { SetValue(NotchProperty, value); }
        }

        public WaferDie HoveredDie
        {
            get { return (WaferDie)GetValue(HoveredDieProperty); }
            set { SetValue(HoveredDieProperty, value); }
        }

        public bool TooLarge { get { return _tooLarge; } }

        private static void OnWaferChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (WaferMapView)d;
            view._wafer = (WaferMapWafer)e.NewValue;
            view._tooLarge = view._wafer != null && IsTooLarge(view._wafer);
            view._cellWidth = 0;
            view._cellHeight = 0;
            view._layoutWidth = -1;
            view._layoutHeight = -1;
            view._zoom = 1;
            view._panX = 0;
            view._panY = 0;
            view._dragging = false;
            view._shapes.Clear();
            view.SetCurrentValue(HoveredDieProperty, null);
            view.InvalidateVisual();
        }

        private static void OnNotchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (WaferMapView)d;
            view._notch = NormalizeNotch((char)e.NewValue);
            view.InvalidateVisual();
        }

        private static void OnHoveredDieChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (WaferMapView)d;
            view._hovered = (WaferDie)e.NewValue;
            view.InvalidateVisual();
        }

        public void ResetView()
        {
            _zoom = 1;
            _panX = 0;
            _panY = 0;
            InvalidateVisual();
        }

        public WaferDie HitTest(Point point)
        {
            if (_wafer == null)
                return null;
            EnsureLayout();
            if (_cellWidth == 0 || _cellHeight == 0 || _tooLarge)
                return null;
            Point basePoint = ScreenToBase(point);
            int col = (int)Math.Floor((basePoint.X - _gridLeft) / _cellWidth);
            int row = (int)Math.Floor((basePoint.Y - _gridTop) / _cellHeight);
            if (col < 0 || col >= _cols || row < 0 || row >= _rows)
                return null;
            return _wafer.GetDie(_wafer.MinX + col, _wafer.MaxY - row);
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
            if (_wafer == null || _wafer.Dice.Count == 0)
                return;
            EnsureLayout();
            if (_cellWidth == 0 || _cellHeight == 0 || _tooLarge)
                return;

            dc.PushTransform(new MatrixTransform(ViewMatrix()));
            dc.DrawEllipse(WaferFill, new Pen(CirclePenBrush, 1), _circleCenter, _circleRadius, _circleRadius);
            foreach (KeyValuePair<SolidColorBrush, StreamGeometry> shape in _shapes)
                dc.DrawGeometry(shape.Key, null, shape.Value);
            DrawNotch(dc);
            if (_hovered != null)
            {
                Rect rect = DieRect(_hovered);
                dc.DrawRectangle(null, new Pen(HoverBrush, 1.5), new Rect(rect.X, rect.Y, _cellWidth, _cellHeight));
            }
            dc.Pop();
            DrawAxes(dc);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_wafer == null || _tooLarge || _cellWidth == 0)
                return;
            double zoom = Math.Max(1, Math.Min(20, _zoom * Math.Pow(1.2, e.Delta / 120.0)));
            if (zoom == _zoom)
            {
                e.Handled = true;
                return;
            }
            Point mouse = e.GetPosition(this);
            Point basePoint = ScreenToBase(mouse);
            if (zoom <= 1)
            {
                _panX = 0;
                _panY = 0;
            }
            else
            {
                _panX = mouse.X - ActualWidth / 2 - (basePoint.X - ActualWidth / 2) * zoom;
                _panY = mouse.Y - ActualHeight / 2 - (basePoint.Y - ActualHeight / 2) * zoom;
            }
            _zoom = zoom;
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (_wafer == null || _tooLarge || _cellWidth == 0)
                return;
            if (e.ClickCount == 2)
            {
                _dragging = false;
                ResetView();
                e.Handled = true;
                return;
            }
            _dragging = true;
            _dragStart = e.GetPosition(this);
            _dragPanX = _panX;
            _dragPanY = _panY;
            CaptureMouse();
            Cursor = Cursors.Hand;
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!_dragging)
                return;
            _dragging = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
            e.Handled = true;
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            _dragging = false;
            Cursor = Cursors.Arrow;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                Point point = e.GetPosition(this);
                _panX = _dragPanX + point.X - _dragStart.X;
                _panY = _dragPanY + point.Y - _dragStart.Y;
                if (_hovered != null)
                    SetCurrentValue(HoveredDieProperty, null);
                InvalidateVisual();
                return;
            }
            WaferDie die = HitTest(e.GetPosition(this));
            if (!ReferenceEquals(die, _hovered))
                SetCurrentValue(HoveredDieProperty, die);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hovered != null)
                SetCurrentValue(HoveredDieProperty, null);
        }

        private void EnsureLayout()
        {
            if (_wafer == null || _wafer.Dice.Count == 0 || _tooLarge)
                return;
            if (_cellWidth > 0 && _cellHeight > 0 && _layoutWidth == ActualWidth && _layoutHeight == ActualHeight)
                return;

            _shapes.Clear();
            _cellWidth = 0;
            _cellHeight = 0;
            _layoutWidth = ActualWidth;
            _layoutHeight = ActualHeight;

            _cols = _wafer.MaxX - _wafer.MinX + 1;
            _rows = _wafer.MaxY - _wafer.MinY + 1;
            double availableWidth = ActualWidth - 48;
            double availableHeight = ActualHeight - 48;
            if (availableWidth < 1 || availableHeight < 1)
                return;
            double side = Math.Min(availableWidth, availableHeight);
            _gridLeft = (ActualWidth - side) / 2;
            _gridTop = (ActualHeight - side) / 2;
            _cellWidth = side / _cols;
            _cellHeight = side / _rows;
            _gap = Math.Min(_cellWidth, _cellHeight) >= 4 ? 1 : 0;
            _circleCenter = new Point(ActualWidth / 2, ActualHeight / 2);
            _circleRadius = side / 2;

            var byBin = new Dictionary<int, List<WaferDie>>();
            foreach (WaferDie die in _wafer.Dice)
            {
                List<WaferDie> list;
                if (!byBin.TryGetValue(die.HardBin, out list))
                {
                    list = new List<WaferDie>();
                    byBin.Add(die.HardBin, list);
                }
                list.Add(die);
            }

            var bins = new List<int>(byBin.Keys);
            bins.Sort();
            foreach (int bin in bins)
            {
                var geometry = new StreamGeometry();
                using (StreamGeometryContext context = geometry.Open())
                {
                    foreach (WaferDie die in byBin[bin])
                    {
                        Rect rect = DieRect(die);
                        context.BeginFigure(new Point(rect.Left, rect.Top), true, false);
                        context.LineTo(new Point(rect.Right, rect.Top), false, false);
                        context.LineTo(new Point(rect.Right, rect.Bottom), false, false);
                        context.LineTo(new Point(rect.Left, rect.Bottom), false, false);
                    }
                }
                geometry.Freeze();
                _shapes.Add(new KeyValuePair<SolidColorBrush, StreamGeometry>(BinPalette.BrushForBin(bin), geometry));
            }
        }

        private Matrix ViewMatrix()
        {
            double centerX = ActualWidth / 2;
            double centerY = ActualHeight / 2;
            return new Matrix(_zoom, 0, 0, _zoom,
                centerX + _panX - _zoom * centerX,
                centerY + _panY - _zoom * centerY);
        }

        private Point ScreenToBase(Point point)
        {
            Matrix matrix = ViewMatrix();
            matrix.Invert();
            return matrix.Transform(point);
        }

        private Rect DieRect(WaferDie die)
        {
            int col = die.X - _wafer.MinX;
            int row = _wafer.MaxY - die.Y;
            double width = Math.Max(1, _cellWidth - _gap);
            double height = Math.Max(1, _cellHeight - _gap);
            return new Rect(_gridLeft + col * _cellWidth, _gridTop + row * _cellHeight, width, height);
        }

        private void DrawNotch(DrawingContext dc)
        {
            double halfWidth = Math.Max(5, Math.Min(14, _circleRadius * 0.15));
            double depth = Math.Max(8, Math.Min(20, halfWidth * 1.2));
            double x = _circleCenter.X;
            double y = _circleCenter.Y;
            double r = _circleRadius;
            double outer = r + 2 + depth;
            Point a;
            Point b;
            Point apex;
            switch (_notch)
            {
                case 'U':
                    a = new Point(x - halfWidth, y - outer);
                    b = new Point(x + halfWidth, y - outer);
                    apex = new Point(x, y - r - 1);
                    break;
                case 'L':
                    a = new Point(x - outer, y - halfWidth);
                    b = new Point(x - outer, y + halfWidth);
                    apex = new Point(x - r - 1, y);
                    break;
                case 'R':
                    a = new Point(x + outer, y - halfWidth);
                    b = new Point(x + outer, y + halfWidth);
                    apex = new Point(x + r + 1, y);
                    break;
                default:
                    a = new Point(x - halfWidth, y + outer);
                    b = new Point(x + halfWidth, y + outer);
                    apex = new Point(x, y + r + 1);
                    break;
            }

            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(a, true, true);
                context.LineTo(b, false, false);
                context.LineTo(apex, false, false);
            }
            geometry.Freeze();
            dc.DrawGeometry(MutedBrush, null, geometry);
        }

        private void DrawAxes(DrawingContext dc)
        {
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var xText = new FormattedText("X \u2192", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, MutedBrush, dpi);
            DrawLabel(dc, xText, new Point(ActualWidth - xText.Width - 10, ActualHeight - xText.Height - 6));
            var yText = new FormattedText("\u2191 Y", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, MutedBrush, dpi);
            DrawLabel(dc, yText, new Point(10, 2));
            if (_zoom > 1.001)
            {
                var zoomText = new FormattedText("\u00d7" + _zoom.ToString("0.0", CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 11, MutedBrush, dpi);
                DrawLabel(dc, zoomText, new Point(ActualWidth - zoomText.Width - 10, 2));
            }
        }

        private static void DrawLabel(DrawingContext dc, FormattedText text, Point point)
        {
            dc.DrawRoundedRectangle(LabelFill, null,
                new Rect(point.X - 4, point.Y - 1, text.Width + 8, text.Height + 2), 3, 3);
            dc.DrawText(text, point);
        }

        private static bool IsTooLarge(WaferMapWafer wafer)
        {
            long cols = (long)wafer.MaxX - wafer.MinX + 1;
            long rows = (long)wafer.MaxY - wafer.MinY + 1;
            return cols * rows > 4000000;
        }

        private static char NormalizeNotch(char notch)
        {
            char upper = char.ToUpperInvariant(notch);
            return upper == 'U' || upper == 'D' || upper == 'L' || upper == 'R' ? upper : 'D';
        }

        private static SolidColorBrush Frozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
