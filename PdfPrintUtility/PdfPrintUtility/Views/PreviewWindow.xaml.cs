using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using PdfiumViewer;

namespace PdfPrintUtility.Views
{
    public partial class PreviewWindow : Window
    {
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private PdfDocument? _document;
        private int _currentPage = 0;
        private int _pageCount = 0;
        private double _zoomFactor = 1.0;
        private string _zoomMode = "fit_width"; // fit_width | fit_page | numeric
        private const float BaseDpi = 150f;

        public PreviewWindow(string filePath)
        {
            InitializeComponent();

            try
            {
                _document = PdfDocument.Load(filePath);
                _pageCount = _document.PageCount;
                FileNameLabel.Text = filePath;
                Title = $"Preview — {Path.GetFileName(filePath)}";

                // Render after layout is ready so we know the ScrollViewer size
                Loaded += (s, e) => RenderPage(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load PDF for preview:\n{ex.Message}",
                    "Preview Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
            }

            Closed += (s, e) => { _document?.Dispose(); _document = null; };
        }

        private void RenderPage(int pageIndex)
        {
            if (_document == null || pageIndex < 0 || pageIndex >= _pageCount) return;

            _currentPage = pageIndex;
            PageLabel.Text = $"Page {_currentPage + 1} of {_pageCount}";
            PrevBtn.IsEnabled = _currentPage > 0;
            NextBtn.IsEnabled = _currentPage < _pageCount - 1;

            var pageSize = _document.PageSizes[pageIndex]; // in points (1/72 inch)

            float dpi;
            if (_zoomMode == "fit_width")
            {
                double available = Math.Max(100, PreviewScrollViewer.ActualWidth - 60);
                double pageWidthAt150 = pageSize.Width * BaseDpi / 72.0;
                dpi = (float)(BaseDpi * (available / pageWidthAt150));
            }
            else if (_zoomMode == "fit_page")
            {
                double availW = Math.Max(100, PreviewScrollViewer.ActualWidth - 60);
                double availH = Math.Max(100, PreviewScrollViewer.ActualHeight - 60);
                double pageWidthAt150 = pageSize.Width * BaseDpi / 72.0;
                double pageHeightAt150 = pageSize.Height * BaseDpi / 72.0;
                double scaleW = availW / pageWidthAt150;
                double scaleH = availH / pageHeightAt150;
                dpi = (float)(BaseDpi * Math.Min(scaleW, scaleH));
            }
            else
            {
                dpi = (float)(BaseDpi * _zoomFactor);
            }

            dpi = Math.Max(10f, dpi);

            int width = Math.Max(1, (int)(pageSize.Width * dpi / 72.0));
            int height = Math.Max(1, (int)(pageSize.Height * dpi / 72.0));

            using var bitmap = (System.Drawing.Bitmap)_document.Render(pageIndex, width, height, dpi, dpi, false);
            var hBitmap = bitmap.GetHbitmap();
            try
            {
                var bmpSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bmpSource.Freeze();
                PreviewImage.Source = bmpSource;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        private void PrevBtn_Click(object sender, RoutedEventArgs e) => RenderPage(_currentPage - 1);
        private void NextBtn_Click(object sender, RoutedEventArgs e) => RenderPage(_currentPage + 1);

        private void ZoomComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ZoomComboBox.SelectedItem is not ComboBoxItem item) return;
            string tag = item.Tag?.ToString() ?? "fit_width";

            if (tag == "fit_width" || tag == "fit_page")
            {
                _zoomMode = tag;
            }
            else if (double.TryParse(tag, out double zoom))
            {
                _zoomMode = "numeric";
                _zoomFactor = zoom;
            }

            // Only render if the window is loaded and document is ready
            if (_document != null && PreviewScrollViewer.ActualWidth > 0)
                RenderPage(_currentPage);
        }

        private void PreviewScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Re-render on resize only when in a fit mode
            if (_document != null && (_zoomMode == "fit_width" || _zoomMode == "fit_page"))
                RenderPage(_currentPage);
        }
    }
}
