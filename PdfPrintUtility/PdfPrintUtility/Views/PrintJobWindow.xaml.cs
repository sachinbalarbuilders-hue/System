using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AutoUpdaterDotNET;
using PdfPrintUtility.Models;
using PdfPrintUtility.Services;
using PdfiumViewer;

namespace PdfPrintUtility.Views;

public partial class PrintJobWindow : Window
{
	private ObservableCollection<PrintFileItem> _observableFiles;

	private SettingsData _settingsData;

	private PdfDocument? _previewDoc;

	private int _previewPage;

	private int _previewPageCount;

	private string? _previewFilePath;

	private const float PreviewBaseDpi = 96f;

	private DispatcherTimer? _renderDebounce;

	private readonly SemaphoreSlim _renderLock = new SemaphoreSlim(1, 1);

	private int _renderGeneration;

	

	

	

	

	

	

	

	

	

	

	

	

	

	

	

	

	

	

	

	

	

	

	

	

	

	

	

	

	

	[DllImport("gdi32.dll")]
	private static extern bool DeleteObject(nint hObject);

	public PrintJobWindow(List<string> files)
	{
		InitializeComponent();
		_observableFiles = new ObservableCollection<PrintFileItem>(files.Select((string f) => new PrintFileItem
		{
			FilePath = f
		}));
		FilesListBox.ItemsSource = _observableFiles;
		base.Loaded += PrintJobWindow_Loaded;
		LoadPrinters();
		LoadData();
		if (_observableFiles.Count > 0)
		{
			FilesListBox.SelectedIndex = 0;
		}
	}

	private void PrintJobWindow_Loaded(object sender, RoutedEventArgs e)
	{
		try
		{
			AutoUpdater.ReportErrors = false;
			AutoUpdater.Start("Z:\\Software\\PdfPrintUtility\\update.xml");
		}
		catch
		{
		}
	}

	private void LoadPrinters()
	{
		foreach (string installedPrinter in PrinterSettings.InstalledPrinters)
		{
			PrinterComboBox.Items.Add(installedPrinter);
		}
	}

	private void LoadPaperTrays(string printerName)
	{
		PaperTrayComboBox.Items.Clear();
		PaperTrayComboBox.Items.Add("Auto (Printer Default)");
		try
		{
			foreach (PaperSource paperSource in new PrinterSettings
			{
				PrinterName = printerName
			}.PaperSources)
			{
				if (!paperSource.SourceName.Equals("Auto Select", StringComparison.OrdinalIgnoreCase))
				{
					PaperTrayComboBox.Items.Add(paperSource.SourceName);
				}
			}
		}
		catch
		{
		}
		PaperTrayComboBox.SelectedIndex = 0;
	}

	private void PrinterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		string text = PrinterComboBox.SelectedItem?.ToString() ?? "";
		if (!string.IsNullOrEmpty(text))
		{
			LoadPaperTrays(text);
		}
	}

	private void LoadData()
	{
		_settingsData = SettingsManager.LoadData();
		ProfileComboBox.Items.Clear();
		foreach (PrintSettings profile in _settingsData.Profiles)
		{
			ProfileComboBox.Items.Add(profile.ProfileName);
		}
		ProfileComboBox.SelectedItem = _settingsData.LastUsedProfile;
		if (ProfileComboBox.SelectedIndex == -1 && ProfileComboBox.Items.Count > 0)
		{
			ProfileComboBox.SelectedIndex = 0;
		}
		LoadSettingsIntoUI(GetSelectedProfile());
	}

	private PrintSettings GetSelectedProfile()
	{
		string profileName = ProfileComboBox.Text;
		return _settingsData.Profiles.FirstOrDefault((PrintSettings p) => p.ProfileName == profileName) ?? new PrintSettings
		{
			ProfileName = profileName
		};
	}

	private void LoadSettingsIntoUI(PrintSettings settings)
	{
		if (PrinterComboBox.Items.Contains(settings.DefaultPrinter))
		{
			PrinterComboBox.SelectedItem = settings.DefaultPrinter;
		}
		else if (PrinterComboBox.Items.Count > 0)
		{
			PrinterComboBox.SelectedIndex = 0;
		}
		string printerName = PrinterComboBox.SelectedItem?.ToString() ?? "";
		LoadPaperTrays(printerName);
		if (!string.IsNullOrEmpty(settings.PaperTray) && PaperTrayComboBox.Items.Contains(settings.PaperTray))
		{
			PaperTrayComboBox.SelectedItem = settings.PaperTray;
		}
		else
		{
			PaperTrayComboBox.SelectedIndex = 0;
		}
		CopiesTextBox.Text = "1";
		DuplexCheckBox.IsChecked = settings.Duplex;
		CollateCheckBox.IsChecked = settings.Collate;
		ColorComboBox.Text = settings.ColorMode;
		OrientationComboBox.Text = settings.Orientation;
		PaperSizeComboBox.Text = settings.PaperSize;
		WatermarkTextBox.Text = string.Empty;
		foreach (ComboBoxItem item in (IEnumerable)FitModeComboBox.Items)
		{
			if (item.Tag?.ToString() == settings.FitMode)
			{
				item.IsSelected = true;
				break;
			}
		}
	}

	private PrintSettings GetSettingsFromUI()
	{
		short.TryParse(CopiesTextBox.Text, out var result);
		if (result < 1)
		{
			result = 1;
		}
		string fitMode = "FitToPage";
		if (FitModeComboBox.SelectedItem is ComboBoxItem { Tag: var tag })
		{
			fitMode = tag?.ToString() ?? "FitToPage";
		}
		string text = PaperTrayComboBox.SelectedItem?.ToString() ?? "";
		if (text == "Auto (Printer Default)")
		{
			text = string.Empty;
		}
		return new PrintSettings
		{
			ProfileName = ProfileComboBox.Text,
			DefaultPrinter = (PrinterComboBox.SelectedItem?.ToString() ?? ""),
			Copies = result,
			Duplex = (DuplexCheckBox.IsChecked == true),
			Collate = (CollateCheckBox.IsChecked ?? true),
			ColorMode = ColorComboBox.Text,
			Orientation = OrientationComboBox.Text,
			PaperSize = PaperSizeComboBox.Text,
			WatermarkText = WatermarkTextBox.Text,
			FitMode = fitMode,
			PaperTray = text
		};
	}

	private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (e.AddedItems.Count > 0)
		{
			string profileName = e.AddedItems[0].ToString();
			PrintSettings printSettings = _settingsData.Profiles.FirstOrDefault((PrintSettings p) => p.ProfileName == profileName);
			if (printSettings != null)
			{
				LoadSettingsIntoUI(printSettings);
			}
		}
	}

	private void SaveProfileBtn_Click(object sender, RoutedEventArgs e)
	{
		string profileName = ProfileComboBox.Text;
		if (string.IsNullOrWhiteSpace(profileName))
		{
			MessageBox.Show("Please enter a profile name.", "Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		PrintSettings settingsFromUI = GetSettingsFromUI();
		PrintSettings printSettings = _settingsData.Profiles.FirstOrDefault((PrintSettings p) => p.ProfileName == profileName);
		if (printSettings != null)
		{
			_settingsData.Profiles.Remove(printSettings);
		}
		else
		{
			ProfileComboBox.Items.Add(profileName);
		}
		_settingsData.Profiles.Add(settingsFromUI);
		_settingsData.LastUsedProfile = profileName;
		SettingsManager.SaveData(_settingsData);
		MessageBox.Show("Profile saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	private void DeleteProfileBtn_Click(object sender, RoutedEventArgs e)
	{
		string profileName = ProfileComboBox.Text;
		if (profileName == "Default")
		{
			MessageBox.Show("Cannot delete the Default profile.", "Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		PrintSettings printSettings = _settingsData.Profiles.FirstOrDefault((PrintSettings p) => p.ProfileName == profileName);
		if (printSettings != null)
		{
			_settingsData.Profiles.Remove(printSettings);
			ProfileComboBox.Items.Remove(profileName);
			ProfileComboBox.SelectedIndex = 0;
			SettingsManager.SaveData(_settingsData);
		}
	}

	private void Window_DragEnter(object sender, DragEventArgs e)
	{
		if (e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			e.Effects = DragDropEffects.Copy;
		}
		else
		{
			e.Effects = DragDropEffects.None;
		}
	}

	private void Window_Drop(object sender, DragEventArgs e)
	{
		if (!e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			return;
		}
		string[] array = (string[])e.Data.GetData(DataFormats.FileDrop);
		string[] array2 = new string[10] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".gif" };
		string[] array3 = array;
		foreach (string text in array3)
		{
			string ext = Path.GetExtension(text).ToLowerInvariant();
			if (Array.Exists(array2, (string x) => x == ext) && !_observableFiles.Any((PrintFileItem f) => f.FilePath.Equals(text, StringComparison.OrdinalIgnoreCase)))
			{
				_observableFiles.Add(new PrintFileItem
				{
					FilePath = text
				});
			}
		}
		if (FilesListBox.SelectedIndex == -1 && _observableFiles.Count > 0)
		{
			FilesListBox.SelectedIndex = 0;
		}
	}

	private void RemoveFile_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: PrintFileItem tag })
		{
			_observableFiles.Remove(tag);
			if (tag.FilePath == _previewFilePath)
			{
				ClearInlinePreview();
			}
		}
	}

	private void FilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (FilesListBox.SelectedItem is PrintFileItem printFileItem)
		{
			LoadInlinePreview(printFileItem.FilePath);
		}
	}

	private void PreviewSettings_Changed(object sender, SelectionChangedEventArgs e)
	{
		if (_previewFilePath != null)
		{
			SchedulePreviewRefresh();
		}
	}

	private void SchedulePreviewRefresh()
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		DispatcherTimer renderDebounce = _renderDebounce;
		if (renderDebounce != null)
		{
			renderDebounce.Stop();
		}
		_renderDebounce = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(150L, 0L)
		};
		_renderDebounce.Tick += delegate
		{
			_renderDebounce.Stop();
			if (_previewFilePath != null)
			{
				string ext = Path.GetExtension(_previewFilePath).ToLowerInvariant();
				if (ext == ".pdf")
				{
					RenderInlinePage(_previewPage);
				}
				else if (Array.Exists(PrintManager.ImageExtensions, (string x) => x == ext))
				{
					RenderImagePreview(_previewFilePath);
				}
			}
		};
		_renderDebounce.Start();
	}

	private void WatermarkTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_previewFilePath != null)
		{
			SchedulePreviewRefresh();
		}
	}

	private void PageRangeTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_previewDoc != null)
		{
			List<int> validPreviewPages = GetValidPreviewPages();
			if (validPreviewPages.Count > 0 && !validPreviewPages.Contains(_previewPage))
			{
				RenderInlinePage(validPreviewPages[0]);
			}
			else
			{
				RenderInlinePage(_previewPage);
			}
		}
	}

	private void LoadInlinePreview(string filePath)
	{
		if (filePath == _previewFilePath)
		{
			return;
		}
		_previewDoc?.Dispose();
		_previewDoc = null;
		_previewFilePath = null;
		string ext = Path.GetExtension(filePath).ToLowerInvariant();
		if (ext == ".pdf")
		{
			try
			{
				_previewDoc = PdfDocument.Load(filePath);
				_previewPageCount = _previewDoc.PageCount;
				_previewFilePath = filePath;
				_previewPage = 0;
				InlineFileLabel.Text = filePath;
				PopOutBtn.IsEnabled = true;
				List<int> validPreviewPages = GetValidPreviewPages();
				RenderInlinePage((validPreviewPages.Count > 0) ? validPreviewPages[0] : 0);
				return;
			}
			catch
			{
				ClearInlinePreview();
				InlinePageLabel.Text = "Could not load PDF preview";
				return;
			}
		}
		if (Array.Exists(PrintManager.ImageExtensions, (string x) => x == ext))
		{
			try
			{
				_previewFilePath = filePath;
				_previewPageCount = 1;
				_previewPage = 0;
				InlineFileLabel.Text = filePath;
				InlinePageLabel.Text = "Image — 1 page";
				InlinePrevBtn.IsEnabled = false;
				InlineNextBtn.IsEnabled = false;
				PopOutBtn.IsEnabled = false;
				RenderImagePreview(filePath);
				return;
			}
			catch
			{
				ClearInlinePreview();
				InlinePageLabel.Text = "Could not load image preview";
				return;
			}
		}
		if (Array.Exists(PrintManager.WordExtensions, (string x) => x == ext))
		{
			_previewFilePath = filePath;
			_previewPageCount = 1;
			_previewPage = 0;
			InlineFileLabel.Text = filePath;
			InlinePageLabel.Text = "Word Document — preview not available";
			InlinePrevBtn.IsEnabled = false;
			InlineNextBtn.IsEnabled = false;
			PopOutBtn.IsEnabled = false;
			InlinePreviewImage.Source = null;
		}
	}

	private SizeF GetPaperSizePoints(string paperName, string orientation, float nativeW, float nativeH)
	{
		if (paperName == "Auto (Match Document)")
		{
			paperName = ((Math.Max(nativeW, nativeH) > 850f) ? "A3" : "A4");
		}
		SizeF sizeF = paperName switch
		{
			"A3" => new SizeF(842f, 1191f), 
			"A4" => new SizeF(595f, 842f), 
			"Letter" => new SizeF(612f, 792f), 
			"Legal" => new SizeF(612f, 1008f), 
			_ => new SizeF(595f, 842f), 
		};
		bool flag = true;
		if (orientation == "Landscape")
		{
			flag = false;
		}
		else if (orientation == "Auto")
		{
			flag = nativeH >= nativeW;
		}
		if (flag)
		{
			return new SizeF(Math.Min(sizeF.Width, sizeF.Height), Math.Max(sizeF.Width, sizeF.Height));
		}
		return new SizeF(Math.Max(sizeF.Width, sizeF.Height), Math.Min(sizeF.Width, sizeF.Height));
	}

	private async void RenderImagePreview(string filePath)
	{
		int gen = ++_renderGeneration;
		string orientation = OrientationComboBox?.Text ?? "Auto";
		bool grayscale = ColorComboBox?.Text == "Grayscale";
		string paperSizeName = PaperSizeComboBox?.Text ?? "A4";
		string fitMode = (FitModeComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "FitToPage";
		string watermark = WatermarkTextBox?.Text ?? "";
		double available = Math.Max(80.0, InlineScrollViewer.ActualWidth - 40.0);
		BitmapSource bitmapSource = null;
		await _renderLock.WaitAsync();
		try
		{
			bitmapSource = await Task.Run(delegate
			{
				//IL_044f: Unknown result type (might be due to invalid IL or missing references)
				if (gen != _renderGeneration)
				{
					return (BitmapSource)null;
				}
				using Bitmap bitmap = new Bitmap(filePath);
				bool flag = bitmap.Height >= bitmap.Width;
				float num = (float)bitmap.Width * 72f / bitmap.HorizontalResolution;
				float num2 = (float)bitmap.Height * 72f / bitmap.VerticalResolution;
				SizeF paperPts = GetPaperSizePoints(paperSizeName, orientation, num, num2);
				float val = (float)(96.0 * (available / ((double)(paperPts.Width * 96f) / 72.0)));
				val = Math.Max(10f, Math.Min(val, 250f));
				int num3 = Math.Max(1, (int)((double)(paperPts.Width * val) / 72.0));
				int num4 = Math.Max(1, (int)((double)(paperPts.Height * val) / 72.0));
				double rotation = 0.0;
				if (orientation == "Landscape" && flag)
				{
					rotation = 90.0;
				}
				if (orientation == "Portrait" && !flag)
				{
					rotation = -90.0;
				}
				((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
				{
					if (gen == _renderGeneration)
					{
						string value = ((paperPts.Width > paperPts.Height) ? "landscape" : "portrait");
						string value2 = ((rotation == 90.0) ? " (90° CW)" : ((rotation == -90.0) ? " (270° CCW)" : ""));
						PaperInfoLabel.Text = $"Paper: '{paperSizeName} ({(double)paperPts.Width / 72.0:F1} x {(double)paperPts.Height / 72.0:F1} in)', {value}{value2}";
					}
				});
				Bitmap bitmap2 = new Bitmap(num3, num4);
				using (Graphics graphics = Graphics.FromImage(bitmap2))
				{
					graphics.Clear(Color.White);
					float num5 = 12f * val / 72f;
					using (Pen pen = new Pen(Color.LightGray, 1f))
					{
						pen.DashStyle = DashStyle.Dash;
						graphics.DrawRectangle(pen, num5, num5, (float)num3 - 2f * num5, (float)num4 - 2f * num5);
					}
					if (rotation != 0.0)
					{
						float num6 = num;
						num = num2;
						num2 = num6;
					}
					float num7 = 1f;
					if (fitMode == "FitToPage")
					{
						num7 = Math.Min(paperPts.Width / num, paperPts.Height / num2);
					}
					else if (fitMode == "ShrinkToFit")
					{
						num7 = Math.Min(1f, Math.Min(paperPts.Width / num, paperPts.Height / num2));
					}
					float num8 = num * num7;
					float num9 = num2 * num7;
					float num10 = (paperPts.Width - num8) / 2f;
					float num11 = (paperPts.Height - num9) / 2f;
					int x = (int)(num10 * val / 72f);
					int y = (int)(num11 * val / 72f);
					int width = (int)(num8 * val / 72f);
					int height = (int)(num9 * val / 72f);
					Bitmap bitmap3 = (grayscale ? ToGrayscale(bitmap) : bitmap);
					if (rotation == 90.0)
					{
						bitmap3.RotateFlip(RotateFlipType.Rotate90FlipNone);
					}
					if (rotation == -90.0)
					{
						bitmap3.RotateFlip(RotateFlipType.Rotate270FlipNone);
					}
					graphics.DrawImage(bitmap3, x, y, width, height);
					if (bitmap3 != bitmap)
					{
						bitmap3.Dispose();
					}
					if (!string.IsNullOrWhiteSpace(watermark))
					{
						GraphicsState gstate = graphics.Save();
						graphics.TranslateTransform((float)num3 / 2f, (float)num4 / 2f);
						graphics.RotateTransform(-45f);
						Font font = new Font("Arial", (float)((double)(72f * val) / 72.0), System.Drawing.FontStyle.Bold);
						SolidBrush brush = new SolidBrush(Color.FromArgb(80, 255, 0, 0));
						SizeF sizeF = graphics.MeasureString(watermark, font);
						graphics.DrawString(watermark, font, brush, (0f - sizeF.Width) / 2f, (0f - sizeF.Height) / 2f);
						graphics.Restore(gstate);
					}
				}
				nint hbitmap = bitmap2.GetHbitmap();
				try
				{
					BitmapSource bitmapSource2 = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
					((Freezable)bitmapSource2).Freeze();
					return bitmapSource2;
				}
				finally
				{
					DeleteObject(hbitmap);
					bitmap2.Dispose();
				}
			});
		}
		finally
		{
			_renderLock.Release();
		}
		if (gen == _renderGeneration && bitmapSource != null)
		{
			InlinePreviewImage.Source = bitmapSource;
		}
	}

	private void ClearInlinePreview()
	{
		_previewDoc?.Dispose();
		_previewDoc = null;
		_previewFilePath = null;
		_previewPageCount = 0;
		_previewPage = 0;
		InlinePreviewImage.Source = null;
		InlinePageLabel.Text = "Select a file to preview";
		InlineFileLabel.Text = "";
		InlinePrevBtn.IsEnabled = false;
		InlineNextBtn.IsEnabled = false;
		PopOutBtn.IsEnabled = false;
	}

	private async void RenderInlinePage(int pageIndex)
	{
		if (_previewDoc == null || pageIndex < 0 || pageIndex >= _previewPageCount)
		{
			return;
		}
		_previewPage = pageIndex;
		int gen = ++_renderGeneration;
		List<int> validPreviewPages = GetValidPreviewPages();
		int num = validPreviewPages.IndexOf(pageIndex);
		InlinePageLabel.Text = ((validPreviewPages.Count < _previewPageCount && validPreviewPages.Count > 0) ? $"Page {pageIndex + 1}  ({num + 1} of {validPreviewPages.Count} in range)" : $"Page {pageIndex + 1} of {_previewPageCount}");
		InlinePrevBtn.IsEnabled = num > 0;
		InlineNextBtn.IsEnabled = num >= 0 && num < validPreviewPages.Count - 1;
		string orientation = OrientationComboBox?.Text ?? "Auto";
		bool grayscale = ColorComboBox?.Text == "Grayscale";
		string paperSizeName = PaperSizeComboBox?.Text ?? "A4";
		string fitMode = (FitModeComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "FitToPage";
		string watermark = WatermarkTextBox?.Text ?? "";
		SizeF pageSize = _previewDoc.PageSizes[pageIndex];
		bool pageIsPortrait = pageSize.Height >= pageSize.Width;
		SizeF paperPts = GetPaperSizePoints(paperSizeName, orientation, pageSize.Width, pageSize.Height);
		double num2 = Math.Max(80.0, InlineScrollViewer.ActualWidth - 40.0);
		float dpi = (float)(96.0 * (num2 / ((double)(paperPts.Width * 96f) / 72.0)));
		dpi = Math.Max(10f, Math.Min(dpi, 250f));
		int paperW = Math.Max(1, (int)((double)(paperPts.Width * dpi) / 72.0));
		int paperH = Math.Max(1, (int)((double)(paperPts.Height * dpi) / 72.0));
		BitmapSource bitmapSource = null;
		await _renderLock.WaitAsync();
		try
		{
			bitmapSource = await Task.Run(delegate
			{
				//IL_0445: Unknown result type (might be due to invalid IL or missing references)
				if (gen != _renderGeneration)
				{
					return (BitmapSource)null;
				}
				double rotation = 0.0;
				if (orientation == "Landscape" && pageIsPortrait)
				{
					rotation = 90.0;
				}
				if (orientation == "Portrait" && !pageIsPortrait)
				{
					rotation = -90.0;
				}
				((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
				{
					if (gen == _renderGeneration)
					{
						string value = ((paperPts.Width > paperPts.Height) ? "landscape" : "portrait");
						string value2 = ((rotation == 90.0) ? " (90° CW)" : ((rotation == -90.0) ? " (270° CCW)" : ""));
						PaperInfoLabel.Text = $"Paper: '{paperSizeName} ({(double)paperPts.Width / 72.0:F1} x {(double)paperPts.Height / 72.0:F1} in)', {value}{value2}";
					}
				});
				Bitmap bitmap = new Bitmap(paperW, paperH);
				using (Graphics graphics = Graphics.FromImage(bitmap))
				{
					graphics.Clear(Color.White);
					float num3 = 12f * dpi / 72f;
					using (Pen pen = new Pen(Color.LightGray, 1f))
					{
						pen.DashStyle = DashStyle.Dash;
						graphics.DrawRectangle(pen, num3, num3, (float)paperW - 2f * num3, (float)paperH - 2f * num3);
					}
					float num4 = pageSize.Width;
					float num5 = pageSize.Height;
					if (rotation != 0.0)
					{
						num4 = pageSize.Height;
						num5 = pageSize.Width;
					}
					float num6 = 1f;
					if (fitMode == "FitToPage")
					{
						num6 = Math.Min(paperPts.Width / num4, paperPts.Height / num5);
					}
					else if (fitMode == "ShrinkToFit")
					{
						num6 = Math.Min(1f, Math.Min(paperPts.Width / num4, paperPts.Height / num5));
					}
					float num7 = num4 * num6;
					float num8 = num5 * num6;
					float num9 = (paperPts.Width - num7) / 2f;
					float num10 = (paperPts.Height - num8) / 2f;
					int x = (int)(num9 * dpi / 72f);
					int y = (int)(num10 * dpi / 72f);
					int width = Math.Max(1, (int)(num7 * dpi / 72f));
					int height = Math.Max(1, (int)(num8 * dpi / 72f));
					int width2 = Math.Max(1, (int)(pageSize.Width * num6 * dpi / 72f));
					int height2 = Math.Max(1, (int)(pageSize.Height * num6 * dpi / 72f));
					using Bitmap bitmap2 = (Bitmap)_previewDoc.Render(pageIndex, width2, height2, dpi, dpi, forPrinting: false);
					Bitmap bitmap3 = (grayscale ? ToGrayscale(bitmap2) : bitmap2);
					if (rotation == 90.0)
					{
						bitmap3.RotateFlip(RotateFlipType.Rotate90FlipNone);
					}
					if (rotation == -90.0)
					{
						bitmap3.RotateFlip(RotateFlipType.Rotate270FlipNone);
					}
					graphics.DrawImage(bitmap3, x, y, width, height);
					if (bitmap3 != bitmap2)
					{
						bitmap3.Dispose();
					}
					if (!string.IsNullOrWhiteSpace(watermark))
					{
						GraphicsState gstate = graphics.Save();
						graphics.TranslateTransform((float)paperW / 2f, (float)paperH / 2f);
						graphics.RotateTransform(-45f);
						Font font = new Font("Arial", (float)((double)(72f * dpi) / 72.0), System.Drawing.FontStyle.Bold);
						SolidBrush brush = new SolidBrush(Color.FromArgb(80, 255, 0, 0));
						SizeF sizeF = graphics.MeasureString(watermark, font);
						graphics.DrawString(watermark, font, brush, (0f - sizeF.Width) / 2f, (0f - sizeF.Height) / 2f);
						graphics.Restore(gstate);
					}
				}
				nint hbitmap = bitmap.GetHbitmap();
				try
				{
					BitmapSource bitmapSource2 = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
					((Freezable)bitmapSource2).Freeze();
					return bitmapSource2;
				}
				finally
				{
					DeleteObject(hbitmap);
					bitmap.Dispose();
				}
			});
		}
		finally
		{
			_renderLock.Release();
		}
		if (gen == _renderGeneration && bitmapSource != null)
		{
			InlinePreviewImage.Source = bitmapSource;
		}
	}

	private List<int> GetValidPreviewPages()
	{
		if (_previewDoc == null)
		{
			return new List<int>();
		}
		string text = PageRangeTextBox?.Text?.Trim() ?? "";
		if (string.IsNullOrEmpty(text))
		{
			return Enumerable.Range(0, _previewPageCount).ToList();
		}
		List<int> list = new List<int>();
		string[] array = text.Split(',');
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = array[i].Trim();
			int result3;
			if (text2.Contains("-"))
			{
				string[] array2 = text2.Split('-');
				if (array2.Length != 2 || !int.TryParse(array2[0], out var result) || !int.TryParse(array2[1], out var result2))
				{
					continue;
				}
				result = Math.Max(1, result) - 1;
				result2 = Math.Min(_previewPageCount, result2) - 1;
				for (int j = result; j <= result2; j++)
				{
					if (!list.Contains(j))
					{
						list.Add(j);
					}
				}
			}
			else if (int.TryParse(text2, out result3))
			{
				result3--;
				if (result3 >= 0 && result3 < _previewPageCount && !list.Contains(result3))
				{
					list.Add(result3);
				}
			}
		}
		list.Sort();
		if (list.Count <= 0)
		{
			return Enumerable.Range(0, _previewPageCount).ToList();
		}
		return list;
	}

	private Bitmap ToGrayscale(Bitmap source)
	{
		Bitmap bitmap = new Bitmap(source.Width, source.Height);
		ColorMatrix colorMatrix = new ColorMatrix(new float[5][]
		{
			new float[5] { 0.299f, 0.299f, 0.299f, 0f, 0f },
			new float[5] { 0.587f, 0.587f, 0.587f, 0f, 0f },
			new float[5] { 0.114f, 0.114f, 0.114f, 0f, 0f },
			new float[5] { 0f, 0f, 0f, 1f, 0f },
			new float[5] { 0f, 0f, 0f, 0f, 1f }
		});
		ImageAttributes imageAttributes = new ImageAttributes();
		imageAttributes.SetColorMatrix(colorMatrix);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, imageAttributes);
		return bitmap;
	}

	private void InlinePrevBtn_Click(object sender, RoutedEventArgs e)
	{
		List<int> validPreviewPages = GetValidPreviewPages();
		int num = validPreviewPages.IndexOf(_previewPage);
		if (num > 0)
		{
			RenderInlinePage(validPreviewPages[num - 1]);
		}
	}

	private void InlineNextBtn_Click(object sender, RoutedEventArgs e)
	{
		List<int> validPreviewPages = GetValidPreviewPages();
		int num = validPreviewPages.IndexOf(_previewPage);
		if (num >= 0 && num < validPreviewPages.Count - 1)
		{
			RenderInlinePage(validPreviewPages[num + 1]);
		}
	}

	private void InlineScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (_previewDoc != null)
		{
			RenderInlinePage(_previewPage);
		}
	}

	private void PopOutBtn_Click(object sender, RoutedEventArgs e)
	{
		if (_previewFilePath != null)
		{
			PdfViewerWindow viewerWindow = new PdfViewerWindow(_previewFilePath);
			viewerWindow.Owner = this;
			viewerWindow.Show();
		}
	}

	private void HistoryBtn_Click(object sender, RoutedEventArgs e)
	{
		new HistoryWindow().ShowDialog();
	}

	private void ScanBtn_Click(object sender, RoutedEventArgs e)
	{
		string? scannedFilePath = PdfPrintUtility.Services.ScannerService.ScanDocument();
		if (!string.IsNullOrEmpty(scannedFilePath))
		{
			ScannerWorkspaceWindow workspace = new ScannerWorkspaceWindow(scannedFilePath);
			workspace.Owner = this;
			if (workspace.ShowDialog() == true)
			{
				foreach (var file in workspace.FinalFiles)
				{
					if (!_observableFiles.Any(f => f.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase)))
					{
						_observableFiles.Add(new PrintFileItem { FilePath = file });
					}
				}
				if (FilesListBox.SelectedIndex == -1 && _observableFiles.Count > 0)
				{
					FilesListBox.SelectedIndex = 0;
				}
			}
		}
	}

	private void PrintButton_Click(object sender, RoutedEventArgs e)
	{
		if (_observableFiles.Count == 0)
		{
			MessageBox.Show("Please add at least one PDF file to print.", "Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (!short.TryParse(CopiesTextBox.Text, out var result) || result < 1)
		{
			MessageBox.Show("Please enter a valid number of copies.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		PrintSettings currentSettings = GetSettingsFromUI();
		PrintSettings printSettings = _settingsData.Profiles.FirstOrDefault((PrintSettings p) => p.ProfileName == currentSettings.ProfileName);
		if (printSettings != null)
		{
			_settingsData.Profiles.Remove(printSettings);
		}
		_settingsData.Profiles.Add(currentSettings);
		_settingsData.LastUsedProfile = currentSettings.ProfileName;
		SettingsManager.SaveData(_settingsData);
		_previewDoc?.Dispose();
		_previewDoc = null;
		string pageRange = PageRangeTextBox.Text.Trim();
		new ProgressWindow(_observableFiles.Select((PrintFileItem f) => f.FilePath).ToList(), currentSettings, pageRange).Show();
		Close();
	}

	private void MergeBtn_Click(object sender, RoutedEventArgs e)
	{
		if (_observableFiles.Count == 0)
		{
			MessageBox.Show("Please add at least one PDF file to merge.", "Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		PageArrangerWindow pageArrangerWindow = new PageArrangerWindow(_observableFiles.ToList());
		pageArrangerWindow.Owner = this;
		pageArrangerWindow.ShowDialog();
	}

	private void RotateItem_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuItem { Tag: PrintFileItem tag })
		{
			tag.Rotation = (tag.Rotation + 90) % 360;
			if (tag.FilePath == _previewFilePath)
			{
				SchedulePreviewRefresh();
			}
		}
	}

	private void MoveUp_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuItem { Tag: PrintFileItem tag })
		{
			int num = _observableFiles.IndexOf(tag);
			if (num > 0)
			{
				_observableFiles.Move(num, num - 1);
			}
		}
	}

	private void MoveDown_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuItem { Tag: PrintFileItem tag })
		{
			int num = _observableFiles.IndexOf(tag);
			if (num >= 0 && num < _observableFiles.Count - 1)
			{
				_observableFiles.Move(num, num + 1);
			}
		}
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e)
	{
		Application.Current.Shutdown();
	}

		private void ViewPdf_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuItem { Tag: PrintFileItem item })
		{
			PdfViewerWindow viewerWindow = new PdfViewerWindow(item.FilePath);
			viewerWindow.Owner = this;
			viewerWindow.Show();
		}
	}
}