using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Threading.Tasks;

namespace StdfViewer
{
    public sealed class RecordRow
    {
        public int Seq { get; set; }
        public int No { get; set; }
        public string Offset { get; set; }
        public string TypSub { get; set; }
        public string Name { get; set; }
        public int Len { get; set; }
    }

    // 列表最多展开这么多行，再多就要求按类型过滤，避免一次生成几百万个行对象
    public static class RecordListBuilder
    {
        public static List<RecordRow> Build(StdfFile file, int[] seqs, int limit)
        {
            int total = seqs == null ? file.Count : seqs.Length;
            int n = Math.Min(limit, total);
            var rows = new List<RecordRow>(n);
            for (int i = 0; i < n; i++)
            {
                int seq = seqs != null ? seqs[i] : i;
                byte typ = file.TypOf(seq);
                byte sub = file.SubOf(seq);
                rows.Add(new RecordRow
                {
                    Seq = seq,
                    No = seq + 1,
                    Offset = "0x" + file.OffsetOf(seq).ToString("X", CultureInfo.InvariantCulture),
                    TypSub = typ + "," + sub,
                    Name = StdfRecordTable.DisplayName(typ, sub),
                    Len = file.LengthOf(seq),
                });
            }
            return rows;
        }
    }

    public sealed class ViewerWindow : Window
    {
        private const int ListRowLimit = 20000;

        private readonly ListView _types = new ListView();
        private readonly ListView _records = new ListView();
        private readonly ListView _fields = new ListView();
        private readonly TextBlock _info = new TextBlock();
        private readonly TextBlock _recordHead = new TextBlock();
        private readonly TextBox _jump = new TextBox();
        private readonly Button _open = new Button();
        private readonly Button _all = new Button();
        private StdfFile _file;
        private int[] _filter;
        private bool _loading;

        public ViewerWindow()
        {
            Title = "STDF Viewer";
            Width = 1500;
            Height = 820;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            BuildLayout();
        }

        public void QueueLoad(string path)
        {
            if (IsLoaded)
            {
                _ = LoadAsync(path);
                return;
            }
            Loaded += delegate { _ = LoadAsync(path); };
        }

        private void BuildLayout()
        {
            _open.Content = "Open STDF...";
            _open.Padding = new Thickness(10, 2, 10, 2);
            _open.Click += OnOpenClick;
            _all.Content = "All types";
            _all.Padding = new Thickness(8, 2, 8, 2);
            _all.Click += delegate { _filter = null; _types.SelectedItem = null; ShowRecords(); };
            _info.Text = "未打开文件";
            _info.Margin = new Thickness(10, 0, 0, 0);
            _info.VerticalAlignment = VerticalAlignment.Center;
            _info.TextTrimming = TextTrimming.CharacterEllipsis;

            var bar = new DockPanel { LastChildFill = true, Margin = new Thickness(6) };
            DockPanel.SetDock(_open, Dock.Left);
            DockPanel.SetDock(_all, Dock.Left);
            _jump.Width = 70;
            _jump.Margin = new Thickness(10, 0, 4, 0);
            ToolTipService.SetToolTip(_jump, "Type the # shown in the record list, then press Enter");
            _jump.KeyDown += OnJumpKeyDown;
            var right = new StackPanel { Orientation = Orientation.Horizontal };
            right.Children.Add(_jump);
            DockPanel.SetDock(right, Dock.Right);
            right.Children.Add(_info);
            bar.Children.Add(_open);
            bar.Children.Add(_all);
            bar.Children.Add(right);

            var typesHead = new TextBlock { Text = "Record types in file", Margin = new Thickness(2, 2, 2, 2), FontWeight = FontWeights.Bold };
            var typesPanel = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(typesHead, Dock.Top);
            typesPanel.Children.Add(typesHead);
            BuildTypeColumns();
            _types.SelectionChanged += OnTypeSelected;
            typesPanel.Children.Add(_types);

            var recHead = new TextBlock { Text = "Records (file order)", Margin = new Thickness(2, 2, 2, 2), FontWeight = FontWeights.Bold };
            var recPanel = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(recHead, Dock.Top);
            recPanel.Children.Add(recHead);
            BuildRecordColumns();
            _records.SelectionChanged += OnRecordSelected;
            recPanel.Children.Add(_records);

            var fieldPanel = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(_recordHead, Dock.Top);
            _recordHead.Margin = new Thickness(2, 2, 2, 2);
            _recordHead.FontWeight = FontWeights.Bold;
            _recordHead.Text = "Fields";
            fieldPanel.Children.Add(_recordHead);
            BuildFieldColumns();
            _fields.Focusable = false;
            fieldPanel.Children.Add(_fields);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(370) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            AddAt(grid, 0, typesPanel);
            AddAt(grid, 1, new GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Center });
            AddAt(grid, 2, recPanel);
            AddAt(grid, 3, new GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Center });
            AddAt(grid, 4, fieldPanel);

            var root = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(bar, Dock.Top);
            root.Children.Add(bar);
            root.Children.Add(grid);
            Content = root;
        }

        private static void AddAt(Grid grid, int column, UIElement child)
        {
            Grid.SetColumn(child, column);
            grid.Children.Add(child);
        }

        private void BuildTypeColumns()
        {
            var view = new GridView();
            view.Columns.Add(Column("Name", "Type", 52));
            view.Columns.Add(Column("TypSub", "Typ,Sub", 50));
            view.Columns.Add(Column("Count", "Count", 70));
            view.Columns.Add(Column("Bytes", "Bytes", 82));
            view.Columns.Add(Column("FirstSeq", "First", 70));
            _types.View = view;
        }

        private void BuildRecordColumns()
        {
            var view = new GridView();
            view.Columns.Add(Column("No", "#", 52));
            view.Columns.Add(Column("Name", "Type", 50));
            view.Columns.Add(Column("TypSub", "Typ,Sub", 50));
            view.Columns.Add(Column("Offset", "Offset", 76));
            view.Columns.Add(Column("Len", "LEN", 46));
            _records.View = view;
        }

        private void BuildFieldColumns()
        {
            var view = new GridView();
            view.Columns.Add(Column("Index", "#", 34));
            view.Columns.Add(Column("Name", "Field", 112));
            view.Columns.Add(Column("Type", "STDF type", 66));
            view.Columns.Add(Column("Value", "Value", 260));
            view.Columns.Add(Column("Hex", "Raw bytes", 220));
            view.Columns.Add(Column("Status", "Status", 100));
            _fields.View = view;
        }

        private static GridViewColumn Column(string path, string header, double width)
        {
            return new GridViewColumn { Header = header, Width = width, DisplayMemberBinding = new Binding(path) };
        }

        private async void OnOpenClick(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "STDF files (*.std)|*.std|All files (*.*)|*.*",
                Title = "Open STDF file",
            };
            if (dlg.ShowDialog(this) != true)
                return;
            await LoadAsync(dlg.FileName);
        }

        private async Task LoadAsync(string path)
        {
            if (_loading)
                return;
            _loading = true;
            _open.IsEnabled = false;
            _info.Text = "scanning " + path + " ...";
            StdfFile file = await Task.Run(delegate { return StdfFile.Scan(path); });
            _open.IsEnabled = true;
            _loading = false;

            if (_file != null)
                _file.Close();
            _file = file;
            _filter = null;
            _types.ItemsSource = file.Types;
            ShowRecords();
        }

        private void ShowRecords()
        {
            if (_file == null)
                return;
            List<RecordRow> rows = RecordListBuilder.Build(_file, _filter, ListRowLimit);
            _records.ItemsSource = rows;
            if (rows.Count > 0)
                _records.SelectedIndex = 0;
            int total = _filter == null ? _file.Count : _filter.Length;
            string cap = total > rows.Count ? "  (list capped at " + ListRowLimit.ToString("N0", CultureInfo.InvariantCulture) + ", pick a type to narrow)" : "";
            _info.Text = Summary(rows.Count) + cap;
        }

        private string Summary(long shown)
        {
            return System.IO.Path.GetFileName(_file.Path)
                + "   " + _file.FileLength.ToString("N0", CultureInfo.InvariantCulture) + " B"
                + "   CPU_TYPE=" + _file.CpuType + " STDF_VER=" + _file.StdfVersion
                + (_file.BigEndian ? " (big-endian)" : " (little-endian)")
                + "   records=" + _file.Count.ToString("N0", CultureInfo.InvariantCulture)
                + "   types=" + _file.Types.Count
                + "   scan=" + _file.ScanTime.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture) + "ms"
                + "   listed=" + shown.ToString("N0", CultureInfo.InvariantCulture)
                + (_file.Note.Length > 0 ? "   NOTE: " + _file.Note : "");
        }

        private void OnTypeSelected(object sender, SelectionChangedEventArgs e)
        {
            var stat = _types.SelectedItem as RecordTypeStat;
            if (stat == null)
                return;
            _filter = stat.Seqs;
            ShowRecords();
        }

        private void OnRecordSelected(object sender, SelectionChangedEventArgs e)
        {
            var row = _records.SelectedItem as RecordRow;
            if (row == null || _file == null)
                return;
            int seq = row.Seq;
            DecodedRecord rec = StdfDecoder.Decode(_file, seq);
            _recordHead.Text = "#" + (seq + 1) + " " + rec.RecordName + " (" + rec.TypSub + ")"
                + "  offset=0x" + rec.Offset.ToString("X", CultureInfo.InvariantCulture)
                + "  REC_LEN=" + rec.DeclaredLen + "  decoded=" + rec.Consumed + "  " + rec.Status;
            _fields.ItemsSource = rec.Rows;
        }

        private void OnJumpKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;
            long no;
            if (!long.TryParse(_jump.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out no))
                return;
            var rows = _records.ItemsSource as List<RecordRow>;
            if (rows == null)
                return;
            int index = rows.FindIndex(delegate(RecordRow r) { return r.No == no; });
            if (index < 0)
                return;
            _records.ScrollIntoView(_records.Items[index]);
            _records.SelectedIndex = index;
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_file != null)
                _file.Close();
            base.OnClosed(e);
        }
    }
}
