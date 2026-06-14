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
        private readonly PrintSettings _settings;
        private readonly string _pageRange;

        public ProgressWindow(List<string> files, PrintSettings settings, string pageRange)
        {
            InitializeComponent();
            _files = files;
            _settings = settings;
            _pageRange = pageRange;
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

            var progress = new Progress<(int Index, string FileName)>(data =>
            {
                StatusTextBlock.Text = $"Printing {data.Index + 1} of {_files.Count}...";
                DetailsTextBlock.Text = data.FileName;
                PrintProgressBar.Value = data.Index;
            });
            var progressReporter = (IProgress<(int, string)>)progress;

            // Run printing on a background thread to keep UI responsive
            await Task.Run(() =>
            {
                for (int i = 0; i < _files.Count; i++)
                {
                    string file = _files[i];
                    string fileName = Path.GetFileName(file);

                    progressReporter.Report((i, fileName));

                    bool success = PrintManager.PrintFile(file, _settings, _pageRange, out string errorMessage);

                    if (success)
                    {
                        successCount++;
                        Logger.LogPrint(fileName, _settings.DefaultPrinter, "Success");
                    }
                    else
                    {
                        failCount++;
                        Logger.LogPrint(fileName, _settings.DefaultPrinter, "Failed", errorMessage);
                    }
                }
            });

            PrintProgressBar.Value = _files.Count;
            StatusTextBlock.Text = "Finished sending to printer!";
            DetailsTextBlock.Text = $"Successfully queued: {successCount}, Failed: {failCount}";

            string summaryMsg = $"All documents have been sent to the printer spooler.\n\nTotal files: {_files.Count}\nSuccess: {successCount}\nFailed: {failCount}\n\nThe physical printer may still be printing.";
            MessageBox.Show(summaryMsg, "Print Job Queued", MessageBoxButton.OK, MessageBoxImage.Information);

            Application.Current.Shutdown();
        }
    }
}
