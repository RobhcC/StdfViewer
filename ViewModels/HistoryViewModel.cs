using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StdfViewer.Services;

namespace StdfViewer.ViewModels
{
    public sealed partial class HistoryViewModel : ObservableObject
    {
        private readonly HistoryStore _store;
        private readonly IDialogService _dialogs;

        [ObservableProperty] private HistoryEntry _selectedEntry;
        [ObservableProperty] private string _summaryText;
        [ObservableProperty] private List<HistoryTypeStat> _types;

        public ObservableCollection<HistoryEntry> Entries { get { return _store.Entries; } }

        public HistoryViewModel(HistoryStore store, IDialogService dialogs)
        {
            _store = store;
            _dialogs = dialogs;
            SummaryText = Loc.T("Str.History.Placeholder");
        }

        public void EnsureSelection()
        {
            if (SelectedEntry == null && Entries.Count > 0)
                SelectedEntry = Entries[0];
        }

        partial void OnSelectedEntryChanged(HistoryEntry value)
        {
            if (value == null)
            {
                SummaryText = Loc.T("Str.History.Placeholder");
                Types = null;
                return;
            }
            Show(value);
        }

        [RelayCommand]
        private void Delete()
        {
            if (SelectedEntry == null)
            {
                SummaryText = Loc.T("Str.History.DeleteEmpty");
                return;
            }
            bool confirmed = _dialogs.Confirm(Loc.T("Str.History.DeleteTitle"),
                Loc.T("Str.History.DeleteConfirm", SelectedEntry.Path));
            if (!confirmed)
                return;

            _store.Remove(SelectedEntry);
            if (Entries.Count > 0)
            {
                SelectedEntry = Entries[0];
            }
            else
            {
                SelectedEntry = null;
                SummaryText = Loc.T("Str.History.Placeholder");
                Types = null;
            }
        }

        public void RefreshLanguage()
        {
            if (SelectedEntry != null)
            {
                Show(SelectedEntry);
            }
            else
            {
                SummaryText = Loc.T("Str.History.Placeholder");
                Types = null;
            }
        }

        private void Show(HistoryEntry entry)
        {
            var text = new StringBuilder();
            text.Append(entry.Path)
                .Append("   ").Append(UiFormat.N(entry.Size)).Append(" B")
                .Append("   CPU_TYPE=").Append(entry.CpuType.ToString(CultureInfo.InvariantCulture))
                .Append(" STDF_VER=").Append(entry.StdfVer.ToString(CultureInfo.InvariantCulture))
                .Append("   ").Append(UiFormat.EndianWord(entry.BigEndian))
                .Append("   ").Append(Loc.T("Str.Lbl.Records")).Append('=').Append(UiFormat.N(entry.Records))
                .Append("   ").Append(Loc.T("Str.Lbl.Types")).Append('=').Append(entry.TypeCount.ToString(CultureInfo.InvariantCulture))
                .Append("   ").Append(Loc.T("Str.Lbl.Scan")).Append('=').Append(entry.ScanMs.ToString("F0", CultureInfo.InvariantCulture)).Append("ms")
                .Append("   ").Append(Loc.T("Str.Lbl.Parsed")).Append('=').Append(entry.ParsedAt);
            if (entry.Note.Length > 0)
                text.Append("   ").Append(Loc.T("Str.Lbl.Note")).Append(": ").Append(entry.Note);
            SummaryText = text.ToString();
            Types = entry.Types;
        }
    }
}
