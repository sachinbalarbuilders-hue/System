using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PdfPrintUtility.Services
{
    public class ScannerService
    {
        // WIA Property IDs
        private const int WIA_IPS_PAGE_SIZE        = 3097;
        private const int WIA_IPS_PAGE_WIDTH       = 3098;
        private const int WIA_IPS_PAGE_HEIGHT      = 3099;
        private const int WIA_IPS_CUR_INTENT       = 4103; // colour mode: 1=B&W, 2=Greyscale, 4=Colour
        private const int WIA_IPS_XRES             = 6147; // horizontal DPI
        private const int WIA_IPS_YRES             = 6148; // vertical DPI
        private const int WIA_PAGE_A4              = 0;    // A4 = 0 in WIA

        public static string? ScanDocument(int dpi = 300, int colourMode = 4)
        {
            try
            {
                Type? dialogType = Type.GetTypeFromProgID("WIA.CommonDialog");
                if (dialogType == null)
                {
                    System.Windows.MessageBox.Show("Windows Image Acquisition (WIA) is not available on this system.", "Scanning Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return null;
                }

                dynamic dialog = Activator.CreateInstance(dialogType)!;

                // Step 1: Let user pick a scanner device
                dynamic device = dialog.ShowSelectDevice(
                    1,      // ScannerDeviceType
                    false,  // AlwaysSelectDevice (auto-select if only one)
                    false   // CancelError
                );

                if (device == null) return null;

                // Step 2: Get the scan item (first item on the device)
                dynamic item = device.Items[1];

                // Step 3: Apply user-chosen scan settings (DPI, colour mode, page size).
                // Each is wrapped individually — unsupported properties are silently skipped.
                try { SetWiaProperty(item.Properties, WIA_IPS_XRES, dpi); }        catch { }
                try { SetWiaProperty(item.Properties, WIA_IPS_YRES, dpi); }        catch { }
                try { SetWiaProperty(item.Properties, WIA_IPS_CUR_INTENT, colourMode); } catch { }
                try { SetWiaProperty(item.Properties, WIA_IPS_PAGE_SIZE, 100); }   catch { } // 100 = WIA_PAGE_AUTO

                // Step 4: Show the scan dialog for this specific item and transfer
                dynamic image = dialog.ShowTransfer(
                    item,
                    "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}", // JPEG
                    false  // CancelError
                );

                if (image != null)
                {
                    string tempFile = Path.Combine(Path.GetTempPath(), $"ScannedDoc_{Guid.NewGuid():N}.jpg");
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                    image.SaveFile(tempFile);
                    Logger.LogPrint("Scanned Image", "Local Scanner", "Success", "");
                    return tempFile;
                }
            }
            catch (COMException ex)
            {
                // 0x80210015 = WIA_E_USER_CANCEL — don't show error for user-cancelled scan
                if ((uint)ex.ErrorCode != 0x80210015)
                {
                    // If the new approach fails, fall back to the simple ShowAcquireImage
                    return FallbackScan();
                }
            }
            catch (Exception)
            {
                return FallbackScan();
            }

            return null;
        }

        /// <summary>
        /// Automatically crops grey/white scanner dead-zone borders from a scanned image.
        /// Works by detecting rows/columns that are uniformly close to the background colour.
        /// Returns the original path if no significant crop is found.
        /// </summary>
        private static string AutoCropGrey(string filePath)
        {
            try
            {
                // Load image via System.Drawing so we avoid WPF threading restrictions in a service
                using var bmp = System.Drawing.Image.FromFile(filePath) as System.Drawing.Bitmap;
                if (bmp == null) return filePath;

                int w = bmp.Width;
                int h = bmp.Height;

                // Sample median background from all four edges (10px strip)
                int strip = Math.Max(5, Math.Min(15, Math.Min(w, h) / 30));
                var rList = new System.Collections.Generic.List<int>();
                var gList = new System.Collections.Generic.List<int>();
                var bList = new System.Collections.Generic.List<int>();

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        bool onEdge = x < strip || x >= w - strip || y < strip || y >= h - strip;
                        if (!onEdge) continue;
                        var px = bmp.GetPixel(x, y);
                        rList.Add(px.R); gList.Add(px.G); bList.Add(px.B);
                    }
                }
                rList.Sort(); gList.Sort(); bList.Sort();
                double medR = rList[rList.Count / 2];
                double medG = gList[gList.Count / 2];
                double medB = bList[bList.Count / 2];

                // Pixels within this distance from background colour are considered background
                const double threshold = 60.0;
                const double voteRatio = 0.015; // 1.5% of row/col must be content

                int top = 0, bottom = h - 1, left = 0, right = w - 1;

                for (int y = 0; y < h; y++)
                {
                    int cnt = 0;
                    for (int x = 0; x < w; x++)
                    {
                        var px = bmp.GetPixel(x, y);
                        if (Math.Abs(px.R - medR) + Math.Abs(px.G - medG) + Math.Abs(px.B - medB) > threshold) cnt++;
                    }
                    if (cnt >= w * voteRatio) { top = y; break; }
                }
                for (int y = h - 1; y >= 0; y--)
                {
                    int cnt = 0;
                    for (int x = 0; x < w; x++)
                    {
                        var px = bmp.GetPixel(x, y);
                        if (Math.Abs(px.R - medR) + Math.Abs(px.G - medG) + Math.Abs(px.B - medB) > threshold) cnt++;
                    }
                    if (cnt >= w * voteRatio) { bottom = y; break; }
                }
                for (int x = 0; x < w; x++)
                {
                    int cnt = 0;
                    for (int y = 0; y < h; y++)
                    {
                        var px = bmp.GetPixel(x, y);
                        if (Math.Abs(px.R - medR) + Math.Abs(px.G - medG) + Math.Abs(px.B - medB) > threshold) cnt++;
                    }
                    if (cnt >= h * voteRatio) { left = x; break; }
                }
                for (int x = w - 1; x >= 0; x--)
                {
                    int cnt = 0;
                    for (int y = 0; y < h; y++)
                    {
                        var px = bmp.GetPixel(x, y);
                        if (Math.Abs(px.R - medR) + Math.Abs(px.G - medG) + Math.Abs(px.B - medB) > threshold) cnt++;
                    }
                    if (cnt >= h * voteRatio) { right = x; break; }
                }

                int padding = 20;
                top    = Math.Max(0, top - padding);
                bottom = Math.Min(h - 1, bottom + padding);
                left   = Math.Max(0, left - padding);
                right  = Math.Min(w - 1, right + padding);

                int cw = right - left + 1;
                int ch = bottom - top + 1;

                // Only crop if we'd remove at least 5% from any side
                bool worthCropping = cw < w * 0.95 || ch < h * 0.95;
                if (!worthCropping || cw < 100 || ch < 100) return filePath;

                System.Drawing.Rectangle cropRect = new System.Drawing.Rectangle(left, top, cw, ch);
                using var cropped = bmp.Clone(cropRect, bmp.PixelFormat);
                string outPath = Path.Combine(Path.GetTempPath(), $"ScannedDoc_Cropped_{Guid.NewGuid():N}.jpg");
                cropped.Save(outPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                return outPath;
            }
            catch
            {
                return filePath; // on any failure return the original
            }
        }

        private static string? FallbackScan()
        {
            try
            {
                Type? dialogType = Type.GetTypeFromProgID("WIA.CommonDialog");
                if (dialogType == null) return null;

                dynamic dialog = Activator.CreateInstance(dialogType)!;
                dynamic image = dialog.ShowAcquireImage(
                    1,
                    1,
                    1,
                    "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}",
                    false,
                    true,
                    false
                );

                if (image != null)
                {
                    string tempFile = Path.Combine(Path.GetTempPath(), $"ScannedDoc_{Guid.NewGuid():N}.jpg");
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                    image.SaveFile(tempFile);
                    Logger.LogPrint("Scanned Image", "Local Scanner", "Success", "");
                    return tempFile;
                }
            }
            catch (COMException ex)
            {
                if ((uint)ex.ErrorCode != 0x80210015)
                {
                    Logger.LogPrint("Scanned Image", "Local Scanner", "Failed", ex.Message);
                    System.Windows.MessageBox.Show($"Scanner error: {ex.Message}", "Scanning Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.LogPrint("Scanned Image", "Local Scanner", "Failed", ex.Message);
                System.Windows.MessageBox.Show($"Could not scan document: {ex.Message}", "Scanning Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }

            return null;
        }

        /// <summary>
        /// Sets a WIA property value by property ID using dynamic COM.
        /// </summary>
        private static void SetWiaProperty(dynamic properties, int propId, object value)
        {
            foreach (dynamic prop in properties)
            {
                if (prop.PropertyID == propId)
                {
                    prop.Value = value;
                    return;
                }
            }
        }
    }
}
