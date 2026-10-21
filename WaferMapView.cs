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
        private static readonly SolidColorBrush AxisBrush = Frozen(0x9A, 0xA3, 0xAF);
        private static readonly SolidColorBrush HoverBrush = Frozen(0x2F, 0x6F, 0xED);
        private static readonly SolidColorBrush BinZeroBrush = Frozen(0xC7, 0xCE, 0xD8);
        private static readonly SolidColorBrush[] BinBrushes =
        {
            Frozen(0x2F, 0x9E, 0x5B), Frozen(0xD9, 0x53, 0x4F), Frozen(0x3B, 0x7B, 0xD9),
            Frozen(0xE0, 0xA0, 0x30), Frozen(0x8A, 0x63, 0xD2), Frozen(0x2A, 0xA7, 0xA0),
            Frozen(0xD2, 0x67, 0x9B), Frozen(0x8A, 0x6D, 0x3B), Frozen(0x6C, 0x7A, 0x89),
            Frozen(0xC9, 0xA2, 0x27), Frozen(0x4F, 0x8A, 0x5B), Frozen(0xA3, 0x5B, 0xA3),
        };

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

        public WaferMapWafer Wafer { get { return _wafer; } }
        public WaferDie HoveredDie { get { return _hovered; } }
        public bool TooLarge { get { return _tooLarge; } }

        public event EventHandler HoverChanged;

        public static SolidColorBrush BrushForBin(int bin)
        {
            return bin <= 0 ? BinZeroBrush : BinBrushes[(bin - 1) % BinBrushes.Length];
        }

        public void SetWafer(WaferMapWafer wafer, char notch)
        {
            _wafer = wafer;
            _notch = NormalizeNotch(notch);
            _tooLarge = wafer != null && IsTooLarge(wafer);
            _hovered = null;
            _cellWidth = 0;
            _cellHeight = 0;
            _layoutWidth = -1;
            _layoutHeight = -1;
            _shapes.Clear();
            InvalidateVisual();
            HoverChanged?.Invoke(this, EventArgs.Empty);
        }

        public WaferDie HitTest(Point point)
        {
            if (_wafer == null)
                return null;
            EnsureLayout();
            if (_cellWidth == 0 || _cellHeight == 0 || _tooLarge)
                return null;
            int col = (int)Math.Floor((point.X - _gridLeft) / _cellWidth);
            int row = (int)Math.Floor((point.Y - _gridTop) / _cellHeight);
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

            dc.DrawEllipse(WaferFill, new Pen(CirclePenBrush, 1), _circleCenter, _circleRadius, _circleRadius);
            foreach (KeyValuePair<SolidColorBrush, StreamGeometry> shape in _shapes)
                dc.DrawGeometry(shape.Key, null, shape.Value);
            DrawNotch(dc);
            DrawAxes(dc);
            if (_hovered != null)
            {
                Rect rect = DieRect(_hovered);
                dc.DrawRectangle(null, new Pen(HoverBrush, 1.5), new Rect(rect.X, rect.Y, _cellWidth, _cellHeight));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            WaferDie die = HitTest(e.GetPosition(this));
            if (!ReferenceEquals(die, _hovered))
            {
                _hovered = die;
                InvalidateVisual();
                HoverChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hovered != null)
            {
                _hovered = null;
                InvalidateVisual();
                HoverChanged?.Invoke(this, EventArgs.Empty);
            }
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
                _shapes.Add(new KeyValuePair<SolidColorBrush, StreamGeometry>(BrushForBin(bin), geometry));
            }
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
                new Typeface("Segoe UI"), 11, AxisBrush, dpi);
            dc.DrawText(xText, new Point(ActualWidth - xText.Width - 10, ActualHeight - xText.Height - 2));
            var yText = new FormattedText("\u2191 Y", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, AxisBrush, dpi);
            dc.DrawText(yText, new Point(10, 2));
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
