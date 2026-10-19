using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace StdfViewer
{
    public sealed partial class ViewerWindow : Window
    {
        private const int ListRowLimit = 20000;

        // 对比时，两侧各自的数据来源：磁盘重扫（带记录序列）或历史快照（只有类型统计）
        private sealed class CompareInput
        {
            public HistoryEntry Entry;
            public List<StructRun> Runs;
        }

        private sealed class ComparePair
        {
            public CompareInput Left;
            public CompareInput Right;
        }

        private readonly HistoryStore _store = new HistoryStore();

        private StdfFile _file;
        private int[] _filter;
        private bool _loading;
        private HistoryEntry _cmpLeft;
        private HistoryEntry _cmpRight;

        public ViewerWindow()
        {
            InitializeComponent();
            _store.Load();
            _historyList.ItemsSource = _store.Entries;
            _cmpList.ItemsSource = _store.Entries;
            UpdateCompareLabels();
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

        private void OnAllClick(object sender, RoutedEventArgs e)
        {
            _filter = null;
            _types.SelectedItem = null;
            ShowRecords();
        }

        private void OnTabChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_all == null || _jump == null)
                return;
            bool browse = _tabs.SelectedIndex == 0;
            _all.IsEnabled = browse;
            _jump.IsEnabled = browse;
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
            _store.Record(file);
            if (_historyList.SelectedIndex < 0 && _historyList.Items.Count > 0)
                _historyList.SelectedIndex = 0;
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
                + (_file.Note.Length > 0 ? "   NOTE: " + _file.Note : "")
                + (_store.LastError.Length > 0 ? "   HISTORY: " + _store.LastError : "");
        }

        private void OnTypeSelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var stat = _types.SelectedItem as RecordTypeStat;
            if (stat == null)
                return;
            _filter = stat.Seqs;
            ShowRecords();
        }

        private void OnRecordSelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
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

        // ---------------- 历史解析记录 ----------------

        private void OnHistorySelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var entry = _historyList.SelectedItem as HistoryEntry;
            if (entry == null)
                return;
            _historySummary.Text = entry.Path
                + "   " + N(entry.Size) + " B"
                + "   CPU_TYPE=" + entry.CpuType + " STDF_VER=" + entry.StdfVer
                + (entry.BigEndian ? " (big-endian)" : " (little-endian)")
                + "   records=" + N(entry.Records)
                + "   types=" + entry.TypeCount
                + "   scan=" + entry.ScanMs.ToString("F0", CultureInfo.InvariantCulture) + "ms"
                + "   parsed=" + entry.ParsedAt
                + (entry.Note.Length > 0 ? "   NOTE: " + entry.Note : "");
            _historyTypes.ItemsSource = entry.Types;
        }

        private void OnHistoryDeleteClick(object sender, RoutedEventArgs e)
        {
            var entry = _historyList.SelectedItem as HistoryEntry;
            if (entry == null)
            {
                _historySummary.Text = "先在左侧列表里选中一条历史记录，再点 Delete。";
                return;
            }
            MessageBoxResult answer = MessageBox.Show(this,
                "Remove this entry from history?\r\nThe .std file on disk is NOT deleted.\r\n\r\n" + entry.Path,
                "Delete history entry", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (answer != MessageBoxResult.OK)
                return;

            _store.Remove(entry);
            if (_historyList.Items.Count > 0)
            {
                _historyList.SelectedIndex = 0;
            }
            else
            {
                _historySummary.Text = "选择左侧一条历史记录查看上次解析结果。";
                _historyTypes.ItemsSource = null;
            }
        }

        // ---------------- 文件对比 ----------------

        private void OnSetLeftClick(object sender, RoutedEventArgs e)
        {
            SetSide(true);
        }

        private void OnSetRightClick(object sender, RoutedEventArgs e)
        {
            SetSide(false);
        }

        private void SetSide(bool left)
        {
            var entry = _cmpList.SelectedItem as HistoryEntry;
            if (entry == null)
            {
                _cmpSummary.Text = "先在左侧列表里选中一条历史记录，再点 Set as left / Set as right。";
                return;
            }
            if (left)
                _cmpLeft = entry;
            else
                _cmpRight = entry;
            UpdateCompareLabels();
        }

        private void UpdateCompareLabels()
        {
            _cmpLeftText.Text = Describe(_cmpLeft);
            _cmpRightText.Text = Describe(_cmpRight);
            bool same = _cmpLeft != null && ReferenceEquals(_cmpLeft, _cmpRight);
            _cmpCompare.IsEnabled = _cmpLeft != null && _cmpRight != null && !same;
            if (same)
                _cmpSummary.Text = "左右两侧不能是同一条历史记录，请换一条。";
        }

        private static string Describe(HistoryEntry entry)
        {
            if (entry == null)
                return "(none)";
            return entry.FileName + "\r\n" + entry.ParsedAt + "   " + N(entry.Records) + " records";
        }

        private async void OnCompareClick(object sender, RoutedEventArgs e)
        {
            if (_cmpLeft == null || _cmpRight == null || ReferenceEquals(_cmpLeft, _cmpRight))
                return;

            _cmpCompare.IsEnabled = false;
            _cmpSummary.Text = "comparing ...";
            _cmpTypes.ItemsSource = null;
            _cmpStruct.Text = "";
            try
            {
                string leftPath = _cmpLeft.Path;
                string rightPath = _cmpRight.Path;
                // 两个文件都还在磁盘上就重新扫描（顺带拿到记录序列，可做结构对比）；
                // 否则退回历史快照，只做类型层与出现顺序对比
                bool onDisk = File.Exists(leftPath) && File.Exists(rightPath);
                ComparePair pair;
                if (onDisk)
                {
                    pair = await Task.Run(delegate { return LoadPair(leftPath, rightPath); });
                }
                else
                {
                    pair = new ComparePair
                    {
                        Left = new CompareInput { Entry = _cmpLeft, Runs = null },
                        Right = new CompareInput { Entry = _cmpRight, Runs = null },
                    };
                }

                List<CompareTypeRow> rows = StdfComparer.CompareTypes(pair.Left.Entry.Types, pair.Right.Entry.Types);
                _cmpTypes.ItemsSource = rows;

                int onlyLeft = 0;
                int onlyRight = 0;
                foreach (CompareTypeRow row in rows)
                {
                    if (row.Status == StdfComparer.OnlyLeft)
                        onlyLeft++;
                    else if (row.Status == StdfComparer.OnlyRight)
                        onlyRight++;
                }

                long delta = (long)pair.Right.Entry.Records - pair.Left.Entry.Records;
                _cmpSummary.Text = "left: " + pair.Left.Entry.FileName
                    + "   right: " + pair.Right.Entry.FileName
                    + "   [" + (onDisk ? "rescanned from disk" : "history snapshot, file not on disk") + "]"
                    + "\r\nrecords " + N(pair.Left.Entry.Records) + " -> " + N(pair.Right.Entry.Records)
                    + "   (" + (delta >= 0 ? "+" : "") + N(delta) + ")"
                    + "   types only left: " + onlyLeft.ToString(CultureInfo.InvariantCulture)
                    + "   only right: " + onlyRight.ToString(CultureInfo.InvariantCulture)
                    + (pair.Left.Entry.Note.Length > 0 ? "   left NOTE: " + pair.Left.Entry.Note : "")
                    + (pair.Right.Entry.Note.Length > 0 ? "   right NOTE: " + pair.Right.Entry.Note : "");

                _cmpStruct.Text = BuildStructureReport(pair, onDisk);
            }
            finally
            {
                UpdateCompareLabels();
            }
        }

        private static ComparePair LoadPair(string leftPath, string rightPath)
        {
            return new ComparePair { Left = LoadOne(leftPath), Right = LoadOne(rightPath) };
        }

        private static CompareInput LoadOne(string path)
        {
            StdfFile file = StdfFile.Scan(path);
            var input = new CompareInput
            {
                Entry = HistoryStore.FromScan(file),
                Runs = StdfComparer.BuildRuns(file),
            };
            file.Close();
            return input;
        }

        private static string BuildStructureReport(ComparePair pair, bool onDisk)
        {
            var sb = new StringBuilder();
            sb.AppendLine(StdfComparer.CompareTypeOrder(pair.Left.Entry.Types, pair.Right.Entry.Types));
            sb.AppendLine();

            if (!onDisk || pair.Left.Runs == null || pair.Right.Runs == null)
            {
                sb.AppendLine("Structure (RLE segments): unavailable — needs both files on disk.");
                sb.AppendLine("history snapshots keep per-type totals only, not the record sequence.");
                return sb.ToString();
            }

            StructureCompare c = StdfComparer.CompareStructure(pair.Left.Runs, pair.Right.Runs);
            if (c.Identical)
            {
                sb.AppendLine("Structure: identical (" + N(c.LeftSegments) + " segments)");
                sb.AppendLine();
                sb.AppendLine("left : " + StdfComparer.RunsText(pair.Left.Runs, 0, 60));
                return sb.ToString();
            }

            sb.AppendLine("Structure: differs");
            sb.AppendLine("  segments       left " + N(c.LeftSegments) + "   right " + N(c.RightSegments));
            sb.AppendLine("  common prefix  " + N(c.CommonPrefix) + " segments");
            sb.AppendLine("  common suffix  " + N(c.CommonSuffix) + " segments");
            sb.AppendLine("  diff window    left " + N(c.LeftWindow) + "   right " + N(c.RightWindow) + " segments");
            sb.AppendLine();
            sb.AppendLine("left  window : " + (c.LeftWindowText.Length == 0 ? "(none)" : c.LeftWindowText));
            sb.AppendLine("right window : " + (c.RightWindowText.Length == 0 ? "(none)" : c.RightWindowText));
            return sb.ToString();
        }

        private static string N(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_file != null)
                _file.Close();
            base.OnClosed(e);
        }
    }
}
