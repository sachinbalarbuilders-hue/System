using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using PdfPrintUtility.Models;
using PdfPrintUtility.Services;

namespace PdfPrintUtility.Views
{
    public partial class ProgressWindow : Window
    {
        private readonly List<string> _files;

        public ProgressWindow(List<string> files)
        {
            InitializeComponent();
            _files = files;
            Loaded += ProgressWindow_Loaded;
        }

        private async void ProgressWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_files == null || _files.Count == 0)
            {
                Application.Current.Shutdown();
                return;
            }

            PrintProgressBar.Maximum = _files.Count;
            int successCount = 0;
            int failCount = 0;

            var settings = SettingsManager.Load();

            // Run printing on a background thread to keep UI responsive
            await Task.Run(() =>
            {
                for (int i = 0; i < _files.Count; i++)
                {
                    string file = _files[i];
                    string fileName = Path.GetFileName(file);
                    
                    Dispatcher.Invoke(() =>
                    {
                        StatusTextBlock.Text = $"Printing {i + 1} of {_files.Count}...";
                        DetailsTextBlock.Text = fileName;
                        PrintProgressBar.Value = i;
                    });

                    bool success = PrintManager.PrintPdf(file, settings, out string errorMessage);
                    
                    if (success)
                    {
                        successCount++;
                        Logger.LogPrint(fileName, settings.DefaultPrinter, "Success");
                    }
                    else
                    {
                        failCount++;
                        Logger.LogPrint(fileName, settings.DefaultPrinter, "Failed", errorMessage);
                    }
                }
            });

            PrintProgressBar.Value = _files.Count;
            StatusTextBlock.Text = "Finished!";
            DetailsTextBlock.Text = $"Successfully printed: {successCount}, Failed: {failCount}";

            string summaryMsg = $"Print job completed.\n\nTotal files: {_files.Count}\nSuccess: {successCount}\nFailed: {failCount}";
            MessageBox.Show(summaryMsg, "Print Summary", MessageBoxButton.OK, MessageBoxImage.Information);
            
            Application.Current.Shutdown();
        }
    }
}
