using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfPrintUtility.Services;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using Microsoft.Win32;

namespace PdfPrintUtility.Views
{
    public class ScannedPage
    {
        public string ImagePath { get; set; } = "";
        public string PageName { get; set; } = "";
    }

    public partial class ScannerWorkspaceWindow : Window
    {
        public ObservableCollection<ScannedPage> ScannedPages { get; set; } = new ObservableCollection<ScannedPage>();
        public List<string> FinalFiles { get; private set; } = new List<string>();

        private System.Windows.Point _startPoint;
        private bool _isDragging;
        private double _zoomLevel = 1.0;
        private static readonly double[] _zoomSteps = { 0.1, 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 3.0, 4.0 };

        public ScannerWorkspaceWindow(string initialFile)
        {
            InitializeComponent();
            PagesListBox.ItemsSource = ScannedPages;

            if (!string.IsNullOrEmpty(initialFile))
            {
                AddPage(initialFile);
            }
        }

        private void AddPage(string filePath)
        {
            ScannedPages.Add(new ScannedPage
            {
                ImagePath = filePath,
                PageName = $"Page {ScannedPages.Count + 1}"
            });
            if (PagesListBox.SelectedIndex == -1)
            {
                PagesListBox.SelectedIndex = 0;
            }
        }

        private void ZoomInBtn_Click(object sender, RoutedEventArgs e) => ApplyZoom(_zoomLevel * 1.25);
        private void ZoomOutBtn_Click(object sender, RoutedEventArgs e) => ApplyZoom(_zoomLevel * 0.8);
        private void ZoomFitBtn_Click(object sender, RoutedEventArgs e) => ApplyZoom(1.0);

        private void PreviewScroller_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                ApplyZoom(e.Delta > 0 ? _zoomLevel * 1.15 : _zoomLevel / 1.15);
            }
        }

        private void ApplyZoom(double newZoom)
        {
            _zoomLevel = Math.Max(0.1, Math.Min(8.0, newZoom));
            PreviewZoom.ScaleX = _zoomLevel;
            PreviewZoom.ScaleY = _zoomLevel;
            ZoomLabel.Text = $"{(int)Math.Round(_zoomLevel * 100)}%";
        }

        private void ScanMoreBtn_Click(object sender, RoutedEventArgs e)
        {
            // Read selected DPI from combo box (default 300)
            int dpi = 300;
            if (DpiComboBox.SelectedItem is ComboBoxItem dpiItem &&
                int.TryParse(dpiItem.Tag?.ToString(), out int parsedDpi))
                dpi = parsedDpi;

            // Read selected colour mode (4=Colour, 2=Greyscale, 1=B&W)
            int colourMode = 4;
            if (ColourModeComboBox.SelectedItem is ComboBoxItem modeItem &&
                int.TryParse(modeItem.Tag?.ToString(), out int parsedMode))
                colourMode = parsedMode;

            string? scannedFilePath = ScannerService.ScanDocument(dpi, colourMode);
            if (!string.IsNullOrEmpty(scannedFilePath))
            {
                AddPage(scannedFilePath);
            }
        }

        private void PagesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PagesListBox.SelectedItem is ScannedPage page)
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(page.ImagePath);
                    bitmap.EndInit();
                    PreviewImage.Source = bitmap;
                    CropRectangle.Visibility = Visibility.Collapsed;
                    CropBtn.IsEnabled = false;
                    RotateBtn.IsEnabled = true;
                    AutoCropBtn.IsEnabled = true;
                    TileCopiesBtn.IsEnabled = true;

                    // UI REFACTOR: Update status bar with image metadata
                    ImageDimensionsLabel.Text = $"Dimensions: {bitmap.PixelWidth} × {bitmap.PixelHeight} px";
                    FileInfo fi = new FileInfo(page.ImagePath);
                    if (fi.Exists)
                    {
                        FileSizeLabel.Text = $"File Size: {fi.Length / 1024} KB";
                    }
                    StatusLabel.Text = "Ready";
                }
                catch { }
            }
            else
            {
                PreviewImage.Source = null;
                RotateBtn.IsEnabled = false;
                AutoCropBtn.IsEnabled = false;
                TileCopiesBtn.IsEnabled = false;

                // UI REFACTOR: Clear status bar when no image is selected
                ImageDimensionsLabel.Text = "Dimensions: N/A";
                FileSizeLabel.Text = "File Size: N/A";
                StatusLabel.Text = "Ready";
            }
        }

        private void RemovePageBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ScannedPage page)
            {
                ScannedPages.Remove(page);
                for (int i = 0; i < ScannedPages.Count; i++)
                {
                    ScannedPages[i].PageName = $"Page {i + 1}";
                }
                PagesListBox.Items.Refresh();
            }
        }

        private void PreviewImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (PreviewImage.Source == null) return;
            _isDragging = true;
            _startPoint = e.GetPosition(CropCanvas);
            Canvas.SetLeft(CropRectangle, _startPoint.X);
            Canvas.SetTop(CropRectangle, _startPoint.Y);
            CropRectangle.Width = 0;
            CropRectangle.Height = 0;
            CropRectangle.Visibility = Visibility.Visible;
            CropBtn.IsEnabled = false;
            PreviewImage.CaptureMouse();
        }

        private void PreviewImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                System.Windows.Point pos = e.GetPosition(CropCanvas);
                double x = Math.Min(pos.X, _startPoint.X);
                double y = Math.Min(pos.Y, _startPoint.Y);
                double w = Math.Abs(pos.X - _startPoint.X);
                double h = Math.Abs(pos.Y - _startPoint.Y);

                // Constrain to image bounds
                x = Math.Max(0, x);
                y = Math.Max(0, y);
                w = Math.Max(0, Math.Min(w, PreviewImage.ActualWidth - x));
                h = Math.Max(0, Math.Min(h, PreviewImage.ActualHeight - y));

                Canvas.SetLeft(CropRectangle, x);
                Canvas.SetTop(CropRectangle, y);

                if (!double.IsNaN(w) && w >= 0) CropRectangle.Width = w;
                if (!double.IsNaN(h) && h >= 0) CropRectangle.Height = h;
            }
        }

        private void PreviewImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                PreviewImage.ReleaseMouseCapture();
                if (CropRectangle.Width > 10 && CropRectangle.Height > 10)
                {
                    CropBtn.IsEnabled = true;
                }
                else
                {
                    CropRectangle.Visibility = Visibility.Collapsed;
                    CropBtn.IsEnabled = false;
                }
            }
        }

        private void RotateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PagesListBox.SelectedItem is ScannedPage page && PreviewImage.Source is BitmapSource source)
            {
                TransformedBitmap rotated = new TransformedBitmap(source, new RotateTransform(90));

                string newPath = Path.Combine(Path.GetTempPath(), $"Rotated_{Guid.NewGuid():N}.jpg");
                using (FileStream fs = new FileStream(newPath, FileMode.Create))
                {
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rotated));
                    encoder.Save(fs);
                }

                page.ImagePath = newPath;

                PagesListBox.Items.Refresh();

                int idx = PagesListBox.SelectedIndex;
                PagesListBox.SelectedIndex = -1;
                PagesListBox.SelectedIndex = idx;
            }
        }

        private void CropBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PagesListBox.SelectedItem is ScannedPage page && PreviewImage.Source is BitmapSource source)
            {
                // UI REFACTOR: Show wait cursor for crop
                Mouse.OverrideCursor = Cursors.Wait;
                StatusLabel.Text = "Cropping...";

                try
                {
                    double x = Canvas.GetLeft(CropRectangle);
                    double y = Canvas.GetTop(CropRectangle);
                    double w = CropRectangle.Width;
                    double h = CropRectangle.Height;

                    // Scale to actual image pixels
                    double scaleX = source.PixelWidth / PreviewImage.ActualWidth;
                    double scaleY = source.PixelHeight / PreviewImage.ActualHeight;

                    int px = (int)(x * scaleX);
                    int py = (int)(y * scaleY);
                    int pw = (int)(w * scaleX);
                    int ph = (int)(h * scaleY);

                    if (pw > 0 && ph > 0)
                    {
                        CroppedBitmap cropped = new CroppedBitmap(source, new Int32Rect(px, py, pw, ph));

                        // Save cropped image
                        string newPath = Path.Combine(Path.GetTempPath(), $"Cropped_{Guid.NewGuid():N}.jpg");
                        using (FileStream fs = new FileStream(newPath, FileMode.Create))
                        {
                            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(cropped));
                            encoder.Save(fs);
                        }

                        page.ImagePath = newPath;

                        PagesListBox.Items.Refresh();

                        // Refresh view
                        int idx = PagesListBox.SelectedIndex;
                        PagesListBox.SelectedIndex = -1;
                        PagesListBox.SelectedIndex = idx;
                    }
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                    StatusLabel.Text = "Ready";
                }
            }
        }

        private void TileCopiesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PagesListBox.SelectedItem is not ScannedPage page) return;

            if (!int.TryParse(TileCopiesBox.Text, out int copies) || copies < 1 || copies > 200)
            {
                MessageBox.Show("Please enter a valid number of copies (1-200).", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // UI REFACTOR: Wait cursor for tiling
                Mouse.OverrideCursor = Cursors.Wait;
                StatusLabel.Text = "Generating Tile Sheet...";

                // Load the source image
                BitmapImage src = new BitmapImage();
                src.BeginInit();
                src.CacheOption = BitmapCacheOption.OnLoad;
                src.UriSource = new Uri(page.ImagePath);
                src.EndInit();

                int photoW = src.PixelWidth;
                int photoH = src.PixelHeight;

                // A4 at 300 DPI = 2480 x 3508 pixels
                int a4W = 2480;
                int a4H = 3508;
                int gap = 20; // pixels between photos

                // Calculate how many fit per row/column
                int cols = Math.Max(1, (a4W + gap) / (photoW + gap));
                int rows = (int)Math.Ceiling((double)copies / cols);

                // If photos are too big for A4, scale them down
                if (photoW > a4W || photoH > a4H)
                {
                    double scale = Math.Min((double)a4W / photoW, (double)a4H / photoH) * 0.9;
                    photoW = (int)(photoW * scale);
                    photoH = (int)(photoH * scale);
                    cols = Math.Max(1, (a4W + gap) / (photoW + gap));
                    rows = (int)Math.Ceiling((double)copies / cols);
                }

                // Create A4 canvas
                WriteableBitmap canvas = new WriteableBitmap(a4W, a4H, 96, 96, PixelFormats.Bgra32, null);

                // Fill white background
                byte[] white = new byte[a4W * a4H * 4];
                for (int i = 0; i < white.Length; i += 4)
                {
                    white[i] = 255;     // B
                    white[i + 1] = 255; // G
                    white[i + 2] = 255; // R
                    white[i + 3] = 255; // A
                }
                canvas.WritePixels(new Int32Rect(0, 0, a4W, a4H), white, a4W * 4, 0);

                // Scale source image if needed
                BitmapSource drawSrc = src;
                if (photoW != src.PixelWidth || photoH != src.PixelHeight)
                {
                    var scaled = new TransformedBitmap();
                    scaled.BeginInit();
                    scaled.Source = src;
                    scaled.Transform = new ScaleTransform((double)photoW / src.PixelWidth, (double)photoH / src.PixelHeight);
                    scaled.EndInit();
                    drawSrc = scaled;
                }

                // Convert source to Bgra32
                FormatConvertedBitmap fcb = new FormatConvertedBitmap(drawSrc, PixelFormats.Bgra32, null, 0);
                int srcStride = fcb.PixelWidth * 4;
                byte[] srcPixels = new byte[fcb.PixelHeight * srcStride];
                fcb.CopyPixels(srcPixels, srcStride, 0);

                int placed = 0;
                int startX = (a4W - (Math.Min(copies, cols) * (photoW + gap) - gap)) / 2;
                int startY = (a4H - (rows * (photoH + gap) - gap)) / 2;
                startY = Math.Max(0, startY);

                for (int row = 0; row < rows && placed < copies; row++)
                {
                    int rowCopies = Math.Min(copies - placed, cols);
                    int rowStartX = (a4W - (rowCopies * (photoW + gap) - gap)) / 2;

                    for (int col = 0; col < rowCopies; col++)
                    {
                        int destX = rowStartX + col * (photoW + gap);
                        int destY = startY + row * (photoH + gap);

                        // Clip to canvas bounds
                        int drawW = Math.Min(photoW, a4W - destX);
                        int drawH = Math.Min(photoH, a4H - destY);

                        if (drawW > 0 && drawH > 0 && destX >= 0 && destY >= 0)
                        {
                            canvas.WritePixels(
                                new Int32Rect(destX, destY, drawW, drawH),
                                srcPixels,
                                srcStride,
                                0);
                        }
                        placed++;
                    }
                }

                // Save the tiled sheet as a new temp file
                string tileFile = Path.Combine(Path.GetTempPath(), $"TileSheet_{Guid.NewGuid():N}.jpg");
                using (FileStream fs = new FileStream(tileFile, FileMode.Create))
                {
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.QualityLevel = 95;
                    encoder.Frames.Add(BitmapFrame.Create(canvas));
                    encoder.Save(fs);
                }

                // Add as new page but keep the original page selected so user can generate more tile sheets
                int originalIdx = PagesListBox.SelectedIndex;
                AddPage(tileFile);
                PagesListBox.SelectedIndex = originalIdx;

                MessageBox.Show($"Generated a tile sheet with {copies} copies! Added as Page {ScannedPages.Count}. You can generate another with a different number, or save/print when ready.",
                    "Tile Sheet Ready", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate tile sheet: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                StatusLabel.Text = "Ready";
            }
        }

        private void AutoCropBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PagesListBox.SelectedItem is not ScannedPage page) return;

            try
            {
                // UI REFACTOR: Wait cursor for auto crop
                Mouse.OverrideCursor = Cursors.Wait;
                StatusLabel.Text = "Auto Cropping...";

                // Always load fresh from file for accurate pixel data
                BitmapImage srcImg = new BitmapImage();
                srcImg.BeginInit();
                srcImg.CacheOption = BitmapCacheOption.OnLoad;
                srcImg.UriSource = new Uri(page.ImagePath);
                srcImg.EndInit();

                FormatConvertedBitmap fcb = new FormatConvertedBitmap(srcImg, PixelFormats.Bgra32, null, 0);
                int width = fcb.PixelWidth;
                int height = fcb.PixelHeight;
                int stride = width * 4;
                byte[] pixels = new byte[height * stride];
                fcb.CopyPixels(pixels, stride, 0);

                // ── Step 1: Sample background from all 4 edges (10px strip) → use MEDIAN ──
                int strip = Math.Max(5, Math.Min(15, Math.Min(width, height) / 30));
                var rList = new List<byte>(strip * (width + height) * 4);
                var gList = new List<byte>(rList.Capacity);
                var bList = new List<byte>(rList.Capacity);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        bool onEdge = x < strip || x >= width - strip || y < strip || y >= height - strip;
                        if (!onEdge) continue;
                        int i = y * stride + x * 4;
                        bList.Add(pixels[i]);
                        gList.Add(pixels[i + 1]);
                        rList.Add(pixels[i + 2]);
                    }
                }
                rList.Sort(); gList.Sort(); bList.Sort();
                double medBgR = rList[rList.Count / 2];
                double medBgG = gList[gList.Count / 2];
                double medBgB = bList[bList.Count / 2];

                // ── Step 2: Row/Column voting — a row/column is "content" if ≥2% pixels differ ──
                const double pixThreshold = 55.0;   // per-pixel color diff to call it content
                const double rowVoteRatio = 0.015;  // 1.5% of row/col pixels must be content

                int top = 0, bottom = height - 1, left = 0, right = width - 1;

                // Find TOP edge
                for (int y = 0; y < height; y++)
                {
                    int cnt = 0;
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * stride + x * 4;
                        if (Math.Abs(pixels[i + 2] - medBgR) + Math.Abs(pixels[i + 1] - medBgG) + Math.Abs(pixels[i] - medBgB) > pixThreshold)
                            cnt++;
                    }
                    if (cnt >= width * rowVoteRatio) { top = y; break; }
                }

                // Find BOTTOM edge
                for (int y = height - 1; y >= 0; y--)
                {
                    int cnt = 0;
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * stride + x * 4;
                        if (Math.Abs(pixels[i + 2] - medBgR) + Math.Abs(pixels[i + 1] - medBgG) + Math.Abs(pixels[i] - medBgB) > pixThreshold)
                            cnt++;
                    }
                    if (cnt >= width * rowVoteRatio) { bottom = y; break; }
                }

                // Find LEFT edge
                for (int x = 0; x < width; x++)
                {
                    int cnt = 0;
                    for (int y = 0; y < height; y++)
                    {
                        int i = y * stride + x * 4;
                        if (Math.Abs(pixels[i + 2] - medBgR) + Math.Abs(pixels[i + 1] - medBgG) + Math.Abs(pixels[i] - medBgB) > pixThreshold)
                            cnt++;
                    }
                    if (cnt >= height * rowVoteRatio) { left = x; break; }
                }

                // Find RIGHT edge
                for (int x = width - 1; x >= 0; x--)
                {
                    int cnt = 0;
                    for (int y = 0; y < height; y++)
                    {
                        int i = y * stride + x * 4;
                        if (Math.Abs(pixels[i + 2] - medBgR) + Math.Abs(pixels[i + 1] - medBgG) + Math.Abs(pixels[i] - medBgB) > pixThreshold)
                            cnt++;
                    }
                    if (cnt >= height * rowVoteRatio) { right = x; break; }
                }

                // ── Step 3: Add padding and crop ──
                int padding = 18;
                top = Math.Max(0, top - padding);
                bottom = Math.Min(height - 1, bottom + padding);
                left = Math.Max(0, left - padding);
                right = Math.Min(width - 1, right + padding);

                int pw = right - left + 1;
                int ph = bottom - top + 1;

                // Crop only if we remove at least 5% from at least one dimension
                bool worthCropping = pw < width * 0.95 || ph < height * 0.95;

                if (pw > 10 && ph > 10 && worthCropping)
                {
                    CroppedBitmap cropped = new CroppedBitmap(srcImg, new Int32Rect(left, top, pw, ph));

                    string newPath = Path.Combine(Path.GetTempPath(), $"AutoCropped_{Guid.NewGuid():N}.jpg");
                    using (FileStream fs = new FileStream(newPath, FileMode.Create))
                    {
                        JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                        encoder.QualityLevel = 96;
                        encoder.Frames.Add(BitmapFrame.Create(cropped));
                        encoder.Save(fs);
                    }

                    page.ImagePath = newPath;
                    PagesListBox.Items.Refresh();
                    int idx = PagesListBox.SelectedIndex;
                    PagesListBox.SelectedIndex = -1;
                    PagesListBox.SelectedIndex = idx;
                }
                else
                {
                    MessageBox.Show("The image is already tightly cropped — no white border was found to remove.", "Auto Crop", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Auto Crop failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                StatusLabel.Text = "Ready";
            }
        }

        private void SavePdfBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ScannedPages.Count == 0) return;

            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = "ScannedDocument.pdf"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using (PdfDocument document = new PdfDocument())
                    {
                        foreach (var page in ScannedPages)
                        {
                            using (XImage img = XImage.FromFile(page.ImagePath))
                            {
                                PdfPage pdfPage = document.AddPage();
                                pdfPage.Width = XUnit.FromPoint(img.PointWidth);
                                pdfPage.Height = XUnit.FromPoint(img.PointHeight);

                                using (XGraphics gfx = XGraphics.FromPdfPage(pdfPage))
                                {
                                    gfx.DrawImage(img, 0, 0, pdfPage.Width, pdfPage.Height);
                                }
                            }
                        }
                        document.Save(sfd.FileName);
                    }
                    MessageBox.Show("PDF saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveImgBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ScannedPages.Count == 0) return;

            System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog();
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    for (int i = 0; i < ScannedPages.Count; i++)
                    {
                        string ext = Path.GetExtension(ScannedPages[i].ImagePath);
                        string dest = Path.Combine(fbd.SelectedPath, $"ScannedPage_{i + 1}{ext}");
                        File.Copy(ScannedPages[i].ImagePath, dest, true);
                    }
                    MessageBox.Show("Images saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving images: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddToPrintBtn_Click(object sender, RoutedEventArgs e)
        {
            foreach (var page in ScannedPages)
            {
                FinalFiles.Add(page.ImagePath);
            }
            try
            {
                DialogResult = true;
            }
            catch { }
        }
    }
}
