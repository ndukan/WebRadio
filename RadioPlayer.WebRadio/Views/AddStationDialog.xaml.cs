using System.Windows;

namespace RadioPlayer.WebRadio.Views
{
    public partial class AddStationDialog : Window
    {
        public string StationName { get; private set; }
        public string StationUrl { get; private set; }

        public AddStationDialog()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            StationName = NameTextBox.Text;
            StationUrl = UrlTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}