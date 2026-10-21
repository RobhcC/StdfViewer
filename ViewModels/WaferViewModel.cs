using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StdfViewer.ViewModels
{
    public sealed class WaferBinRow
    {
        public Brush Fill { get; set; }
        public string BinText { get; set; }
        public string CountText { get; set; }
        public string PercentText { get; set; }
    }

    public sealed partial class WaferViewModel : ObservableObject
    {
        private StdfFile _file;
        private WaferMap _map;

        [ObservableProperty] private List<WaferMapWafer> _wafers;
        [ObservableProperty] private WaferMapWafer _selectedWafer;
        [ObservableProperty] private int _notchMode;
        [ObservableProperty] private char _notch = 'D';
        [ObservableProperty] private WaferDie _hoveredDie;
        [ObservableProperty] private string _statsText = "";
        [ObservableProperty] private string _hoverText = "";
        [ObservableProperty] private string _emptyText;
        [ObservableProperty] private bool _hasMap;
        [ObservableProperty] private bool _hasWafers;
        [ObservableProperty] private List<WaferBinRow> _binRows;

        public WaferViewModel()
        {
            EmptyText = Loc.T("Str.Wafer.FileEmpty");
        }

        public void SetFile(StdfFile file)
        {
            _file = file;
            _map = null;
            Wafers = null;
            SelectedWafer = null;
            NotchMode = 0;
            HasMap = false;
            HasWafers = false;
            EmptyText = Loc.T("Str.Wafer.FileEmpty");
            StatsText = "";
            HoverText = "";
            BinRows = null;
            HoveredDie = null;
        }

        public void EnsureBuilt()
        {
            if (_file == null)
            {
                EmptyText = Loc.T("Str.Wafer.FileEmpty");
                HasMap = false;
                HasWafers = false;
                return;
            }
            if (_map == null)
            {
                try
                {
                    _map = WaferMap.Build(_file);
                }
                catch (Exception ex)
                {
                    EmptyText = Loc.T("Str.Wafer.BuildFailed", ex.Message);
                    HasMap = false;
                    HasWafers = false;
                    Wafers = null;
                    SelectedWafer = null;
                    return;
                }
            }
            if (_map.Wafers.Count == 0)
            {
                EmptyText = Loc.T("Str.Wafer.NoCoords");
                HasMap = false;
                HasWafers = false;
                Wafers = null;
                SelectedWafer = null;
                return;
            }

            Wafers = _map.Wafers;
            HasWafers = true;
            if (SelectedWafer == null || !Wafers.Contains(SelectedWafer))
                SelectedWafer = Wafers[0];
            else
                UpdateView();
        }

        public void RefreshLanguage()
        {
            if (_file == null || _map == null)
            {
                EmptyText = Loc.T("Str.Wafer.FileEmpty");
                StatsText = "";
                HoverText = "";
                return;
            }
            if (_map.Wafers.Count == 0)
            {
                EmptyText = Loc.T("Str.Wafer.NoCoords");
                StatsText = "";
                HoverText = "";
                HasMap = false;
                return;
            }
            UpdateView();
        }

        partial void OnSelectedWaferChanged(WaferMapWafer value)
        {
            UpdateView();
        }

        partial void OnNotchModeChanged(int value)
        {
            UpdateView();
        }

        partial void OnHoveredDieChanged(WaferDie value)
        {
            if (value == null)
            {
                HoverText = HasMap ? Loc.T("Str.Wafer.ZoomHint") : "";
                return;
            }
            string text = Loc.T("Str.Wafer.Hover", value.X, value.Y, value.HardBin, value.SiteNum);
            if (value.Retests > 0)
                text += "   " + Loc.T("Str.Wafer.RetestMark", value.Retests);
            HoverText = text;
        }

        private void UpdateView()
        {
            WaferMapWafer wafer = SelectedWafer;
            if (wafer == null)
                return;

            char notch;
            string sourceKey;
            if (NotchMode > 0)
            {
                notch = "UDLR"[NotchMode - 1];
                sourceKey = "Str.Wafer.NotchManual";
            }
            else
            {
                bool fromWcr = IsNotch(wafer.Flat);
                notch = fromWcr ? wafer.Flat : 'D';
                sourceKey = fromWcr ? "Str.Wafer.NotchWcr" : "Str.Wafer.NotchDefault";
            }
            Notch = notch;
            HoveredDie = null;

            long cols = (long)wafer.MaxX - wafer.MinX + 1;
            long rows = (long)wafer.MaxY - wafer.MinY + 1;
            if (cols * rows > 4000000)
            {
                HasMap = false;
                EmptyText = Loc.T("Str.Wafer.BadExtent",
                    cols.ToString("N0", CultureInfo.InvariantCulture),
                    rows.ToString("N0", CultureInfo.InvariantCulture));
                StatsText = "";
                HoverText = "";
                BinRows = null;
                return;
            }

            HasMap = true;
            EmptyText = "";
            StatsText = BuildStats(wafer, notch, sourceKey);
            BinRows = BuildLegend(wafer);
            HoverText = Loc.T("Str.Wafer.ZoomHint");
        }

        private static string BuildStats(WaferMapWafer wafer, char notch, string sourceKey)
        {
            var text = new StringBuilder();
            if (wafer.WaferId.Length > 0)
                text.Append("WAFER_ID=").Append(wafer.WaferId).Append("   ");
            text.Append(Loc.T("Str.Wafer.Die")).Append('=').Append(UiFormat.N(wafer.Dice.Count));
            text.Append("   ").Append(Loc.T("Str.Kpi.Yield")).Append('=');
            if (wafer.PartCount > 0)
                text.Append((100.0 * wafer.GoodCount / wafer.PartCount).ToString("F2", CultureInfo.InvariantCulture)).Append('%');
            else
                text.Append(Loc.T("Str.Word.Na"));
            text.Append("   ").Append(Loc.T("Str.Wafer.Retest")).Append('=').Append(UiFormat.N(wafer.RetestCount));
            text.Append("   ").Append(Loc.T(sourceKey, DirectionWord(notch)));
            return text.ToString();
        }

        private static List<WaferBinRow> BuildLegend(WaferMapWafer wafer)
        {
            var bins = new List<int>(wafer.BinCounts.Keys);
            bins.Sort();
            var rows = new List<WaferBinRow>(bins.Count);
            int total = wafer.Dice.Count;
            foreach (int bin in bins)
            {
                int count = wafer.BinCounts[bin];
                rows.Add(new WaferBinRow
                {
                    Fill = BinPalette.BrushForBin(bin),
                    BinText = "BIN " + bin.ToString(CultureInfo.InvariantCulture),
                    CountText = count.ToString("N0", CultureInfo.InvariantCulture),
                    PercentText = (100.0 * count / total).ToString("F2", CultureInfo.InvariantCulture) + "%",
                });
            }
            return rows;
        }

        private static bool IsNotch(char value)
        {
            return value == 'U' || value == 'D' || value == 'L' || value == 'R';
        }

        private static string DirectionWord(char notch)
        {
            switch (notch)
            {
                case 'U': return Loc.T("Str.Wafer.Up");
                case 'L': return Loc.T("Str.Wafer.Left");
                case 'R': return Loc.T("Str.Wafer.Right");
                default: return Loc.T("Str.Wafer.Down");
            }
        }
    }
}
