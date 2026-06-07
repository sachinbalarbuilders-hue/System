using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PdfiumViewer;
using PdfPrintUtility.Models;
using PdfPrintUtility.Services;

namespace PdfPrintUtility.Views
{
    public partial class PageArrangerWindow : Window
    {
        private readonly List<PrintFileItem> _sourceFiles;
        private ObservableCollection<PdfPageItem> _pages;

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        public PageArrangerWindow(List<PrintFileItem> sourceFiles)
        {
            InitializeComponent();
            _sourceFiles = sourceFiles;
            _pages = new ObservableCollection<PdfPageItem>();
            PagesListBox.ItemsSource = _pages;

            Loaded += PageArrangerWindow_Loaded;
        }

        private async void PageArrangerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadPagesAsync();
        }

        private async Task LoadPagesAsync()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            SaveBtn.IsEnabled = false;

            int totalFiles = _sourceFiles.Count;
            int totalPagesExtracted = 0;

            await Task.Run(() =>
            {
                for (int i = 0; i < totalFiles; i++)
                {
                    var fileItem = _sourceFiles[i];
                    if (!File.Exists(fileItem.FilePath)) continue;
                    
                    string ext = Path.GetExtension(fileItem.FilePath).ToLowerInvariant();
                    if (ext != ".pdf") continue; // Merging only supports PDFs right now

                    try
                    {
                        using (var doc = PdfDocument.Load(fileItem.FilePath))
                        {
                            for (int pageIdx = 0; pageIdx < doc.PageCount; pageIdx++)
                            {
                                int currentFileIndex = i;
                                int currentPageIdx = pageIdx;
                                int pageCount = doc.PageCount;

                                Dispatcher.Invoke(() =>
                                {
                                    LoadingText.Text = $"Extracting pages from file {currentFileIndex + 1} of {totalFiles}...";
                                    LoadingProgress.Value = ((double)currentPageIdx / pageCount) * 100;
                                });

                                // Render thumbnail
                                SizeF pageSize = doc.PageSizes[pageIdx];
                                float dpi = 48f; // Thumbnail DPI
                                int width = Math.Max(1, (int)(pageSize.Width * dpi / 72f));
                                int height = Math.Max(1, (int)(pageSize.Height * dpi / 72f));

                                using (var bmp = (Bitmap)doc.Render(pageIdx, width, height, dpi, dpi, false))
                                {
                                    if (fileItem.Rotation != 0)
                                    {
                                        if (fileItem.Rotation == 90) bmp.RotateFlip(RotateFlipType.Rotate90FlipNone);
                                        else if (fileItem.Rotation == 180) bmp.RotateFlip(RotateFlipType.Rotate180FlipNone);
                                        else if (fileItem.Rotation == 270) bmp.RotateFlip(RotateFlipType.Rotate270FlipNone);
                                    }

                                    IntPtr hBitmap = bmp.GetHbitmap();
                                    try
                                    {
                                        BitmapSource bs = Imaging.CreateBitmapSourceFromHBitmap(
                                            hBitmap, IntPtr.Zero, Int32Rect.Empty,
                                            BitmapSizeOptions.FromEmptyOptions());
                                        bs.Freeze();

                                        var pageItem = new PdfPageItem
                                        {
                                            SourceFilePath = fileItem.FilePath,
                                            SourcePageIndex = pageIdx,
                                            Rotation = fileItem.Rotation,
                                            Thumbnail = bs,
                                            DisplayName = $"{Path.GetFileName(fileItem.FilePath)}\nPage {pageIdx + 1}"
                                        };

                                        Dispatcher.Invoke(() =>
                                        {
                                            _pages.Add(pageItem);
                                            totalPagesExtracted++;
                                        });
                                    }
                                    finally
                                    {
                                        DeleteObject(hBitmap);
                                    }
                                }
                            }
                        }
                    }
                    catch { /* Ignore unreadable PDFs */ }
                }
            });

            StatusText.Text = $"{totalPagesExtracted} pages loaded from {totalFiles} files.";
            LoadingOverlay.Visibility = Visibility.Collapsed;
            SaveBtn.IsEnabled = _pages.Count > 0;
        }

        private void RotatePage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is PdfPageItem item)
            {
                // To rotate visually, we can't easily re-render without reloading the doc.
                // For simplicity, we just change the data and use a LayoutTransform or re-render.
                // Let's just do a quick re-render of the thumbnail since we have the path.
                item.Rotation = (item.Rotation + 90) % 360;
                
                try
                {
                    using (var doc = PdfDocument.Load(item.SourceFilePath))
                    {
                        SizeF pageSize = doc.PageSizes[item.SourcePageIndex];
                        float dpi = 48f;
                        int width = Math.Max(1, (int)(pageSize.Width * dpi / 72f));
                        int height = Math.Max(1, (int)(pageSize.Height * dpi / 72f));

                        using (var bmp = (Bitmap)doc.Render(item.SourcePageIndex, width, height, dpi, dpi, false))
                        {
                            if (item.Rotation == 90) bmp.RotateFlip(RotateFlipType.Rotate90FlipNone);
                            else if (item.Rotation == 180) bmp.RotateFlip(RotateFlipType.Rotate180FlipNone);
                            else if (item.Rotation == 270) bmp.RotateFlip(RotateFlipType.Rotate270FlipNone);

                            IntPtr hBitmap = bmp.GetHbitmap();
                            try
                            {
                                BitmapSource bs = Imaging.CreateBitmapSourceFromHBitmap(
                                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                                bs.Freeze();
                                item.Thumbnail = bs;
                            }
                            finally
                            {
                                DeleteObject(hBitmap);
                            }
                        }
                    }
                    // Refresh the item in UI
                    int idx = _pages.IndexOf(item);
                    _pages[idx] = item;
                }
                catch { }
            }
        }

        private void DeletePage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is PdfPageItem item)
            {
                _pages.Remove(item);
                StatusText.Text = $"{_pages.Count} pages remaining.";
                if (_pages.Count == 0) SaveBtn.IsEnabled = false;
            }
        }

        private async void RotateAllBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_pages.Count == 0) return;

            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingText.Text = "Rotating all pages...";
            SaveBtn.IsEnabled = false;

            await Task.Run(() =>
            {
                for (int i = 0; i < _pages.Count; i++)
                {
                    var item = _pages[i];
                    item.Rotation = (item.Rotation + 90) % 360;

                    try
                    {
                        using (var doc = PdfDocument.Load(item.SourceFilePath))
                        {
                            SizeF pageSize = doc.PageSizes[item.SourcePageIndex];
                            float dpi = 48f;
                            int width = Math.Max(1, (int)(pageSize.Width * dpi / 72f));
                            int height = Math.Max(1, (int)(pageSize.Height * dpi / 72f));

                            using (var bmp = (Bitmap)doc.Render(item.SourcePageIndex, width, height, dpi, dpi, false))
                            {
                                if (item.Rotation == 90) bmp.RotateFlip(RotateFlipType.Rotate90FlipNone);
                                else if (item.Rotation == 180) bmp.RotateFlip(RotateFlipType.Rotate180FlipNone);
                                else if (item.Rotation == 270) bmp.RotateFlip(RotateFlipType.Rotate270FlipNone);

                                IntPtr hBitmap = bmp.GetHbitmap();
                                try
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        BitmapSource bs = Imaging.CreateBitmapSourceFromHBitmap(
                                            hBitmap, IntPtr.Zero, Int32Rect.Empty,
                                            BitmapSizeOptions.FromEmptyOptions());
                                        bs.Freeze();
                                        item.Thumbnail = bs;
                                        _pages[i] = item; // Trigger property changed via ObservableCollection
                                    });
                                }
                                finally
                                {
                                    DeleteObject(hBitmap);
                                }
                            }
                        }
                    }
                    catch { }
                }
            });

            LoadingOverlay.Visibility = Visibility.Collapsed;
            SaveBtn.IsEnabled = true;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save Merged PDF",
                Filter = "PDF Files|*.pdf",
                DefaultExt = ".pdf",
                FileName = "MergedDocument.pdf"
            };

            if (dialog.ShowDialog() == true)
            {
                string outPath = dialog.FileName;
                
                LoadingOverlay.Visibility = Visibility.Visible;
                LoadingText.Text = "Saving merged PDF...";
                SaveBtn.IsEnabled = false;

                var pagesList = _pages.ToList();
                var result = await MergeManager.MergePagesAsync(pagesList, outPath);

                LoadingOverlay.Visibility = Visibility.Collapsed;
                SaveBtn.IsEnabled = true;

                if (result.Success)
                {
                    MessageBox.Show($"Successfully merged {result.PagesMerged} pages into {Path.GetFileName(outPath)}.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                else
                {
                    MessageBox.Show("Merge failed: " + result.ErrorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
