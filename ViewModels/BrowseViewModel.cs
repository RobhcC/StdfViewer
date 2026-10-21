using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StdfViewer.ViewModels
{
    public sealed partial class BrowseViewModel : ObservableObject
    {
        public const int ListRowLimit = 20000;

        private StdfFile _file;
        private int[] _filter;
        private DecodedRecord _record;
        private bool _suppressTypeChange;

        [ObservableProperty] private List<RecordTypeStat> _types;
        [ObservableProperty] private List<RecordRow> _records;
        [ObservableProperty] private List<FieldRow> _fields;
        [ObservableProperty] private RecordTypeStat _selectedType;
        [ObservableProperty] private RecordRow _selectedRecord;
        [ObservableProperty] private string _jumpText = "";
        [ObservableProperty] private string _recordHeadText;
        [ObservableProperty] private int _listedCount;
        [ObservableProperty] private int _filteredTotal;
        [ObservableProperty] private bool _isListCapped;

        public BrowseViewModel()
        {
            RecordHeadText = Loc.T("Str.Browse.Fields");
        }

        public void SetFile(StdfFile file)
        {
            _file = file;
            _record = null;
            _filter = null;
            _suppressTypeChange = true;
            SelectedType = null;
            _suppressTypeChange = false;
            Types = file == null ? null : file.Types;
            ShowRecords();
        }

        partial void OnSelectedTypeChanged(RecordTypeStat value)
        {
            if (_suppressTypeChange)
                return;
            _filter = value == null ? null : value.Seqs;
            ShowRecords();
        }

        partial void OnSelectedRecordChanged(RecordRow value)
        {
            if (_file == null || value == null)
            {
                Fields = null;
                if (_file != null)
                    RecordHeadText = Loc.T("Str.Browse.Fields");
                return;
            }
            Decode(value.Seq);
        }

        [RelayCommand]
        private void AllTypes()
        {
            if (SelectedType == null)
                ShowRecords();
            else
                SelectedType = null;
        }

        [RelayCommand]
        private void Jump()
        {
            if (_file == null || Records == null)
                return;
            long no;
            if (!long.TryParse(JumpText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out no))
                return;
            int index = Records.FindIndex(delegate(RecordRow row) { return row.No == no; });
            if (index < 0)
                return;
            SelectedRecord = Records[index];
        }

        public void RefreshLanguage()
        {
            if (_record != null)
                Decode(_record.Seq);
            else
                RecordHeadText = Loc.T("Str.Browse.Fields");
        }

        private void ShowRecords()
        {
            if (_file == null)
            {
                Records = null;
                Fields = null;
                RecordHeadText = Loc.T("Str.Browse.Fields");
                ListedCount = 0;
                FilteredTotal = 0;
                IsListCapped = false;
                return;
            }

            List<RecordRow> rows = RecordListBuilder.Build(_file, _filter, ListRowLimit);
            FilteredTotal = _filter == null ? _file.Count : _filter.Length;
            ListedCount = rows.Count;
            IsListCapped = FilteredTotal > rows.Count;
            Records = rows;
            if (rows.Count > 0)
            {
                SelectedRecord = rows[0];
            }
            else
            {
                SelectedRecord = null;
                Fields = null;
                RecordHeadText = Loc.T("Str.Browse.Fields");
            }
        }

        private void Decode(int seq)
        {
            DecodedRecord rec = StdfDecoder.Decode(_file, seq);
            _record = rec;
            RecordHeadText = "#" + (seq + 1) + " " + rec.RecordName + " (" + rec.TypSub + ")"
                + "  offset=0x" + rec.Offset.ToString("X", CultureInfo.InvariantCulture)
                + "  REC_LEN=" + rec.DeclaredLen + "  decoded=" + rec.Consumed + "  " + Loc.Status(rec.Status);
            Fields = rec.Rows;
        }
    }
}
