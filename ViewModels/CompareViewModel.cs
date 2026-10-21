using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StdfViewer.ViewModels
{
    public sealed partial class CompareViewModel : ObservableObject
    {
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

        private readonly HistoryStore _store;
        private ComparePair _pair;
        private bool _onDisk;

        [ObservableProperty] private HistoryEntry _selectedEntry;
        [ObservableProperty] private HistoryEntry _left;
        [ObservableProperty] private HistoryEntry _right;
        [ObservableProperty] private string _leftText;
        [ObservableProperty] private string _rightText;
        [ObservableProperty] private string _summaryText;
        [ObservableProperty] private List<CompareTypeRow> _types;
        [ObservableProperty] private string _structureText = "";
        [ObservableProperty] private bool _canCompare;

        public ObservableCollection<HistoryEntry> Entries { get { return _store.Entries; } }

        public CompareViewModel(HistoryStore store)
        {
            _store = store;
            LeftText = Loc.T("Str.Compare.None");
            RightText = Loc.T("Str.Compare.None");
            SummaryText = Loc.T("Str.Compare.Placeholder");
        }

        [RelayCommand]
        private void SetLeft()
        {
            if (SelectedEntry == null)
            {
                SummaryText = Loc.T("Str.Compare.SelectFirst");
                return;
            }
            Left = SelectedEntry;
            UpdateLabels();
        }

        [RelayCommand]
        private void SetRight()
        {
            if (SelectedEntry == null)
            {
                SummaryText = Loc.T("Str.Compare.SelectFirst");
                return;
            }
            Right = SelectedEntry;
            UpdateLabels();
        }

        [RelayCommand]
        private async Task Compare()
        {
            if (!CanCompare || Left == null || Right == null)
                return;

            CanCompare = false;
            SummaryText = Loc.T("Str.Compare.Comparing");
            Types = null;
            StructureText = "";
            try
            {
                string leftPath = Left.Path;
                string rightPath = Right.Path;
                bool onDisk = File.Exists(leftPath) && File.Exists(rightPath);
                if (onDisk)
                {
                    _pair = await Task.Run(delegate { return LoadPair(leftPath, rightPath); });
                }
                else
                {
                    _pair = new ComparePair
                    {
                        Left = new CompareInput { Entry = Left, Runs = null },
                        Right = new CompareInput { Entry = Right, Runs = null },
                    };
                }
                _onDisk = onDisk;
                RenderCompare();
            }
            finally
            {
                UpdateLabels();
            }
        }

        public void RefreshLanguage()
        {
            if (_pair != null)
            {
                RenderCompare();
                return;
            }
            SummaryText = Loc.T(Left != null && ReferenceEquals(Left, Right)
                ? "Str.Compare.SameSide"
                : "Str.Compare.Placeholder");
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            LeftText = Describe(Left);
            RightText = Describe(Right);
            bool same = Left != null && ReferenceEquals(Left, Right);
            CanCompare = Left != null && Right != null && !same;
            if (same)
                SummaryText = Loc.T("Str.Compare.SameSide");
        }

        private static string Describe(HistoryEntry entry)
        {
            if (entry == null)
                return Loc.T("Str.Compare.None");
            return entry.FileName + "\r\n" + entry.ParsedAt + "   " + UiFormat.N(entry.Records) + " " + Loc.T("Str.Lbl.Records");
        }

        private void RenderCompare()
        {
            ComparePair pair = _pair;
            List<CompareTypeRow> rows = StdfComparer.CompareTypes(pair.Left.Entry.Types, pair.Right.Entry.Types);
            Types = rows;

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
            string signed = (delta >= 0 ? "+" : "") + UiFormat.N(delta);

            var text = new StringBuilder();
            text.Append(Loc.T("Str.Compare.SideLine", pair.Left.Entry.FileName, pair.Right.Entry.FileName,
                Loc.T(_onDisk ? "Str.Compare.Rescanned" : "Str.Compare.Snapshot")));
            text.AppendLine();
            text.Append(Loc.T("Str.Compare.RecordsDelta", UiFormat.N(pair.Left.Entry.Records),
                UiFormat.N(pair.Right.Entry.Records), signed))
                .Append("   ").Append(Loc.T("Str.Compare.OnlyLeftCount", onlyLeft.ToString(CultureInfo.InvariantCulture)))
                .Append("   ").Append(Loc.T("Str.Compare.OnlyRightCount", onlyRight.ToString(CultureInfo.InvariantCulture)));
            if (pair.Left.Entry.Note.Length > 0)
                text.Append("   ").Append(Loc.T("Str.Compare.LeftNote", pair.Left.Entry.Note));
            if (pair.Right.Entry.Note.Length > 0)
                text.Append("   ").Append(Loc.T("Str.Compare.RightNote", pair.Right.Entry.Note));
            SummaryText = text.ToString();

            StructureText = BuildStructureReport(pair, _onDisk);
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

            StructureCompare compare = StdfComparer.CompareStructure(pair.Left.Runs, pair.Right.Runs);
            if (compare.Identical)
            {
                sb.AppendLine(Loc.T("Str.Compare.StructIdentical", UiFormat.N(compare.LeftSegments)));
                sb.AppendLine();
                sb.AppendLine(Loc.T("Str.Compare.LeftSide") + " : " + SegmentText(StdfComparer.RunsText(pair.Left.Runs, 0, 60)));
                return sb.ToString();
            }

            sb.AppendLine(Loc.T("Str.Compare.StructDiffers"));
            sb.AppendLine("  " + Loc.T("Str.Compare.Segments") + "         " + Loc.T("Str.Compare.LeftSide") + " " + UiFormat.N(compare.LeftSegments)
                + "   " + Loc.T("Str.Compare.RightSide") + " " + UiFormat.N(compare.RightSegments));
            sb.AppendLine("  " + Loc.T("Str.Compare.CommonPrefix") + "  " + UiFormat.N(compare.CommonPrefix) + " " + Loc.T("Str.Compare.Segments"));
            sb.AppendLine("  " + Loc.T("Str.Compare.CommonSuffix") + "  " + UiFormat.N(compare.CommonSuffix) + " " + Loc.T("Str.Compare.Segments"));
            sb.AppendLine("  " + Loc.T("Str.Compare.DiffWindow") + "    " + Loc.T("Str.Compare.LeftSide") + " " + UiFormat.N(compare.LeftWindow)
                + "   " + Loc.T("Str.Compare.RightSide") + " " + UiFormat.N(compare.RightWindow) + " " + Loc.T("Str.Compare.Segments"));
            sb.AppendLine();
            sb.AppendLine(Loc.T("Str.Compare.LeftWindow") + "  : " + SegmentText(compare.LeftWindowText));
            sb.AppendLine(Loc.T("Str.Compare.RightWindow") + " : " + SegmentText(compare.RightWindowText));
            return sb.ToString();
        }

        private static string SegmentText(string text)
        {
            return string.IsNullOrEmpty(text) ? Loc.T("Str.Compare.NoSegment") : text;
        }
    }
}
