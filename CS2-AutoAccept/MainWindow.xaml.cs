using System;
using System.IO;
using Tesseract;
using System.Linq;
using CS2AutoAccept;
using System.Windows;
using System.Drawing;
using System.Net.Http;
using Microsoft.Win32;
using System.Text.Json;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Forms;
using System.Windows.Media;
using CS2_AutoAccept.Models;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using Microsoft.Toolkit.Uwp.Notifications;

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
        private bool _scannerIsActive = false;
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
        private readonly bool debugMode = false;
        public ICommand ToggleWindowCommand { get; }
        public ICommand CloseCommand { get; }
        private bool _isTrayIconVisible;

        public bool IsTrayIconVisible
        {
            get => _isTrayIconVisible;
            set
            {
                _isTrayIconVisible = value;
                UpdateTrayIconVisibility();
            }
        }
        public MainWindow()
        {
            InitializeComponent();

            // Subscribe to the event when another instance tries to start
            Task.Run(() => OnAnotherInstanceStarted());
            IsTrayIconVisible = true;
            DataContext = this;

            ToggleWindowCommand = new RelayCommand(o => ToggleWindowState());
            CloseCommand = new RelayCommand(o => CloseApplication());

            // Event handler for double-click on TaskbarIcon
            MyNotifyIcon.TrayMouseDoubleClick += OnTrayIconDoubleClick;

            _basePath = Path.Combine(Environment.ExpandEnvironmentVariables("%APPDATA%"), "CS2 AutoAccept");
            _updatePath = Path.Combine(_basePath, "UPDATE");

            Loaded += MainWindow_Loaded;

            RestoreSizeIfSaved();

            if (!debugMode)
            {
                ControlLocation();
            }

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
            if (File.Exists(Path.Combine(_basePath, "DELETE_ME.tsgs")))
            {
                string folderToDelete = File.ReadAllText(Path.Combine(_basePath, "DELETE_ME.tsgs"));
                try
                {
                    if (Directory.Exists(folderToDelete))
                    {
                        Directory.Delete(folderToDelete, true);
                    }

                    File.Delete(Path.Combine(_basePath, "DELETE_ME.tsgs"));
                }
                catch (Exception)
                { }
            }
            #endregion

            #region Update
            if (Directory.Exists(_updatePath) && !debugMode)
            {
                File.Copy(Path.Combine(_basePath, "settings.cs2_auto"), Path.Combine(_updatePath, "settings.cs2_auto"));

                string runPath = AppContext.BaseDirectory;

                if (runPath.LastIndexOf('\\') == runPath.Length - 1)
                {
                    runPath = runPath[..^1]; // Remove the last character
                }

                // If true, the exe was run from inside the UPDATE folder
                if (runPath == _updatePath)
                {
                    string[] updatedFiles = Directory.GetFiles(_updatePath, "*", SearchOption.TopDirectoryOnly);
                    string[] updatedDirectories = Directory.GetDirectories(_updatePath, "*", SearchOption.TopDirectoryOnly);

                    // Delete all the old files and folders
                    DeleteAllExceptFolder(_basePath, "UPDATE");

                    // Move all the new files and folders, to the basePath
                    foreach (string filePath in updatedFiles)
                    {
                        string fileName = filePath.Substring(_updatePath.Length + 1);
                        string destinationPath = Path.Combine(_basePath, fileName);

                        try
                        {
                            File.Move(filePath, destinationPath, true);
                            //Debug.WriteLine($"Copied: {fileName}");
                        }
                        catch (Exception)
                        {
                            //Debug.WriteLine($"Error copying {fileName}: {ex.Message}");
                        }
                    }

                    foreach (string directoryPath in updatedDirectories)
                    {
                        string directoryName = directoryPath.Substring(_updatePath.Length + 1);
                        string destinationPath = Path.Combine(_basePath, directoryName);

                        try
                        {
                            Directory.Move(directoryPath, destinationPath);
                            //Debug.WriteLine($"Moved: {directoryName}");
                        }
                        catch (Exception)
                        {
                            //Debug.WriteLine($"Error copying {directoryName}: {ex.Message}");
                        }
                    }

                    // Start the updated program, in the new default path
                    Process.Start(Path.Combine(_basePath, "CS2-AutoAccept"), "--updated");
                    Environment.Exit(0);
                }
                else
                {
                    // Try to delete the update folder, if not run from that path
                    try
                    {
                        Directory.Delete(_updatePath, true);
                    }
                    catch (Exception)
                    {
                        //Debug.WriteLine($"Failed to delete the update directoryPath: {ex.Message}");
                    }
                }
            }
            #endregion
        }
        private void ToggleWindowState()
        {
            if (WindowState == WindowState.Minimized)
            {
                Show();
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Minimized;
                Hide();
            }
        }

        private void CloseApplication()
        {
            Close();
        }
        private void UpdateTrayIconVisibility()
        {
            // Show or hide the tray icon based on IsTrayIconVisible
            MyNotifyIcon.Visibility = IsTrayIconVisible ? Visibility.Visible : Visibility.Collapsed;
        }
        #region EventHandlers
        // Handle the event when another instance tries to start
        private void OnAnotherInstanceStarted()
        {
            using (MemoryMappedFile mmf = MemoryMappedFile.CreateOrOpen("CS2_AutoAccept_MMF", 1024))
            using (MemoryMappedViewStream view = mmf.CreateViewStream())
            {
                BinaryReader reader = new BinaryReader(view);
                EventWaitHandle signal = new EventWaitHandle(false, EventResetMode.AutoReset, "CS2_AutoAccept_Event");
                Mutex mutex = new Mutex(false, "CS2-AutoAccept by tsgsOFFICIAL");

                while (true)
                {
                    signal.WaitOne();
                    mutex.WaitOne();
                    reader.BaseStream.Position = 0;
                    string message = reader.ReadString();

                    if (message == "New instance started")
                    {
                        Dispatcher.Invoke(() =>
                        {
                            // Bring the existing window to the front if it's minimized
                            if (WindowState == WindowState.Minimized)
                            {
                                Show();
                                WindowState = WindowState.Normal;
                            }
                        });
                    }

                    mutex.ReleaseMutex();
                }
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Access command line arguments
            string[] args = Environment.GetCommandLineArgs();

            foreach (string arg in args)
            {
                if (arg.ToLower().Equals("start cs2"))
                {
                    LaunchWeb("steam://rungameid/730");
                    Button_LaunchCS.Content = "Launching CS2";
                    _gameRunExtraDelay = 15;
                }

                // Application was started minimized
                if (arg.ToLower().Equals("--minimize"))
                {
                    WindowState = WindowState.Minimized;
                }

                // Application was updated
                if (arg.ToLower().Equals("--updated"))
                {
                    // Try to delete the update folder
                    try
                    {
                        Directory.Delete(_updatePath, true);
                    }
                    catch (Exception)
                    { }

                    ShowNotification("CS2 AutoAccept", "CS2 AutoAccept has been updated!");
                }
            }
        }
        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            // Find context menu items and update texts based on WindowState
            var menu = (System.Windows.Controls.ContextMenu)MyNotifyIcon.ContextMenu;
            var toggleMenuItem = (System.Windows.Controls.MenuItem)menu.Items[0];

            if (WindowState == WindowState.Minimized)
            {
                string toolTipText = _scannerIsActive == true ? "AutoAccept is running in the background" : "CS2 AutoAccept is minimized";

                Hide();
                ShowNotification("CS2 AutoAccept", toolTipText);
                toggleMenuItem.Header = "Restore";
                MyNotifyIcon.ToolTipText = toolTipText;
            }
            else
            {
                toggleMenuItem.Header = "Minimize";
                MyNotifyIcon.ToolTipText = "CS2 AutoAccept by tsgsOFFICIAL";
            }
        }
        private void OnTrayIconDoubleClick(object sender, RoutedEventArgs e)
        {
            // Show the window and restore it to normal state
            Show();
            WindowState = WindowState.Normal;
        }
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
                        Button_LaunchCS.Visibility = Visibility.Visible;

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
        /// Maximize the window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Button_Click_Maximize(object sender, RoutedEventArgs e)
        {
            if (WindowState.Equals(WindowState.Maximized))
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }
        /// <summary>
        /// Close button
        /// </summary>
        /// <param Name="sender"></param>
        /// <param Name="e"></param>
        private void Button_Click_Close(object sender, RoutedEventArgs e)
        {
            Close();
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
                Button_LaunchCS.Visibility = Visibility.Collapsed;
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
            _scannerIsActive = true;
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
            _scannerIsActive = false;

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
            string exePath = GetExePath();

            try
            {
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");

                if (key.GetValue("CS2-AutoAccept") == null)
                {
                    key.SetValue("CS2-AutoAccept", $"\"{exePath}\" --minimize");
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
        /// <summary>
        /// Run when window size is changed
        /// </summary>
        private void WindowSizeChangedEventHandler(object sender, SizeChangedEventArgs e)
        {
            double newWidth = e.NewSize.Width;
            double newHeight = e.NewSize.Height;

            SettingsModel userSettings = new SettingsModel(newWidth, newHeight);

            JsonSerializerOptions jsonOptions = new JsonSerializerOptions()
            {
                WriteIndented = true,
            };

            string jsonString = JsonSerializer.Serialize(userSettings, jsonOptions);

            File.WriteAllText(Path.Combine(_basePath, "settings.cs2_auto"), jsonString);
        }
        #endregion
        private static string GetExePath()
        {
            string? exeLocation5 = Process.GetCurrentProcess().MainModule?.FileName;

            string executingDir = AppDomain.CurrentDomain.BaseDirectory;
            string executingName = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);

            return exeLocation5 ?? $"{Path.Combine(executingDir, executingName)}.exe";
        }
        /// <summary>
        /// Restore the window size, if it was previously opened & changed
        /// </summary>
        private void RestoreSizeIfSaved()
        {
            // Get a reference to the current WPF application
            System.Windows.Application wpfApp = System.Windows.Application.Current;

            // Access the main window of the application (assuming it's of type Window)
            Window mainWindow = wpfApp.MainWindow;

            try
            {
                string jsonString = File.ReadAllText(Path.Combine(_basePath, "settings.cs2_auto"));
                SettingsModel? userSettings = JsonSerializer.Deserialize<SettingsModel>(jsonString) ?? new SettingsModel();

                if (userSettings.WindowWidth != null && userSettings.WindowHeight != null)
                {
                    // Set the size of the main window
                    mainWindow.Width = (double)userSettings.WindowWidth;
                    mainWindow.Height = (double)userSettings.WindowHeight;
                }
            }
            catch (Exception)
            {
            }

            mainWindow.SizeChanged += WindowSizeChangedEventHandler;
        }

        /// <summary>
        /// Ensures the application is running from the correct folder. If not, it moves the necessary files and folders
        /// to the correct base path and restarts the application from there.
        /// </summary>
        private void ControlLocation()
        {
            // Was the program ran from the correct folder (_basePath or _updatePath)?
            string runPath = AppContext.BaseDirectory;

            if (runPath.LastIndexOf('\\') == runPath.Length - 1)
            {
                runPath = runPath[..^1]; // Remove the last character
            }

            if (runPath != _basePath && runPath != _updatePath)
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
                        //Debug.WriteLine($"Copied: {fileName}");
                    }
                    catch (Exception)
                    {
                        //Debug.WriteLine($"Error copying {fileName}: {ex.Message}");
                    }
                }

                foreach (string directoryPath in folders)
                {
                    string directoryName = directoryPath.Substring(runPath.Length + 1);
                    string destinationPath = Path.Combine(_basePath, directoryName);

                    try
                    {
                        Directory.Move(directoryPath, destinationPath);
                        //Debug.WriteLine($"Moved: {directoryName}");
                    }
                    catch (Exception)
                    {
                        //Debug.WriteLine($"Error copying {directoryName}: {ex.Message}");
                    }
                }

                // Start the updated program, in the new default path
                Process.Start(Path.Combine(_basePath, "CS2-AutoAccept"), "--restarted");
                Environment.Exit(0);
            }
        }
        /// <summary>
        /// Launch a web URL on Windows, Linux and OSX
        /// </summary>
        /// <param Name="url">The URL to open in the standard browser</param>
        private static void LaunchWeb(string url)
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
                    serverVersion = serverUpdateInfo.Version!.Split(".").Select(int.Parse).ToList();
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
            catch (Exception)
            {
                // PrintToLog("{UpdateHeaderVersion} EXCEPTION: " + ex.Message);
                //Debug.WriteLine(ex.Message);
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
        private static int GCD(int a, int b)
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
        private static System.Drawing.Color GetMostFrequentColor(Bitmap bitmap)
        {
            Dictionary<System.Drawing.Color, int> colorFrequency = new Dictionary<System.Drawing.Color, int>();

            for (int x = 0; x < bitmap.Width; x++)
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    System.Drawing.Color pixelColor = bitmap.GetPixel(x, y);

                    // If color is already in the dictionary, increment the count
                    if (colorFrequency.ContainsKey(pixelColor))
                    {
                        colorFrequency[pixelColor]++;
                    }
                    else
                    {
                        colorFrequency[pixelColor] = 1;
                    }
                }
            }

            // Find the color with the highest frequency
            System.Drawing.Color mostFrequentColor = System.Drawing.Color.Empty;
            int maxFrequency = 0;

            foreach (KeyValuePair<System.Drawing.Color, int> pair in colorFrequency)
            {
                if (pair.Value > maxFrequency)
                {
                    maxFrequency = pair.Value;
                    mostFrequentColor = pair.Key;
                }
            }

            return mostFrequentColor;
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
                Rectangle captureRectangle = _activeScreen?.Bounds ?? new Rectangle(0, 0, w, h);

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

            double confidenceThreshold = 0.5;
            System.Drawing.Color targetColor = System.Drawing.Color.FromArgb(255, 54, 183, 82);

            CancellationToken ct = (CancellationToken)obj;
            while (!ct.IsCancellationRequested)
            {
                // PrintToLog("{Scanner}");
                // Take a screenshot of the accept button
                Bitmap bitmap = CaptureScreen(_acceptWidth, _acceptHeight, _acceptPosX, _acceptPosY); // "Accept" button

                Bitmap greyBitmap = OptimiseImage(bitmap);

                // Read the image using OCR
                (string text, double confidence) valuePair = OCR(greyBitmap);

                // Control OCR output
                if (valuePair.text.ToLower().Contains("accept"))
                {
                    System.Drawing.Color mostFrequentColor = GetMostFrequentColor(bitmap); // Get the most frequent color in the original image

                    if (valuePair.confidence > confidenceThreshold || targetColor == mostFrequentColor)
                    {
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
                        greyBitmap = OptimiseImage(bitmap);

                        // Read the image using OCR
                        valuePair = OCR(greyBitmap);

                        // Check the returned value
                        bool condition = !(valuePair.text.ToLower().Contains("cancel search") && valuePair.confidence > confidenceThreshold)
                            && !_run_Continuously;

                        if (condition)
                        {
                            //Debug.WriteLine("Match was initiated");
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                Program_state.IsChecked = false;
                            }));

                            return;
                        }
                        else if (valuePair.text.ToLower().Contains("go") && valuePair.confidence > confidenceThreshold)
                        {
                            //Debug.WriteLine("Teammates failed to accept, pressing go again");

                            int clickPosX = _cancelPosX + (_cancelWidth / 2);
                            int clickPosY = _cancelPosY + (_cancelHeight / 2);

                            System.Windows.Forms.Cursor.Position = new System.Drawing.Point(clickPosX, clickPosY);

                            X = (uint)System.Windows.Forms.Cursor.Position.X;
                            Y = (uint)System.Windows.Forms.Cursor.Position.Y;

                            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, X, Y, 0, 0);

                            // Wait 5 seconds, to not waste cpu power
                            Thread.Sleep(5 * 1000);
                        }
                        else
                        {
                            // Take a screenshot of the accept button
                            bitmap = CaptureScreen(_acceptWidth, _acceptHeight, _acceptPosX, _acceptPosY); // "Accept" button
                            greyBitmap = OptimiseImage(bitmap);

                            // Read the image using OCR
                            valuePair = OCR(greyBitmap);

                            // Check the returned value
                            if (valuePair.text.ToLower().Contains("accept"))
                            {
                                mostFrequentColor = GetMostFrequentColor(bitmap); // Get the most frequent color in the original image
                                
                                if (valuePair.confidence > confidenceThreshold || targetColor == mostFrequentColor)
                                {
                                    //Debug.WriteLine("Accept conditions met");

                                    // Move the cursor and click the accept button
                                    System.Windows.Forms.Cursor.Position = new System.Drawing.Point(_clickPosX, _clickPosY);

                                    X = (uint)System.Windows.Forms.Cursor.Position.X;
                                    Y = (uint)System.Windows.Forms.Cursor.Position.Y;

                                    mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, X, Y, 0, 0);
                                }
                            }
                        }
                    }
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
        private static Bitmap OptimiseImage(Bitmap bitmap)
        {
            // PrintToLog("{OptimiseImage}");
            bitmap = ImageManipulator.SetGrayscale(bitmap);

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
                    Process.Start(Path.Combine(_basePath, "CS2-AutoAccept"), "--restart");
                    ShowNotification("CS2 AutoAccept", "Tesseract failed to initialise, restarting the application.. " + ex.Message);
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
        private static byte[] ImageToByte(Image img)
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
        private static void DeleteAllExceptFolder(string directoryPath, string folderToKeep)
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
        private static async Task<bool> PrintToLog(string log)
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
        private static void ShowNotification(string title, string message)
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show(toast =>
                {
                    toast.ExpirationTime = DateTime.Now.AddSeconds(1);
                });
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object parameter) => _execute(parameter);

        public event EventHandler CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}