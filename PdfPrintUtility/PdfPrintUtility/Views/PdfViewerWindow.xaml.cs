using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;

namespace PdfPrintUtility.Views
{
    public class TabData
    {
        public string HeaderText { get; set; }
        public string FilePath { get; set; }
        public WebView2 WebView { get; set; }
    }

    public partial class PdfViewerWindow : Window
    {
        public PdfViewerWindow(string filePath)
        {
            InitializeComponent();

            Loaded += PdfViewerWindow_Loaded;
            Closed += PdfViewerWindow_Closed;

            if (!string.IsNullOrEmpty(filePath))
            {
                AddTab(filePath);
            }
        }

        private void PdfViewerWindow_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public async void AddTab(string filePath)
        {
            if (!File.Exists(filePath)) return;

            var webView = new WebView2();

            var tabData = new TabData
            {
                HeaderText = Path.GetFileName(filePath),
                FilePath = filePath,
                WebView = webView
            };

            var tabItem = new TabItem
            {
                Header = tabData,
                Content = webView,
                Tag = tabData
            };

            PdfTabControl.Items.Add(tabItem);
            PdfTabControl.SelectedItem = tabItem;

            try
            {
                string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfPrintUtility", "WebView2");
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await webView.EnsureCoreWebView2Async(env);

                webView.Source = new Uri(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                PdfTabControl.Items.Remove(tabItem);
                if (PdfTabControl.Items.Count == 0) Close();
            }
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var tabData = button?.Tag as TabData;

            if (tabData != null)
            {
                TabItem tabToRemove = null;
                foreach (TabItem item in PdfTabControl.Items)
                {
                    if (item.Tag == tabData)
                    {
                        tabToRemove = item;
                        break;
                    }
                }

                if (tabToRemove != null)
                {
                    PdfTabControl.Items.Remove(tabToRemove);

                    try
                    {
                        tabData.WebView.Dispose();
                    }
                    catch { }

                    if (PdfTabControl.Items.Count == 0)
                    {
                        Close();
                    }
                }
            }
        }

        private void PdfViewerWindow_Closed(object sender, EventArgs e)
        {
            if (App.CurrentViewer == this)
            {
                App.CurrentViewer = null;
            }

            foreach (TabItem item in PdfTabControl.Items)
            {
                if (item.Tag is TabData data && data.WebView != null)
                {
                    try
                    {
                        data.WebView.Dispose();
                    }
                    catch { }
                }
            }
        }
    }
}
