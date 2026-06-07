using System.Windows;
using PdfPrintUtility.Services;

namespace PdfPrintUtility.Views
{
    public partial class HistoryWindow : Window
    {
        public HistoryWindow()
        {
            InitializeComponent();
            LoadHistory();
        }

        private void LoadHistory()
        {
            var history = Logger.GetHistory();
            HistoryDataGrid.ItemsSource = history;
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadHistory();
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to clear all print history?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                Logger.ClearHistory();
                LoadHistory();
            }
        }
    }
}
