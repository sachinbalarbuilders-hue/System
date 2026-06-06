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
    public partial class App : Application
    {
        private const string MutexName = "PdfPrintUtility_SingleInstance_Mutex";
        private const string PipeName = "PdfPrintUtility_Pipe";
        private Mutex _mutex;
        private List<string> _filesToPrint = new List<string>();
        private CancellationTokenSource _cts = new CancellationTokenSource();

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                // Another instance is running, send arguments via Named Pipe
                if (e.Args.Length > 0)
                {
                    try
                    {
                        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                        client.Connect(1000); // Wait up to 1s
                        using var writer = new StreamWriter(client);
                        writer.WriteLine(e.Args[0]);
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
            if (e.Args.Length > 0)
            {
                _filesToPrint.Add(e.Args[0]);
                
                // Start listening on named pipe for other instances
                _ = Task.Run(() => ListenForFilesAsync(_cts.Token));

                // Wait 500ms to collect all paths from secondary instances
                await Task.Delay(500);
                _cts.Cancel(); // Stop listening
                
                // Open Print Job Window instead of printing immediately
                var printJobWindow = new PrintJobWindow(_filesToPrint);
                printJobWindow.Show();
            }
            else
            {
                // Open Print Job Window (with 0 files) to allow registering context menu
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
                    string file = await reader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(file))
                    {
                        Dispatcher.Invoke(() => _filesToPrint.Add(file));
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
