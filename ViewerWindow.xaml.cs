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
        private SummaryResult _summary;
        private DecodedRecord _record;
        private ComparePair _comparePair;
        private bool _compareOnDisk;

        public ViewerWindow()
        {
            InitializeComponent();
            Loc.LanguageChanged += OnLanguageChanged;
            _store.Load();
            _historyList.ItemsSource = _store.Entries;
            _cmpList.ItemsSource = _store.Entries;
            _info.Text = Loc.T("Str.Status.NoFile");
            _historySummary.Text = Loc.T("Str.History.Placeholder");
            _cmpSummary.Text = Loc.T("Str.Compare.Placeholder");
            UpdateLangButton();
            UpdateCompareLabels();
            ApplySummary();
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
                Filter = Loc.T("Str.Dlg.OpenFilter"),
                Title = Loc.T("Str.Dlg.OpenTitle"),
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
            bool browse = ReferenceEquals(_tabs.SelectedItem, _tabBrowse);
            _all.IsEnabled = browse;
            _jump.IsEnabled = browse;
        }

        private async Task LoadAsync(string path)
        {
            if (_loading)
                return;
            _loading = true;
            _open.IsEnabled = false;
            _info.Text = Loc.T("Str.Status.Scanning", path);
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
            _summary = null;
            ApplySummary();
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
            _info.Text = BuildInfoLine(rows.Count);
        }

        private void RefreshInfo()
        {
            var rows = _records.ItemsSource as List<RecordRow>;
            _info.Text = BuildInfoLine(rows == null ? 0 : rows.Count);
        }

        private string BuildInfoLine(long shown)
        {
            var text = new StringBuilder();
            text.Append(Path.GetFileName(_file.Path))
                .Append("   ").Append(N(_file.FileLength)).Append(" B")
                .Append("   CPU_TYPE=").Append(_file.CpuType.ToString(CultureInfo.InvariantCulture))
                .Append(" STDF_VER=").Append(_file.StdfVersion.ToString(CultureInfo.InvariantCulture))
                .Append("   ").Append(EndianWord(_file.BigEndian))
                .Append("   ").Append(Loc.T("Str.Lbl.Records")).Append('=').Append(N(_file.Count))
                .Append("   ").Append(Loc.T("Str.Lbl.Types")).Append('=').Append(_file.Types.Count.ToString(CultureInfo.InvariantCulture))
                .Append("   ").Append(Loc.T("Str.Lbl.Scan")).Append('=').Append(_file.ScanTime.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)).Append("ms")
                .Append("   ").Append(Loc.T("Str.Lbl.Listed")).Append('=').Append(N(shown));
            long total = _filter == null ? _file.Count : _filter.Length;
            if (total > shown)
                text.Append(Loc.T("Str.Status.ListCapped", ListRowLimit.ToString("N0", CultureInfo.InvariantCulture)));
            if (!string.IsNullOrEmpty(_file.Note))
                text.Append("   ").Append(Loc.T("Str.Lbl.Note")).Append(": ").Append(_file.Note);
            if (!string.IsNullOrEmpty(_store.LastError))
                text.Append("   ").Append(Loc.T("Str.Lbl.History")).Append(": ").Append(_store.LastError);
            if (!string.IsNullOrEmpty(UiSettings.LastError))
                text.Append("   ").Append(Loc.T("Str.Lbl.Settings")).Append(": ").Append(UiSettings.LastError);
            return text.ToString();
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
            ShowRecord(row.Seq);
        }

        private void ShowRecord(int seq)
        {
            DecodedRecord rec = StdfDecoder.Decode(_file, seq);
            _record = rec;
            _recordHead.Text = "#" + (seq + 1) + " " + rec.RecordName + " (" + rec.TypSub + ")"
                + "  offset=0x" + rec.Offset.ToString("X", CultureInfo.InvariantCulture)
                + "  REC_LEN=" + rec.DeclaredLen + "  decoded=" + rec.Consumed + "  " + Loc.Status(rec.Status);
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

        // ---------------- 语言 ----------------

        private void OnLanguageClick(object sender, RoutedEventArgs e)
        {
            Loc.SetLanguage(Loc.IsChinese ? Loc.English : Loc.Chinese);
        }

        private void UpdateLangButton()
        {
            // 按钮显示"点了会切到哪种语言"，中文环境下不需要再翻译
            _lang.Content = Loc.IsChinese ? "English" : "中文";
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            UpdateLangButton();
            _summary = null;
            ApplySummary();
            if (_file == null)
                _info.Text = Loc.T("Str.Status.NoFile");
            else
                RefreshInfo();
            if (_record != null)
                ShowRecord(_record.Seq);
            else
                _recordHead.Text = Loc.T("Str.Browse.Fields");
            var entry = _historyList.SelectedItem as HistoryEntry;
            if (entry != null)
            {
                ShowHistoryEntry(entry);
            }
            else
            {
                _historySummary.Text = Loc.T("Str.History.Placeholder");
                _historyTypes.ItemsSource = null;
            }
            if (_comparePair != null)
                RenderCompare();
            else
                _cmpSummary.Text = Loc.T(_cmpLeft != null && ReferenceEquals(_cmpLeft, _cmpRight) ? "Str.Compare.SameSide" : "Str.Compare.Placeholder");
            UpdateCompareLabels();
        }

        protected override void OnClosed(EventArgs e)
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            if (_file != null)
                _file.Close();
            base.OnClosed(e);
        }

        // ---------------- 汇总 ----------------

        private void ApplySummary()
        {
            bool hasFile = _file != null;
            _summaryEmpty.Visibility = hasFile ? Visibility.Collapsed : Visibility.Visible;
            _summaryScroll.Visibility = hasFile ? Visibility.Visible : Visibility.Collapsed;
            if (!hasFile)
            {
                _summarySections.ItemsSource = null;
                _kpiSize.Text = "";
                _kpiRecords.Text = "";
                _kpiTypes.Text = "";
                _kpiDuts.Text = "";
                _kpiYield.Text = "";
                _kpiScan.Text = "";
                _kpiEndian.Text = "";
                return;
            }

            if (_summary == null)
                _summary = StdfSummary.Build(_file);

            // 先清空再赋值：Title/Name 是构建时定稿的文字，换语言要靠重建模型
            _summarySections.ItemsSource = null;
            _summarySections.ItemsSource = _summary.Sections;
            _kpiSize.Text = N(_summary.FileLength) + " B";
            _kpiRecords.Text = N(_summary.RecordCount);
            _kpiTypes.Text = _summary.TypeCount.ToString(CultureInfo.InvariantCulture);
            _kpiDuts.Text = _summary.DutCount.ToString(CultureInfo.InvariantCulture);
            _kpiYield.Text = _summary.YieldPercent < 0
                ? Loc.T("Str.Word.Na")
                : _summary.YieldPercent.ToString("F2", CultureInfo.InvariantCulture) + "%";
            _kpiScan.Text = _summary.ScanMs.ToString("F0", CultureInfo.InvariantCulture) + " ms";
            _kpiEndian.Text = EndianWord(_summary.BigEndian);
        }

        // ---------------- 历史解析记录 ----------------

        private void OnHistorySelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var entry = _historyList.SelectedItem as HistoryEntry;
            if (entry == null)
                return;
            ShowHistoryEntry(entry);
        }

        private void ShowHistoryEntry(HistoryEntry entry)
        {
            var text = new StringBuilder();
            text.Append(entry.Path)
                .Append("   ").Append(N(entry.Size)).Append(" B")
                .Append("   CPU_TYPE=").Append(entry.CpuType.ToString(CultureInfo.InvariantCulture))
                .Append(" STDF_VER=").Append(entry.StdfVer.ToString(CultureInfo.InvariantCulture))
                .Append("   ").Append(EndianWord(entry.BigEndian))
                .Append("   ").Append(Loc.T("Str.Lbl.Records")).Append('=').Append(N(entry.Records))
                .Append("   ").Append(Loc.T("Str.Lbl.Types")).Append('=').Append(entry.TypeCount.ToString(CultureInfo.InvariantCulture))
                .Append("   ").Append(Loc.T("Str.Lbl.Scan")).Append('=').Append(entry.ScanMs.ToString("F0", CultureInfo.InvariantCulture)).Append("ms")
                .Append("   ").Append(Loc.T("Str.Lbl.Parsed")).Append('=').Append(entry.ParsedAt);
            if (entry.Note.Length > 0)
                text.Append("   ").Append(Loc.T("Str.Lbl.Note")).Append(": ").Append(entry.Note);
            _historySummary.Text = text.ToString();
            _historyTypes.ItemsSource = entry.Types;
        }

        private void OnHistoryDeleteClick(object sender, RoutedEventArgs e)
        {
            var entry = _historyList.SelectedItem as HistoryEntry;
            if (entry == null)
            {
                _historySummary.Text = Loc.T("Str.History.DeleteEmpty");
                return;
            }
            MessageBoxResult answer = MessageBox.Show(this,
                Loc.T("Str.History.DeleteConfirm", entry.Path),
                Loc.T("Str.History.DeleteTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (answer != MessageBoxResult.OK)
                return;

            _store.Remove(entry);
            if (_historyList.Items.Count > 0)
            {
                _historyList.SelectedIndex = 0;
            }
            else
            {
                _historySummary.Text = Loc.T("Str.History.Placeholder");
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
                _cmpSummary.Text = Loc.T("Str.Compare.SelectFirst");
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
                _cmpSummary.Text = Loc.T("Str.Compare.SameSide");
        }

        private static string Describe(HistoryEntry entry)
        {
            if (entry == null)
                return Loc.T("Str.Compare.None");
            return entry.FileName + "\r\n" + entry.ParsedAt + "   " + N(entry.Records) + " " + Loc.T("Str.Lbl.Records");
        }

        private async void OnCompareClick(object sender, RoutedEventArgs e)
        {
            if (_cmpLeft == null || _cmpRight == null || ReferenceEquals(_cmpLeft, _cmpRight))
                return;

            _cmpCompare.IsEnabled = false;
            _cmpSummary.Text = Loc.T("Str.Compare.Comparing");
            _cmpTypes.ItemsSource = null;
            _cmpStruct.Text = "";
            try
            {
                string leftPath = _cmpLeft.Path;
                string rightPath = _cmpRight.Path;
                // 两个文件都还在磁盘上就重新扫描（顺带拿到记录序列，可做结构对比）；
                // 否则退回历史快照，只做类型层与出现顺序对比
                bool onDisk = File.Exists(leftPath) && File.Exists(rightPath);
                if (onDisk)
                {
                    _comparePair = await Task.Run(delegate { return LoadPair(leftPath, rightPath); });
                }
                else
                {
                    _comparePair = new ComparePair
                    {
                        Left = new CompareInput { Entry = _cmpLeft, Runs = null },
                        Right = new CompareInput { Entry = _cmpRight, Runs = null },
                    };
                }
                _compareOnDisk = onDisk;
                RenderCompare();
            }
            finally
            {
                UpdateCompareLabels();
            }
        }

        private void RenderCompare()
        {
            ComparePair pair = _comparePair;
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
            string signed = (delta >= 0 ? "+" : "") + N(delta);

            var text = new StringBuilder();
            text.Append(Loc.T("Str.Compare.SideLine", pair.Left.Entry.FileName, pair.Right.Entry.FileName,
                Loc.T(_compareOnDisk ? "Str.Compare.Rescanned" : "Str.Compare.Snapshot")));
            text.AppendLine();
            text.Append(Loc.T("Str.Compare.RecordsDelta", N(pair.Left.Entry.Records), N(pair.Right.Entry.Records), signed))
                .Append("   ").Append(Loc.T("Str.Compare.OnlyLeftCount", onlyLeft.ToString(CultureInfo.InvariantCulture)))
                .Append("   ").Append(Loc.T("Str.Compare.OnlyRightCount", onlyRight.ToString(CultureInfo.InvariantCulture)));
            if (pair.Left.Entry.Note.Length > 0)
                text.Append("   ").Append(Loc.T("Str.Compare.LeftNote", pair.Left.Entry.Note));
            if (pair.Right.Entry.Note.Length > 0)
                text.Append("   ").Append(Loc.T("Str.Compare.RightNote", pair.Right.Entry.Note));
            _cmpSummary.Text = text.ToString();

            _cmpStruct.Text = BuildStructureReport(pair, _compareOnDisk);
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
                sb.AppendLine(Loc.T("Str.Compare.StructUnavailable"));
                sb.AppendLine(Loc.T("Str.Compare.StructHistoryOnly"));
                return sb.ToString();
            }

            StructureCompare c = StdfComparer.CompareStructure(pair.Left.Runs, pair.Right.Runs);
            if (c.Identical)
            {
                sb.AppendLine(Loc.T("Str.Compare.StructIdentical", N(c.LeftSegments)));
                sb.AppendLine();
                sb.AppendLine(Loc.T("Str.Compare.LeftSide") + " : " + SegmentText(StdfComparer.RunsText(pair.Left.Runs, 0, 60)));
                return sb.ToString();
            }

            sb.AppendLine(Loc.T("Str.Compare.StructDiffers"));
            sb.AppendLine("  " + Loc.T("Str.Compare.Segments") + "         " + Loc.T("Str.Compare.LeftSide") + " " + N(c.LeftSegments)
                + "   " + Loc.T("Str.Compare.RightSide") + " " + N(c.RightSegments));
            sb.AppendLine("  " + Loc.T("Str.Compare.CommonPrefix") + "  " + N(c.CommonPrefix) + " " + Loc.T("Str.Compare.Segments"));
            sb.AppendLine("  " + Loc.T("Str.Compare.CommonSuffix") + "  " + N(c.CommonSuffix) + " " + Loc.T("Str.Compare.Segments"));
            sb.AppendLine("  " + Loc.T("Str.Compare.DiffWindow") + "    " + Loc.T("Str.Compare.LeftSide") + " " + N(c.LeftWindow)
                + "   " + Loc.T("Str.Compare.RightSide") + " " + N(c.RightWindow) + " " + Loc.T("Str.Compare.Segments"));
            sb.AppendLine();
            sb.AppendLine(Loc.T("Str.Compare.LeftWindow") + "  : " + SegmentText(c.LeftWindowText));
            sb.AppendLine(Loc.T("Str.Compare.RightWindow") + " : " + SegmentText(c.RightWindowText));
            return sb.ToString();
        }

        private static string SegmentText(string text)
        {
            return string.IsNullOrEmpty(text) ? Loc.T("Str.Compare.NoSegment") : text;
        }

        private static string EndianWord(bool bigEndian)
        {
            return Loc.T(bigEndian ? "Str.Word.BigEndian" : "Str.Word.LittleEndian");
        }

        private static string N(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
