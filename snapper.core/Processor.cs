using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace snapper.core
{
    public class Processor : IDisposable
    {
        private Bitmap _lastBitmap = null;
        private List<string> _lastWindowTitles = new List<string>();
        private string _logFolder;
        private string _logFileName;
        private string _keysFileName;
        private string _debugFileName;
        private bool _running;
        private GlobalKeyboardHook _globalKeyboardHook;
        private DateTime _lastKeystroke;
        private ProcessorConfig _config;

        private void Debug(string message)
        {
            if (!_config.Debug)
            {
                return;
            }
            File.AppendAllText(_debugFileName, $@"{message}
");
        }

        public void Start(ProcessorConfig config)
        {
            _config = config;

            _debugFileName = Path.Combine(config.RootFolderPath, "debug.log");
            Debug($"Log folder: {config.RootFolderPath}");

            string userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            if (!Regex.IsMatch(userName, config.UsernamePattern))
            {
                Debug($"Username {userName} does not match pattern {config.UsernamePattern}");
                return;
            }

            _logFolder = Path.Combine(config.RootFolderPath, $"{DateTime.Now.ToString("yyyyMMdd")}");
            Directory.CreateDirectory(_logFolder);

            _logFileName = Path.Combine(_logFolder, "windows.log");
            if (!File.Exists(_logFileName))
            {
                File.WriteAllText(_logFileName, $@"Starting up...

");
            }

            _keysFileName = Path.Combine(_logFolder, "keys.log");
            if (!File.Exists(_keysFileName))
            {
                File.WriteAllText(_keysFileName, $@"Starting up...

");
            }

            //SetupKeyboardHooks();

            _running = true;

            Debug("About to start infinite loop...");

            var i = 1;
            while (_running)
            {
                try
                {
                    Debug($"{i}");
                    CaptureScreenshot();
                    CaptureWindows();
                } catch (Exception ex)
                {
                    Debug(ex.ToString());
                }
                Thread.Sleep(config.PauseSeconds * 1000);
                if (i % 10 == 0)
                {
                    CheckDiskUsage(config);
                }
                i++;
            }
        }

        private void CheckDiskUsage(ProcessorConfig config)
        {
            long length = Directory.GetFiles(config.RootFolderPath, "*", SearchOption.AllDirectories).Sum(t => (new FileInfo(t).Length));
            var lengthMB = (double)length / 1024 / 1024;
            if (lengthMB < config.MaxDiskSpaceMB)
            {
                return;
            }
            var dirNames = Directory.GetDirectories(config.RootFolderPath);
            Directory.Delete(dirNames.OrderBy(d => d).First(), true);
        }

        private void SetupKeyboardHooks()
        {
            _globalKeyboardHook = new GlobalKeyboardHook();
            _globalKeyboardHook.KeyboardPressed += OnKeyPressed;
        }

        private void OnKeyPressed(object sender, GlobalKeyboardHookEventArgs e)
        {
            if (e.KeyboardState == GlobalKeyboardHook.KeyboardState.KeyDown)
            {
                var appName = GetForegroundProcessName();
                File.AppendAllText(_keysFileName, $@"{appName}
{e.KeyboardData.VirtualCode}");
                e.Handled = true;
            }
        }

        public void Dispose()
        {
            Stop();
            _globalKeyboardHook?.Dispose();
        }

        public void Stop()
        {
            Debug("Stopping");
            _running = false;
        }

        // The GetForegroundWindow function returns a handle to the foreground window
        // (the window  with which the user is currently working).
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        // The GetWindowThreadProcessId function retrieves the identifier of the thread
        // that created the specified window and, optionally, the identifier of the
        // process that created the window.
        [DllImport("user32.dll")]
        private static extern Int32 GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // Returns the name of the process owning the foreground window.
        private string GetForegroundProcessName()
        {
            IntPtr hwnd = GetForegroundWindow();

            // The foreground window can be NULL in certain circumstances, 
            // such as when a window is losing activation.
            if (hwnd == null)
            {
                return "Unknown";
            }

            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);

            foreach (Process p in Process.GetProcesses())
            {
                if (p.Id == pid)
                {
                    return $"[{p.ProcessName}] {p.MainWindowTitle}";
                }
            }

            return "Unknown";
        }

        private void CaptureWindows()
        {
            var windowTitles = new List<string>();
            Process[] processes = Process.GetProcesses();
            foreach (Process p in processes)
            {
                if (!String.IsNullOrEmpty(p.MainWindowTitle))
                {
                    windowTitles.Add($"[{p.ProcessName}] {p.MainWindowTitle}");
                }
            }
            if (!windowTitles.SequenceEqual(_lastWindowTitles))
            {
                WriteWindowTitles(windowTitles);
                _lastWindowTitles = windowTitles;
                Debug($"Written changed window titles");
            }
        }

        private void WriteWindowTitles(List<string> windowTitles)
        {
            var titles = string.Join(Environment.NewLine, windowTitles);
            File.AppendAllText(_logFileName, $@"[{DateTime.Now.ToString("hh:mm:ss")}]
{titles}

");
        }

        private void CaptureScreenshot()
        {
            using (Bitmap bmpScreenCapture = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
                                                Screen.PrimaryScreen.Bounds.Height))
            using (Graphics g = Graphics.FromImage(bmpScreenCapture))
            {
                g.CopyFromScreen(Screen.PrimaryScreen.Bounds.X,
                                 Screen.PrimaryScreen.Bounds.Y,
                                 0, 0,
                                 bmpScreenCapture.Size,
                                 CopyPixelOperation.SourceCopy);

                if (!_config.TryToSaveSpace || (_lastBitmap == null || !CompareBitmapsFast(_lastBitmap, bmpScreenCapture)))
                {
                    var fileName = $"{DateTime.Now.ToString("yyyyMMddHHmmss")}.png";
                    bmpScreenCapture.Save($"{_logFolder}/{fileName}", ImageFormat.Png);
                    _lastBitmap = bmpScreenCapture.Clone(new Rectangle(0, 0, Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height), bmpScreenCapture.PixelFormat);
                    Debug($"Captured screenshot {fileName}");
                }
            }
        }

        private bool CompareBitmapsFast(Bitmap bmp1, Bitmap bmp2)
        {
            if (bmp1 == null || bmp2 == null)
                return false;
            if (object.Equals(bmp1, bmp2))
                return true;
            if (!bmp1.Size.Equals(bmp2.Size) || !bmp1.PixelFormat.Equals(bmp2.PixelFormat))
                return false;

            int bytes = bmp1.Width * bmp1.Height * (Image.GetPixelFormatSize(bmp1.PixelFormat) / 8);

            bool result = true;
            byte[] b1bytes = new byte[bytes];
            byte[] b2bytes = new byte[bytes];

            BitmapData bitmapData1 = bmp1.LockBits(new Rectangle(0, 0, bmp1.Width - 1, bmp1.Height - 1), ImageLockMode.ReadOnly, bmp1.PixelFormat);
            BitmapData bitmapData2 = bmp2.LockBits(new Rectangle(0, 0, bmp2.Width - 1, bmp2.Height - 1), ImageLockMode.ReadOnly, bmp2.PixelFormat);

            Marshal.Copy(bitmapData1.Scan0, b1bytes, 0, bytes);
            Marshal.Copy(bitmapData2.Scan0, b2bytes, 0, bytes);

            for (int n = 0; n <= bytes - 1; n++)
            {
                if (b1bytes[n] != b2bytes[n])
                {
                    result = false;
                    break;
                }
            }

            bmp1.UnlockBits(bitmapData1);
            bmp2.UnlockBits(bitmapData2);

            return result;
        }
    }
}
