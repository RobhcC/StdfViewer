using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace StdfViewer
{
    public sealed partial class ViewerWindow : Window
    {
        private const int ListRowLimit = 20000;

        private StdfFile _file;
        private int[] _filter;
        private bool _loading;

        public ViewerWindow()
        {
            InitializeComponent();
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

        protected override void OnClosed(EventArgs e)
        {
            if (_file != null)
                _file.Close();
            base.OnClosed(e);
        }
    }
}
