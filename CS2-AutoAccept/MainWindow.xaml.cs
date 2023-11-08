using System;
using System.IO;
using Tesseract;
using System.Windows;
using System.Drawing;
using System.Net.Http;
using Microsoft.Win32;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Forms;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CS2AutoAccept;
using System.Net.Http.Headers;
using System.Linq;
using System.Text.Json;

namespace CS2_AutoAccept
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region DLL Imports For Mouse Manipulation
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);
        // Mouse actions
        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const int MOUSEEVENTF_RIGHTUP = 0x10;
        #endregion
        private Updater? updater;
        private Screen? _activeScreen;
        private Thread? _scannerThread;
        private CancellationTokenSource? cts;
        private bool _run_Continuously = false;
        private bool _updateAvailable = false;
        private bool _updateFailed = false;
        private int _acceptPosX;
        private int _acceptPosY;
        private int _acceptWidth;
        private int _acceptHeight;
        private int _cancelPosX;
        private int _cancelPosY;
        private int _cancelWidth;
        private int _cancelHeight;
        private int _clickPosX;
        private int _clickPosY;
        private int _gameRunExtraDelay = 0; // Seconds
        private string _basePath;
        private string _updatePath;
        public MainWindow()
        {
            InitializeComponent();

            _basePath = Path.Combine(Environment.ExpandEnvironmentVariables("%APPDATA%"), "CS2 AutoAccept");
            _updatePath = Path.Combine(_basePath, "UPDATE");

            //ControlLocation();

            _ = UpdateHeaderVersion();
            updater = new Updater();
            updater.DownloadProgress += Updater_ProgressUpdated!;
            Thread GameRunningThread = new Thread(IsGameRunning);
            GameRunningThread.Start();
            GameRunningThread.IsBackground = true;

            try
            {
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                Run_at_startup_state.IsChecked = key.GetValue("CS2-AutoAccept") != null;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "CS2 AutoAccept", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            #region Delete old runPath
            //if (File.Exists(Path.Combine(_basePath, "DELETE_ME.tsgs")))
            //{
            //    string folderToDelete = File.ReadAllText(Path.Combine(_basePath, "DELETE_ME.tsgs"));
            //    try
            //    {
            //        if (Directory.Exists(folderToDelete))
            //        {
            //            Directory.Delete(folderToDelete, true);
            //        }

            //        File.Delete(Path.Combine(_basePath, "DELETE_ME.tsgs"));
            //    }
            //    catch (Exception)
            //    { }
            //}
            #endregion

            #region Update
            if (Directory.Exists(_updatePath))
            {
                string runPath = AppContext.BaseDirectory;

                if (runPath.LastIndexOf('\\') == runPath.Length - 1)
                {
                    runPath = runPath[..^1]; // Remove the last character
                }

                string lastFolderInPath = runPath.Split('\\')[^1];

                string updatePath = Path.Combine(_basePath, "UPDATE");

                // If true, the exe was run from inside the UPDATE folder
                if (lastFolderInPath == "UPDATE")
                {
                    string[] updatedFiles = Directory.GetFiles(updatePath, "*", SearchOption.TopDirectoryOnly);
                    string[] updatedDirectories = Directory.GetDirectories(updatePath, "*", SearchOption.TopDirectoryOnly);

                    // Delete all the old files and folders
                    DeleteAllExceptFolder(_basePath, "UPDATE");

                    // Move all the new files and folders, to the basePath
                    foreach (string filePath in updatedFiles)
                    {
                        string fileName = filePath.Substring(updatePath.Length + 1);
                        string destinationPath = Path.Combine(_basePath, fileName);

                        try
                        {
                            File.Copy(filePath, destinationPath, true);
                            Debug.WriteLine($"Copied: {fileName}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error copying {fileName}: {ex.Message}");
                        }
                    }

                    foreach (string directoryPath in updatedDirectories)
                    {
                        string directoryName = directoryPath.Substring(updatePath.Length + 1);
                        string destinationPath = Path.Combine(_basePath, directoryName);

                        try
                        {
                            Directory.Move(directoryPath, destinationPath);
                            Debug.WriteLine($"Moved: {directoryName}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error copying {directoryName}: {ex.Message}");
                        }
                    }

                    // Start the updated program, in the new default path
                    Process.Start(Path.Combine(_basePath, "CS2-AutoAccept"));
                    Environment.Exit(0);
                }
                else
                {
                    // Try to delete the update folder, if not run from that path
                    try
                    {
                        Directory.Delete(updatePath, true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to delete the update directoryPath: {ex.Message}");
                    }
                }
            }
            #endregion

        }
        #region EventHandlers
        /// <summary>
        /// Event handler for download progress
        /// </summary>
        /// <param Name="sender"></param>
        /// <param Name="progress"></param>
        private void Updater_ProgressUpdated(object sender, ProgressEventArgs e)
        {
            if (!_updateFailed)
            {
                // Update the UI with the progress value
                Dispatcher.Invoke(() =>
                {
                    if (e.Status != "" && e.Status != null)
                    {
                        Progress_Download.Visibility = Visibility.Collapsed;
                        TextBlock_Progress.Visibility = Visibility.Collapsed;

                        _ = UpdateHeaderVersion();
                        Button_Update.IsEnabled = true;
                        Program_state.Visibility = Visibility.Visible;
                        Program_state_continuously.Visibility = Visibility.Visible;
                        Run_at_startup_state.Visibility = Visibility.Visible;

                        System.Windows.MessageBox.Show($"Update Failed, please try again later, or download it directly from the Github page!\n\nError Message: {e.Status}", "CS2 AutoAccept", MessageBoxButton.OK, MessageBoxImage.Error);
                        _updateFailed = true;
                    }
                    else if (e.Progress < 100)
                    {
                        // Update your UI elements with the progress value, e.g., a ProgressBar
                        Progress_Download.Visibility = Visibility.Visible;
                        TextBlock_Progress.Visibility = Visibility.Visible;
                        Progress_Download.Value = e.Progress;
                        TextBlock_Progress.Text = $"{e.Progress}%";
                    }
                });
            }
        }
        /// <summary>
        /// Minimize button
        /// </summary>
        /// <param Name="sender"></param>
        /// <param Name="e"></param>
        private void Button_Click_Minimize(object sender, RoutedEventArgs e)
        {
            // PrintToLog("{Button_Click_Close}");
            WindowState = WindowState.Minimized;
        }
        /// <summary>
        /// Close button
        /// </summary>
        /// <param Name="sender"></param>
        /// <param Name="e"></param>
        private void Button_Click_Close(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        /// <summary>
        /// Open Github to download the newest version
        /// </summary>
        /// <param Name="sender"></param>
        /// <param Name="e"></param>
        private async void Button_Update_Click(object sender, RoutedEventArgs e)
        {
            // PrintToLog("{Button_Update_Click}");
            if (_updateAvailable)
            {
                _updateFailed = false;
                Button_Update.IsEnabled = false;
                Button_Update.Content = "Updating...";
                Program_state.IsChecked = false;
                Program_state.Visibility = Visibility.Collapsed;
                Program_state_continuously.Visibility = Visibility.Collapsed;
                Run_at_startup_state.Visibility = Visibility.Collapsed;
                updater!.DownloadUpdate(_updatePath);
            }
            else
            {
                _updateAvailable = await UpdateHeaderVersion();
            }

        }
        /// <summary>
        /// Open Discord
        /// </summary>
        /// <param Name="sender"></param>
        /// <param Name="e"></param>
        private void Button_Click_Discord(object sender, RoutedEventArgs e)
        {
            // PrintToLog("{Button_Click_Discord}");
            LaunchWeb(@"https://discord.gg/Cddu5aJ");
        }
        /// <summary>
        /// Launch CS2
        /// </summary>
        /// <param Name="sender"></param>
        /// <param Name="e"></param>
        private void Button_Click_LaunchCS2(object sender, RoutedEventArgs e)
        {
            // PrintToLog("{Button_Click_LaunchCS}");
            LaunchWeb("steam://rungameid/730");
            Button_LaunchCS.Content = "Launching CS2";
            _gameRunExtraDelay = 15;
        }
        /// <summary>
        /// Drag header
        /// </summary>
        /// <param Name="sender"></param>
        /// <param Name="e"></param>
        private void WindowHeader_Mousedown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
        /// <summary>
        /// State ON event
        /// </summary>
        /// <param Name="sender"></param>
        /// <param Name="e"></param>
        private void Program_state_Checked(object sender, RoutedEventArgs e)
        {
            // PrintToLog("{Program_state_Checked}");
            _scannerThread = new Thread(new ParameterizedThreadStart(Scanner)) { IsBackground = true };
            cts = new CancellationTokenSource();
            _scannerThread.Start(cts!.Token);
            // Change to a brighter color
            Program_state.Foreground = new SolidColorBrush(Colors.LawnGreen);
            Program_state.Content = "AutoAccept (ON)";
        }
        /// <summary>
        /// State OFF event
        /// </summary>
        /// <param Name="sender"></param>
        /// <param Name="e"></param>
        private void Program_state_Unchecked(object sender, RoutedEventArgs e)
        {
            // PrintToLog("{Program_state_Unchecked}");
            cts!.Cancel();
            Program_state_continuously.IsChecked = false;

            // Change to a darker color
            Program_state.Foreground = new SolidColorBrush(Colors.Red);
            Program_state.Content = "AutoAccept (OFF)";
        }
        /// <summary>
        /// 24/7 State ON event
        /// </summary>
        /// <param Name="sender"></param>
        /// <param Name="e"></param>
        private void Program_state_continuously_Checked(object sender, RoutedEventArgs e)
        {
            // PrintToLog("{Program_state_continuously_Checked}");
            Program_state.IsChecked = true;

            // Change to a brighter color
            Program_state_continuously.Foreground = new SolidColorBrush(Colors.LawnGreen);
            Program_state_continuously.Content = "Auto Accept Every Match (ON)";
            _run_Continuously = true;
        }
        /// <summary>
        /// 24/7 State OFF event
        /// </summary>
        /// <param Name="sender"></param>
        /// <param Name="e"></param>
        private void Program_state_continuously_Unchecked(object sender, RoutedEventArgs e)
        {
            // PrintToLog("{Program_state_continuously_Unchecked}");

            // Change to a darker color
            Program_state_continuously.Foreground = new SolidColorBrush(Colors.Red);
            Program_state_continuously.Content = "Auto Accept Every Match (OFF)";
            _run_Continuously = false;
        }
        /// <summary>
        /// Run at startup State ON event
        /// </summary>
        /// <param Name="sender"></param>
        /// <param Name="e"></param>
        private void Run_at_startup_state_Checked(object sender, RoutedEventArgs e)
        {
            // PrintToLog("{Run_at_startup_state_Checked}");
            string exeLocation = $"{AppContext.BaseDirectory}CS2-AutoAccept";
            try
            {
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");

                if (key.GetValue("CS2-AutoAccept") == null)
                {
                    key.SetValue("CS2-AutoAccept", exeLocation);
                }

                key.Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "CS2 AutoAccept", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Change to a brighter color
            Run_at_startup_state.Foreground = new SolidColorBrush(Colors.LawnGreen);
            Run_at_startup_state.Content = "Run at startup (ON)";
        }
        /// <summary>
        /// Run at startup State OFF event
        /// </summary>
        /// <param Name="sender"></param>
        /// <param Name="e"></param>
        private void Run_at_startup_state_Unchecked(object sender, RoutedEventArgs e)
        {
            // PrintToLog("{Run_at_startup_state_Unchecked}");
            try
            {
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");

                if (key.GetValue("CS2-AutoAccept") != null)
                {
                    key.DeleteValue("CS2-AutoAccept");
                }

                key.Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "CS2 AutoAccept", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Change to a darker color
            Run_at_startup_state.Foreground = new SolidColorBrush(Colors.Red);
            Run_at_startup_state.Content = "Run at startup (OFF)";
        }
        #endregion
        /// <summary>
        /// Control the location of the application,
        /// If not in the correct place Move it
        /// </summary>
        private void ControlLocation()
        {
            // Was the program ran from the correct folder?
            string runPath = AppContext.BaseDirectory;

            if (runPath.LastIndexOf('\\') == runPath.Length - 1)
            {
                runPath = runPath[..^1]; // Remove the last character
            }

            string lastFolderInPath = runPath.Split('\\')[^1];

            if (lastFolderInPath != "CS2 AutoAccept")
            {
                string[] files = Directory.GetFiles(runPath, "*", SearchOption.TopDirectoryOnly);
                string[] folders = Directory.GetDirectories(runPath, "*", SearchOption.TopDirectoryOnly);

                // Does the correct folder exist?
                if (!Directory.Exists(_basePath))
                {
                    Directory.CreateDirectory(_basePath);
                }
                else
                {
                    // Delete all the old files and folders
                    DeleteAllExceptFolder(_basePath, "");
                }

                File.WriteAllText(Path.Combine(_basePath, "DELETE_ME.tsgs"), runPath);

                // Move all the new files and folders, to the basePath
                foreach (string filePath in files)
                {
                    string fileName = filePath.Substring(runPath.Length + 1);
                    string destinationPath = Path.Combine(_basePath, fileName);

                    try
                    {
                        File.Copy(filePath, destinationPath, true);
                        Debug.WriteLine($"Copied: {fileName}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error copying {fileName}: {ex.Message}");
                    }
                }

                foreach (string directoryPath in folders)
                {
                    string directoryName = directoryPath.Substring(runPath.Length + 1);
                    string destinationPath = Path.Combine(_basePath, directoryName);

                    try
                    {
                        Directory.Move(directoryPath, destinationPath);
                        Debug.WriteLine($"Moved: {directoryName}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error copying {directoryName}: {ex.Message}");
                    }
                }

                // Start the updated program, in the new default path
                Process.Start(Path.Combine(_basePath, "CS2-AutoAccept"));
                Environment.Exit(0);

            }
        }
        /// <summary>
        /// Launch a web URL on Windows, Linux and OSX
        /// </summary>
        /// <param Name="url">The URL to open in the standard browser</param>
        private void LaunchWeb(string url)
        {
            // PrintToLog("{LaunchWeb}");
            try
            {
                Process.Start(url);
            }
            catch
            {
                // Hack for running the above line in DOTNET Core...
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw new Exception("Could not open the browser on this machine");
                }
            }
        }
        /// <summary>
        /// Check for any updates and append to the header
        /// </summary>
        private async Task<bool> UpdateHeaderVersion()
        {
            string fileVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion!;

            List<int> serverVersion = new List<int>();
            List<int> clientVersion = fileVersion.Split('.').Select(int.Parse).ToList();
            UpdateInfo serverUpdateInfo;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
                    {
                        NoCache = true
                    };

                    client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

                    serverUpdateInfo = JsonSerializer.Deserialize<UpdateInfo>(await client.GetStringAsync("https://raw.githubusercontent.com/tsgsOFFICIAL/CS2-AutoAccept/main/CS2-AutoAccept/updateInfo.json")) ?? new UpdateInfo();
                    serverVersion = serverUpdateInfo.Version.Split(".").Select(int.Parse).ToList();
                }

                // PrintToLog("{UpdateHeaderVersion} You are up-to-date!");
                Button_Update.Content = "You are up-to-date!";
                Button_Update.ToolTip = $"You are on the newest version ({clientVersion[0]}.{clientVersion[1]}.{clientVersion[2]}.{clientVersion[3]})\nYou can click at anytime to check again";
                Button_Update.Foreground = new SolidColorBrush(Colors.LawnGreen);
                _updateAvailable = false;

                // Is the update newer
                if ((clientVersion[0] < serverVersion[0]) || (clientVersion[1] < serverVersion[1] && clientVersion[0] <= serverVersion[0]) || (clientVersion[2] < serverVersion[2] && clientVersion[1] <= serverVersion[1] && clientVersion[0] <= serverVersion[0]) || (clientVersion[3] < serverVersion[3] && clientVersion[2] <= serverVersion[2] && clientVersion[1] <= serverVersion[1] && clientVersion[0] <= serverVersion[0]))
                {
                    // PrintToLog("{UpdateHeaderVersion} Update available");
                    Button_Update.Content = "Update Now";
                    Button_Update.ToolTip = $"Version {serverVersion[0]}.{serverVersion[1]}.{serverVersion[2]}.{serverVersion[3]} is now available!\nYou're on version {clientVersion[0]}.{clientVersion[1]}.{clientVersion[2]}.{clientVersion[3]}\nClick to update now";
                    Button_Update.Foreground = new SolidColorBrush(Colors.Orange);
                    _updateAvailable = true;
                }

                // Check if the user is on a newer build than the server
                if ((clientVersion[0] > serverVersion[0]) || (clientVersion[1] > serverVersion[1] && clientVersion[0] >= serverVersion[0]) || (clientVersion[2] > serverVersion[2] && clientVersion[1] >= serverVersion[1] && clientVersion[0] >= serverVersion[0]) || (clientVersion[3] > serverVersion[3] && clientVersion[2] >= serverVersion[2] && clientVersion[1] >= serverVersion[1] && clientVersion[0] >= serverVersion[0]))
                {
                    // PrintToLog("{UpdateHeaderVersion} You're on a dev build");
                    Button_Update.Content = "You're on a dev build";
                    Button_Update.ToolTip = $"Woooo! Look at you, you're on a dev build, version: {clientVersion[0]}.{clientVersion[1]}.{clientVersion[2]}.{clientVersion[3]}\nBe careful, Dev builds don't tend to be as stable.. ;)";
                    Button_Update.Foreground = new SolidColorBrush(Colors.GreenYellow);
                    _updateAvailable = false;
                }

                Button_Update.ToolTip += $"\n\nChangelog: {serverUpdateInfo.Changelog}\nType: {serverUpdateInfo.Type}";
                // Catch if the client.DownloadString failed, maybe the link changed, the server is down or the client is offline
            }
            catch (Exception ex)
            {
                // PrintToLog("{UpdateHeaderVersion} EXCEPTION: " + ex.Message);
                Debug.WriteLine(ex.Message);
                Button_Update.Foreground = new SolidColorBrush(Colors.Red);
                Button_Update.Content = "You're offline!";
                Button_Update.ToolTip = $"You are on version ({clientVersion[0]}.{clientVersion[1]}.{clientVersion[2]}.{clientVersion[3]})";
                _updateAvailable = false;
            }

            return _updateAvailable;
        }
        /// <summary>
        /// This method continuesly runs and makes sure the game is running
        /// </summary>
        private void IsGameRunning()
        {
            // PrintToLog("{IsGameRunning}");
            try
            {
                _activeScreen = WindowFinder.FindApplication("cs2");

                if (_activeScreen != null)
                {
                    // PrintToLog("{IsGameRunning} Game is running");
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        string input = _activeScreen.DeviceName;
                        int lastBackslashIndex = input.LastIndexOf('\\');
                        string extractedString = input.Substring(lastBackslashIndex + 1).TrimStart('.');
                        string formattedString = extractedString.Insert(7, " ");

                        // Append the appropriate strings
                        TextBlock_Monitor.Foreground = new SolidColorBrush(Colors.GhostWhite);
                        Program_state.IsEnabled = true;
                        Program_state_continuously.IsEnabled = true;
                        TextBlock_Monitor.Text = $"CS2 is running on: {formattedString}";
                        TextBlock_MonitorSize.Text = $"Display Size: {_activeScreen.Bounds.Width}x{_activeScreen.Bounds.Height} ({AspectRatio()})";
                        Button_LaunchCS.Visibility = Visibility.Collapsed;
                        Button_LaunchCS.Content = "Launch CS2";
                    }));

                    CalculateSizes(AspectRatio());
                }
                else
                {
                    // PrintToLog("{IsGameRunning} Game is not running");
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        TextBlock_Monitor.Foreground = new SolidColorBrush(Colors.Red);
                        TextBlock_Monitor.Text = "CS2 is not running, make sure the game is open!";
                        TextBlock_MonitorSize.Text = "";
                        Program_state.IsChecked = false;
                        Program_state.IsEnabled = false;
                        Program_state_continuously.IsChecked = false;
                        Program_state_continuously.IsEnabled = false;
                        Button_LaunchCS.Content = "Launch CS2";
                        Button_LaunchCS.Visibility = Visibility.Visible;
                    }));
                }
            }
            catch (Exception)
            {
                // PrintToLog("{IsGameRunning} EXCEPTION: " + ex.Message);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    TextBlock_Monitor.Foreground = new SolidColorBrush(Colors.Red);
                    TextBlock_Monitor.Text = "CS2 is not running, make sure the game is open!";
                    TextBlock_MonitorSize.Text = "";
                    Program_state.IsChecked = false;
                    Program_state.IsEnabled = false;
                    Program_state_continuously.IsChecked = false;
                    Program_state_continuously.IsEnabled = false;
                    Button_LaunchCS.Content = "Launch CS2";
                    Button_LaunchCS.Visibility = Visibility.Visible;
                }));
            }

            Thread.Sleep(5 * 1000);
            Thread.Sleep(_gameRunExtraDelay * 1000);
            _gameRunExtraDelay = 0; // Reset the delay, in case it was changed somewhere else
            IsGameRunning();
        }
        /// <summary>
        /// Gets the aspect ratio of a given display
        /// </summary>
        /// <param Name="x">Width</param>
        /// <param Name="y">Height</param>
        /// <returns>This method returns the aspect ratio</returns>
        private string AspectRatio()
        {
            // PrintToLog("{AspectRatio}");
            // double value = (double)_activeScreen!.Bounds.Width / _activeScreen.Bounds.Height;
            int x = _activeScreen!.Bounds.Width;
            int y = _activeScreen!.Bounds.Height;

            // We need to find Greatest Common Divisor, and divide both x and y by it.
            string aspectRatio = $"{x / GCD(x, y)}:{y / GCD(x, y)}";

            if (aspectRatio == "8:5")
            {
                // PrintToLog("{AspectRatio} 16:10");
                return "16:10";
            }

            // PrintToLog("{AspectRatio} " + aspectRatio);
            return aspectRatio;
        }
        /// <summary>
        /// Find the Greatest Common Divisor and return it 
        /// </summary>
        /// <param Name="a">a, here it's width</param>
        /// <param Name="b">b, here it's height</param>
        /// <returns>This method returns the Greatest Common Divisor</returns>
        private int GCD(int a, int b)
        {
            int Remainder;

            while (b != 0)
            {
                Remainder = a % b;
                a = b;
                b = Remainder;
            }

            return a;
        }
        /// <summary>
        /// Take a screen capture assuming the screen is 16:9
        /// </summary>
        /// <param Name="xwidth">Width in pixels</param>
        /// <param Name="xheight">Height in pixels</param>
        /// <param Name="xstartpos">X Starting position in pixels</param>
        /// <param Name="ystartpos">Y Starting position in pixels</param>
        /// <returns>This method returns a bitmap of the area</returns>
        private Bitmap CaptureScreen(int w, int h, int x = 0, int y = 0)
        {
            // PrintToLog("{CaptureScreen}");
            try
            {
                // Creating a new Bitmap object
                Bitmap captureBitmap = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                // Creating a Rectangle object which will capture our Screen
                Rectangle captureRectangle = _activeScreen!.Bounds;

                // Creating a New Graphics Object
                Graphics captureGraphics = Graphics.FromImage(captureBitmap);

                // Copying Image from The Screen
                captureGraphics.CopyFromScreen(x, y, 0, 0, captureRectangle.Size);

                // PrintToLog("{CaptureScreen} SUCCESS");
                return captureBitmap;
            }
            catch (Exception ex)
            {
                // PrintToLog("{CaptureScreen} FAILED: " + ex.Message);
                System.Windows.MessageBox.Show(ex.Message, "CS2 AutoAccept", MessageBoxButton.OK, MessageBoxImage.Error);
                return null!;
            }
        }
        /// <summary>
        /// Scanner thread method
        /// </summary>
        /// <param Name="obj">CancellationToken</param>
        private void Scanner(object? obj)
        {
            // PrintToLog("{Scanner Started}");
            if (obj is null)
                return;

            CancellationToken ct = (CancellationToken)obj;
            while (!ct.IsCancellationRequested)
            {

                // PrintToLog("{Scanner}");
                // Take a screenshot of the accept button
                Bitmap bitmap = CaptureScreen(_acceptWidth, _acceptHeight, _acceptPosX, _acceptPosY); // "Accept" button

                // Adjust the contrast, then sharpen the image
                bitmap = OptimiseImage(bitmap);

                // Read the image using OCR
                (string text, double confidence) valuePair = OCR(bitmap);

                Debug.WriteLine("OCR RESULTS:");
                Debug.WriteLine(valuePair.text);
                Debug.WriteLine(valuePair.confidence);

                // Check the returned value
                if (valuePair.text.ToLower().Contains("accept") && valuePair.confidence > .75)
                {
                    Debug.WriteLine("Accept conditions met");
                    // PrintToLog("{Scanner} Match found");
                    // Move the cursor and click the accept button

                    System.Windows.Forms.Cursor.Position = new System.Drawing.Point(_clickPosX, _clickPosY);

                    uint X = (uint)System.Windows.Forms.Cursor.Position.X;
                    uint Y = (uint)System.Windows.Forms.Cursor.Position.Y;

                    mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, X, Y, 0, 0);

                    // PrintToLog("{Scanner} Match accpeted");

                    // Wait 30 seconds, to see if everyone accepted the match
                    Thread.Sleep(30 * 1000);

                    bitmap = CaptureScreen(_cancelWidth, _cancelHeight, _cancelPosX, _cancelPosY); // "Cancel Search" button

                    // Adjust the contrast, then sharpen the image
                    bitmap = OptimiseImage(bitmap);

                    // Read the image using OCR
                    valuePair = OCR(bitmap);
                    // Check the returned value
                    if (!(valuePair.text.ToLower().Contains("cancel search") && valuePair.confidence > .75) && !_run_Continuously)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            Program_state.IsChecked = false;
                        }));

                        return;
                    }

                    // PrintToLog("{Scanner} Match cancelled");
                }

                Thread.Sleep(1000);
            }
        }
        /// <summary>
        /// Calculate the positions based on display Size
        /// </summary>
        /// <param Name="type">Aspect Ratio</param>
        private void CalculateSizes(string type)
        {
            // Base settings for 2560x1440
            int acceptPosX = 1164;
            int acceptPosY = 546;
            int acceptWidth = 227;
            int acceptHeight = 105;
            int cancelPosX = 2050;
            int cancelPosY = 1347;
            int cancelWidth = 400;
            int cancelHeight = 60;
            int baseWidth = 2560;
            int baseHeight = 1440;

            // PrintToLog("{CalculateSizes}");
            switch (type)
            {
                case "16:9":
                    // Convert back to pixels for the specific display
                    _acceptPosX = (int)(acceptPosX * (_activeScreen!.Bounds.Width / (float)baseWidth));
                    _acceptPosY = (int)(acceptPosY * (_activeScreen.Bounds.Height / (float)baseHeight));

                    _acceptWidth = (int)(acceptWidth * (_activeScreen.Bounds.Width / (float)baseWidth));
                    _acceptHeight = (int)(acceptHeight * (_activeScreen.Bounds.Height / (float)baseHeight));

                    _cancelPosX = (int)(cancelPosX * (_activeScreen.Bounds.Width / (float)baseWidth));
                    _cancelPosY = (int)(cancelPosY * (_activeScreen.Bounds.Height / (float)baseHeight));

                    _cancelWidth = (int)(cancelWidth * (_activeScreen.Bounds.Width / (float)baseWidth));
                    _cancelHeight = (int)(cancelHeight * (_activeScreen.Bounds.Height / (float)baseHeight));

                    _clickPosX = _acceptPosX + (_acceptWidth / 2);
                    _clickPosY = _acceptPosY + (_acceptHeight / 2);
                    break;
                case "16:10":
                    // Base settings for 1920x1200
                    acceptPosX = 834;
                    acceptPosY = 447;
                    acceptWidth = 251;
                    acceptHeight = 99;
                    cancelPosX = 1454;
                    cancelPosY = 1123;
                    cancelWidth = 321;
                    cancelHeight = 49;
                    baseWidth = 1920;
                    baseHeight = 1200;

                    // Convert back to pixels for the specific display
                    _acceptPosX = (int)(acceptPosX * (_activeScreen!.Bounds.Width / (float)baseWidth));
                    _acceptPosY = (int)(acceptPosY * (_activeScreen.Bounds.Height / (float)baseHeight));

                    _acceptWidth = (int)(acceptWidth * (_activeScreen.Bounds.Width / (float)baseWidth));
                    _acceptHeight = (int)(acceptHeight * (_activeScreen.Bounds.Height / (float)baseHeight));

                    _cancelPosX = (int)(cancelPosX * (_activeScreen.Bounds.Width / (float)baseWidth));
                    _cancelPosY = (int)(cancelPosY * (_activeScreen.Bounds.Height / (float)baseHeight));

                    _cancelWidth = (int)(cancelWidth * (_activeScreen.Bounds.Width / (float)baseWidth));
                    _cancelHeight = (int)(cancelHeight * (_activeScreen.Bounds.Height / (float)baseHeight));

                    _clickPosX = _acceptPosX + (_acceptWidth / 2);
                    _clickPosY = _acceptPosY + (_acceptHeight / 2);
                    break;
                case "4:3":
                    // Base settings for 1440x1080
                    acceptPosX = 608;
                    acceptPosY = 404;
                    acceptWidth = 225;
                    acceptHeight = 88;
                    cancelPosX = 1018;
                    cancelPosY = 1011;
                    cancelWidth = 293;
                    cancelHeight = 44;
                    baseWidth = 1440;
                    baseHeight = 1080;

                    // Convert back to pixels for the specific display
                    _acceptPosX = (int)(acceptPosX * (_activeScreen!.Bounds.Width / (float)baseWidth));
                    _acceptPosY = (int)(acceptPosY * (_activeScreen.Bounds.Height / (float)baseHeight));

                    _acceptWidth = (int)(acceptWidth * (_activeScreen.Bounds.Width / (float)baseWidth));
                    _acceptHeight = (int)(acceptHeight * (_activeScreen.Bounds.Height / (float)baseHeight));

                    _cancelPosX = (int)(cancelPosX * (_activeScreen.Bounds.Width / (float)baseWidth));
                    _cancelPosY = (int)(cancelPosY * (_activeScreen.Bounds.Height / (float)baseHeight));

                    _cancelWidth = (int)(cancelWidth * (_activeScreen.Bounds.Width / (float)baseWidth));
                    _cancelHeight = (int)(cancelHeight * (_activeScreen.Bounds.Height / (float)baseHeight));

                    _clickPosX = _acceptPosX + (_acceptWidth / 2);
                    _clickPosY = _acceptPosY + (_acceptHeight / 2);
                    break;
            }

            _acceptPosX += _activeScreen!.Bounds.Left;
            _acceptPosY += _activeScreen!.Bounds.Top;
            _cancelPosX += _activeScreen!.Bounds.Left;
            _cancelPosY += _activeScreen!.Bounds.Top;
            _clickPosX += _activeScreen!.Bounds.Left;
            _clickPosY += _activeScreen!.Bounds.Top;

            // PrintToLog("{CalculateSizes} _acceptPosX: " + _acceptPosX);
            // PrintToLog("{CalculateSizes} _acceptPosY: " + _acceptPosY);
            // PrintToLog("{CalculateSizes} _acceptWidth: " + _acceptWidth);
            // PrintToLog("{CalculateSizes} _acceptHeight: " + _acceptHeight);
            // PrintToLog("{CalculateSizes} _cancelPosX: " + _cancelPosX);
            // PrintToLog("{CalculateSizes} _cancelPosY: " + _cancelPosY);
            // PrintToLog("{CalculateSizes} _cancelWidth: " + _cancelWidth);
            // PrintToLog("{CalculateSizes} _cancelHeight: " + _cancelHeight);
            // PrintToLog("{CalculateSizes} _clickPosX: " + _clickPosX);
            // PrintToLog("{CalculateSizes} _clickPosY: " + _clickPosY);
            // PrintToLog("{CalculateSizes} SUCCESS");
        }
        /// <summary>
        /// Optimise an image for OCR
        /// </summary>
        /// <param Name="bitmap">The image to optimise</param>
        /// <returns>This method returns a bitmap, optimised for OCR</returns>
        private Bitmap OptimiseImage(Bitmap bitmap)
        {
            // PrintToLog("{OptimiseImage}");
            // Adjust the contrast, then sharpen the image
            bitmap = ImageManipulator.Resize(bitmap, bitmap.Width * 2, bitmap.Height * 2);

            // PrintToLog("{OptimiseImage} SUCCESS");
            return bitmap;
        }
        /// <summary>
        /// Perform OCR on an Image
        /// </summary>
        /// <param Name="bitmap">The image</param>
        /// <returns>This method returns the text, and the confidence</returns>
        private (string, float) OCR(Bitmap bitmap)
        {
            // PrintToLog("{OCR}");
            try
            {
                using (TesseractEngine engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
                {
                    using (Pix img = Pix.LoadFromMemory(ImageToByte(bitmap)))
                    {
                        using (Page page = engine.Process(img))
                        {
                            string text = page.GetText();
                            float confidence = page.GetMeanConfidence();

                            // PrintToLog("{OCR} text: " + text);
                            // PrintToLog("{OCR} confidence: " + confidence);
                            // PrintToLog("{OCR} SUCCESS");
                            return (text, confidence);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // PrintToLog("{OCR} " + ex.Message);
                if (ex.Message.ToLower().Contains("failed to initialise tesseract engine"))
                {
                    Process.Start(Path.Combine(_basePath, "CS2-AutoAccept"));
                    Environment.Exit(0);
                    return ("", 100);
                }

                return ("", 100);
            }
        }
        /// <summary>
        /// Converts an image to a Byte[]
        /// </summary>
        /// <param Name="img">Image/Bitmap</param>
        /// <returns>This method returns a Byte[] containing the Image</returns>
        private byte[] ImageToByte(Image img)
        {
            // PrintToLog("{ImageToByte}");
            using (MemoryStream stream = new MemoryStream())
            {
                img.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                // PrintToLog("{ImageToByte} SUCCESS");
                return stream.ToArray();
            }
        }
        /// <summary>
        /// Delete all files and folders except one
        /// </summary>
        /// <param name="directoryPath">Directory To Clear</param>
        /// <param name="folderToKeep">Folder to keep</param>
        private void DeleteAllExceptFolder(string directoryPath, string folderToKeep)
        {
            foreach (string directory in Directory.GetDirectories(directoryPath))
            {
                if (Path.GetFileName(directory) != folderToKeep)
                {
                    try
                    {
                        Directory.Delete(directory, true);
                    }
                    catch (Exception)
                    { }
                }
            }

            foreach (string file in Directory.GetFiles(directoryPath))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception)
                { }
            }

            // Recursively process subdirectories
            foreach (string subdirectory in Directory.GetDirectories(directoryPath))
            {
                if (Path.GetFileName(subdirectory) != folderToKeep)
                {
                    DeleteAllExceptFolder(subdirectory, folderToKeep);
                }
            }
        }
        /// <summary>
        /// Prints to the log
        /// </summary>
        /// <param Name="log">Text to log</param>
        private async Task<bool> PrintToLog(string log)
        {
            try
            {
                string logLocation = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\CS2 AutoAccepter Log.txt";
                log = $"{DateTime.Now.ToString("[HH:mm:ss]")} {log}{Environment.NewLine}";
                await File.AppendAllTextAsync(logLocation, log);
            }
            catch (Exception)
            {
                try
                {
                    string logLocation = Environment.ExpandEnvironmentVariables("%userprofile%") + "\\onedrive\\Desktop\\CS2 AutoAccepter Log.txt";
                    log = $"{DateTime.Now.ToString("[HH:mm:ss]")} {log}{Environment.NewLine}";
                    await File.AppendAllTextAsync(logLocation, log);
                }
                catch (Exception)
                {
                    throw;
                }
            }

            return true;
        }
    }
}
