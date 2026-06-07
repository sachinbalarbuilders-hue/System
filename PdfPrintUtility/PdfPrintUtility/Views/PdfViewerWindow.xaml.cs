using System;
using System.IO;
using System.Windows;

namespace PdfPrintUtility.Views
{
    public partial class PdfViewerWindow : Window
    {
        private readonly string _filePath;

        public PdfViewerWindow(string filePath)
        {
            InitializeComponent();
            _filePath = filePath;
            
            Loaded += PdfViewerWindow_Loaded;
            Closed += PdfViewerWindow_Closed;
        }

        private async void PdfViewerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (File.Exists(_filePath))
            {
                Title = $"Native PDF Viewer - {Path.GetFileName(_filePath)}";
                try
                {
                    string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfPrintUtility", "WebView2");
                    var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
                    await WebViewControl.EnsureCoreWebView2Async(env);
                    // Pass the file path as a URI
                    WebViewControl.Source = new Uri(_filePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not load PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                }
            }
        }

        private void PdfViewerWindow_Closed(object sender, EventArgs e)
        {
            try
            {
                WebViewControl.Dispose();
            }
            catch { }
        }
    }
}
