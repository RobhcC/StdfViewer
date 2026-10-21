using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StdfViewer.ViewModels
{
    public sealed partial class SummaryViewModel : ObservableObject
    {
        private StdfFile _file;
        private SummaryResult _summary;

        [ObservableProperty] private bool _hasFile;
        [ObservableProperty] private List<SummarySection> _sections;
        [ObservableProperty] private string _sizeText = "";
        [ObservableProperty] private string _recordsText = "";
        [ObservableProperty] private string _typesText = "";
        [ObservableProperty] private string _dutsText = "";
        [ObservableProperty] private string _yieldText = "";
        [ObservableProperty] private string _scanText = "";
        [ObservableProperty] private string _endianText = "";

        public void SetFile(StdfFile file)
        {
            _file = file;
            if (file == null)
            {
                HasFile = false;
                _summary = null;
                Sections = null;
                SizeText = "";
                RecordsText = "";
                TypesText = "";
                DutsText = "";
                YieldText = "";
                ScanText = "";
                EndianText = "";
                return;
            }
            HasFile = true;
            Build();
        }

        public void RefreshLanguage()
        {
            if (_file != null)
                Build();
        }

        private void Build()
        {
            _summary = StdfSummary.Build(_file);
            Sections = _summary.Sections;
            SizeText = UiFormat.N(_summary.FileLength) + " B";
            RecordsText = UiFormat.N(_summary.RecordCount);
            TypesText = _summary.TypeCount.ToString(CultureInfo.InvariantCulture);
            DutsText = UiFormat.N(_summary.DutCount);
            YieldText = _summary.YieldPercent < 0
                ? Loc.T("Str.Word.Na")
                : _summary.YieldPercent.ToString("F2", CultureInfo.InvariantCulture) + "%";
            ScanText = _summary.ScanMs.ToString("F0", CultureInfo.InvariantCulture) + " ms";
            EndianText = UiFormat.EndianWord(_summary.BigEndian);
        }
    }
}
