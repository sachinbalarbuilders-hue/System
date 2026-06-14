using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PdfPrintUtility.Views;

namespace PdfPrintUtility
{
    public partial class App : System.Windows.Application
    {
        private const string MutexName = "PdfPrintUtility_SingleInstance_Mutex";
        private const string PipeName = "PdfPrintUtility_Pipe";
        private Mutex _mutex;
        private List<string> _filesToPrint = new List<string>();
        private CancellationTokenSource _cts = new CancellationTokenSource();

        private bool _startupComplete = false;

        public static PdfViewerWindow CurrentViewer { get; set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"Unhandled Exception: {args.Exception.Message}\n\nStack Trace:\n{args.Exception.StackTrace}", "Application Crash", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true; // Prevent silent crash
            };

            bool isPrintCommand = false;
            string fileArg = null;

            // Parse arguments
            foreach (var arg in e.Args)
            {
                if (arg.Equals("-print", StringComparison.OrdinalIgnoreCase))
                    isPrintCommand = true;
                else if (File.Exists(arg))
                    fileArg = arg;
            }

            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                // Another instance is running, send arguments via Named Pipe
                if (fileArg != null)
                {
                    try
                    {
                        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                        client.Connect(1000); // Wait up to 1s
                        using var writer = new StreamWriter(client);
                        writer.WriteLine(isPrintCommand ? $"-print|{fileArg}" : fileArg);
                        writer.Flush();
                    }
                    catch
                    {
                        // Ignore errors if pipe fails
                    }
                }

                Shutdown();
                return;
            }

            // Primary Instance
            if (fileArg != null)
            {
                _filesToPrint.Add(fileArg);

                // Start listening on named pipe for other instances
                _ = Task.Run(() => ListenForFilesAsync(_cts.Token));

                // Wait 500ms to collect all paths from secondary instances
                await Task.Delay(500);
                _startupComplete = true; // Mark startup as complete, but DO NOT stop listening!

                // If it's a single file and NOT a print command, open Viewer
                if (!isPrintCommand && _filesToPrint.Count == 1 && _filesToPrint[0].EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    var viewerWindow = new PdfViewerWindow(_filesToPrint[0]);
                    viewerWindow.Show();
                    CurrentViewer = viewerWindow;
                }
                else
                {
                    var printJobWindow = new PrintJobWindow(_filesToPrint);
                    printJobWindow.Show();
                }
            }
            else
            {
                _startupComplete = true;
                // Open Print Job Window (with 0 files)
                var printJobWindow = new PrintJobWindow(_filesToPrint);
                printJobWindow.Show();
            }
        }

        private async Task ListenForFilesAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(token);

                    using var reader = new StreamReader(server);
                    string line = await reader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        string file = line;
                        bool isPrintMsg = false;

                        if (line.StartsWith("-print|"))
                        {
                            isPrintMsg = true;
                            file = line.Substring(7);
                        }

                        if (!string.IsNullOrWhiteSpace(file))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (!_startupComplete)
                                {
                                    _filesToPrint.Add(file);
                                }
                                else
                                {
                                    // If startup is already complete, we pop open a new window immediately
                                    if (isPrintMsg)
                                    {
                                        var printJobWindow = new PrintJobWindow(new List<string> { file });
                                        printJobWindow.Show();
                                    }
                                    else if (file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (CurrentViewer != null && CurrentViewer.IsLoaded)
                                        {
                                            CurrentViewer.AddTab(file);
                                            if (CurrentViewer.WindowState == WindowState.Minimized)
                                                CurrentViewer.WindowState = WindowState.Normal;
                                            CurrentViewer.Activate();
                                        }
                                        else
                                        {
                                            var viewerWindow = new PdfViewerWindow(file);
                                            viewerWindow.Show();
                                            CurrentViewer = viewerWindow;
                                        }
                                    }
                                    else
                                    {
                                        var printJobWindow = new PrintJobWindow(new List<string> { file });
                                        printJobWindow.Show();
                                    }
                                }
                            });
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Ignore pipe errors
                }
            }
        }


        protected override void OnExit(ExitEventArgs e)
        {
            if (_mutex != null)
            {
                try { _mutex.ReleaseMutex(); } catch { }
                _mutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}
