using System.Windows.Controls;

namespace StdfViewer.Views
{
    public partial class BrowseView : UserControl
    {
        public BrowseView()
        {
            InitializeComponent();
        }

        private void OnRecordsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_records.SelectedItem != null)
                _records.ScrollIntoView(_records.SelectedItem);
        }
    }
}
