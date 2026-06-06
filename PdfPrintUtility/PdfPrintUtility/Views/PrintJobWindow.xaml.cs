using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Windows;
using Microsoft.Win32;
using PdfPrintUtility.Models;

namespace PdfPrintUtility.Views
{
    public partial class PrintJobWindow : Window
    {
        private readonly List<string> _files;

        public PrintJobWindow(List<string> files)
        {
            InitializeComponent();
            _files = files;
            SummaryTextBlock.Text = $"Ready to print {_files.Count} selected PDFs.";
            LoadPrinters();
            LoadSettings();
        }

        private void LoadPrinters()
        {
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                PrinterComboBox.Items.Add(printer);
            }
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Load();
            
            if (PrinterComboBox.Items.Contains(settings.DefaultPrinter))
                PrinterComboBox.SelectedItem = settings.DefaultPrinter;
            else if (PrinterComboBox.Items.Count > 0)
                PrinterComboBox.SelectedIndex = 0;

            CopiesTextBox.Text = settings.Copies.ToString();
            DuplexCheckBox.IsChecked = settings.Duplex;
            CollateCheckBox.IsChecked = settings.Collate;

            ColorComboBox.Text = settings.ColorMode;
            OrientationComboBox.Text = settings.Orientation;
            PaperSizeComboBox.Text = settings.PaperSize;
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            if (!short.TryParse(CopiesTextBox.Text, out short copies) || copies < 1)
            {
                MessageBox.Show("Please enter a valid number of copies.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var settings = new PrintSettings
            {
                DefaultPrinter = PrinterComboBox.SelectedItem?.ToString() ?? "",
                Copies = copies,
                Duplex = DuplexCheckBox.IsChecked ?? false,
                Collate = CollateCheckBox.IsChecked ?? true,
                ColorMode = ColorComboBox.Text,
                Orientation = OrientationComboBox.Text,
                PaperSize = PaperSizeComboBox.Text
            };

            SettingsManager.Save(settings);

            var progressWindow = new ProgressWindow(_files);
            progressWindow.Show();
            
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                string keyPath = @"Software\Classes\SystemFileAssociations\.pdf\shell\PdfPrintUtility";
                
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    key.SetValue("MUIVerb", "Print All PDFs");
                    key.SetValue("MultiSelectModel", "Player");
                    
                    using (RegistryKey commandKey = key.CreateSubKey("command"))
                    {
                        commandKey.SetValue("", $"\"{exePath}\" \"%1\"");
                    }
                }
                MessageBox.Show("Context menu registered successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to register context menu: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UnregisterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string keyPath = @"Software\Classes\SystemFileAssociations\.pdf\shell\PdfPrintUtility";
                Registry.CurrentUser.DeleteSubKeyTree(keyPath, false);
                MessageBox.Show("Context menu unregistered successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to unregister context menu: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
