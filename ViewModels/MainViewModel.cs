using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StdfViewer.Services;

namespace StdfViewer.ViewModels
{
    public sealed partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly HistoryStore _store;
        private readonly IDialogService _dialogs;
        private StdfFile _file;
        private bool _loading;

        public SummaryViewModel Summary { get; }
        public BrowseViewModel Browse { get; }
        public HistoryViewModel History { get; }
        public CompareViewModel Compare { get; }
        public WaferViewModel Wafer { get; }

        [ObservableProperty] private string _statusText;
        [ObservableProperty] private string _languageButtonText;
        [ObservableProperty] private int _selectedTabIndex;
        [ObservableProperty] private bool _isBrowseTab;
        [ObservableProperty] private bool _canOpen = true;

        public MainViewModel(HistoryStore store, IDialogService dialogs)
        {
            _store = store;
            _dialogs = dialogs;
            Summary = new SummaryViewModel();
            Browse = new BrowseViewModel();
            History = new HistoryViewModel(store, dialogs);
            Compare = new CompareViewModel(store);
            Wafer = new WaferViewModel();
            Browse.PropertyChanged += OnBrowsePropertyChanged;
            Loc.LanguageChanged += OnLanguageChanged;
            LanguageButtonText = Loc.IsChinese ? "English" : "中文";
            StatusText = Loc.T("Str.Status.NoFile");
        }

        partial void OnSelectedTabIndexChanged(int value)
        {
            IsBrowseTab = value == 1;
            if (value == 4)
                Wafer.EnsureBuilt();
        }

        [RelayCommand]
        private async Task Open()
        {
            if (_loading)
                return;
            string path = _dialogs.PickStdfFile();
            if (path == null)
                return;
            await LoadAsync(path);
        }

        public async Task LoadAsync(string path)
        {
            if (_loading || string.IsNullOrEmpty(path))
                return;

            _loading = true;
            CanOpen = false;
            StatusText = Loc.T("Str.Status.Scanning", path);
            StdfFile file;
            try
            {
                file = await Task.Run(delegate { return StdfFile.Scan(path); });
            }
            finally
            {
                _loading = false;
                CanOpen = true;
            }

            StdfFile old = _file;
            _file = file;
            Summary.SetFile(file);
            Browse.SetFile(file);
            Wafer.SetFile(file);
            _store.Record(file);
            History.EnsureSelection();
            UpdateStatus();
            if (SelectedTabIndex == 4)
                Wafer.EnsureBuilt();
            if (old != null)
                old.Close();
        }

        [RelayCommand]
        private void ToggleLanguage()
        {
            Loc.SetLanguage(Loc.IsChinese ? Loc.English : Loc.Chinese);
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            LanguageButtonText = Loc.IsChinese ? "English" : "中文";
            Summary.RefreshLanguage();
            Browse.RefreshLanguage();
            History.RefreshLanguage();
            Compare.RefreshLanguage();
            if (SelectedTabIndex == 4)
                Wafer.EnsureBuilt();
            Wafer.RefreshLanguage();
            UpdateStatus();
        }

        private void OnBrowsePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BrowseViewModel.ListedCount)
                || e.PropertyName == nameof(BrowseViewModel.FilteredTotal)
                || e.PropertyName == nameof(BrowseViewModel.IsListCapped))
                UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (_file == null)
            {
                StatusText = Loc.T("Str.Status.NoFile");
                return;
            }

            var text = new StringBuilder();
            text.Append(Path.GetFileName(_file.Path))
                .Append("   ").Append(UiFormat.N(_file.FileLength)).Append(" B")
                .Append("   CPU_TYPE=").Append(_file.CpuType.ToString(CultureInfo.InvariantCulture))
                .Append(" STDF_VER=").Append(_file.StdfVersion.ToString(CultureInfo.InvariantCulture))
                .Append("   ").Append(UiFormat.EndianWord(_file.BigEndian))
                .Append("   ").Append(Loc.T("Str.Lbl.Records")).Append('=').Append(UiFormat.N(_file.Count))
                .Append("   ").Append(Loc.T("Str.Lbl.Types")).Append('=').Append(_file.Types.Count.ToString(CultureInfo.InvariantCulture))
                .Append("   ").Append(Loc.T("Str.Lbl.Scan")).Append('=').Append(_file.ScanTime.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)).Append("ms")
                .Append("   ").Append(Loc.T("Str.Lbl.Listed")).Append('=').Append(UiFormat.N(Browse.ListedCount));
            if (Browse.IsListCapped)
                text.Append(Loc.T("Str.Status.ListCapped", BrowseViewModel.ListRowLimit.ToString("N0", CultureInfo.InvariantCulture)));
            if (!string.IsNullOrEmpty(_file.Note))
                text.Append("   ").Append(Loc.T("Str.Lbl.Note")).Append(": ").Append(_file.Note);
            if (!string.IsNullOrEmpty(_store.LastError))
                text.Append("   ").Append(Loc.T("Str.Lbl.History")).Append(": ").Append(_store.LastError);
            if (!string.IsNullOrEmpty(UiSettings.LastError))
                text.Append("   ").Append(Loc.T("Str.Lbl.Settings")).Append(": ").Append(UiSettings.LastError);
            StatusText = text.ToString();
        }

        public void Dispose()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            Browse.PropertyChanged -= OnBrowsePropertyChanged;
            if (_file != null)
            {
                _file.Close();
                _file = null;
            }
        }
    }
}
