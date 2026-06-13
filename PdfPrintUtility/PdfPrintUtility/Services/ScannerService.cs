using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PdfPrintUtility.Services
{
    public class ScannerService
    {
        // WIA Property IDs
        private const int WIA_IPS_PAGE_SIZE   = 3097;
        private const int WIA_IPS_PAGE_WIDTH  = 3098;
        private const int WIA_IPS_PAGE_HEIGHT = 3099;
        private const int WIA_PAGE_A4         = 0;    // A4 = 0 in WIA

        public static string? ScanDocument()
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

                // Step 3: Try to set page size to AUTO (WIA_PAGE_AUTO = 100)
                // This lets the scanner auto-detect the document size.
                // If the scanner doesn't support AUTO, we skip it gracefully.
                try
                {
                    SetWiaProperty(item.Properties, WIA_IPS_PAGE_SIZE, 100); // 100 = WIA_PAGE_AUTO
                }
                catch { /* Not all scanners support auto page size; skip */ }

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
        /// Fallback to original single-call approach if the multi-step scan fails.
        /// </summary>
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
                    return tempFile;
                }
            }
            catch (COMException ex)
            {
                if ((uint)ex.ErrorCode != 0x80210015)
                {
                    System.Windows.MessageBox.Show($"Scanner error: {ex.Message}", "Scanning Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
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
