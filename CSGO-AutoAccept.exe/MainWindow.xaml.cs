using System;
using System.IO;
using Tesseract;
using System.Windows;
using System.Drawing;
using System.Net.Http;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Forms;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Controls;

namespace CSGO_AutoAccept
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
        private Screen? _activeScreen;
        private Thread? _scannerThread;
        private CancellationTokenSource? cts;
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
        public MainWindow()
        {
            InitializeComponent();
            _ = UpdateHeaderVersion();
            Thread GameRunningThread = new Thread(IsGameRunning);
            GameRunningThread.Start();
            GameRunningThread.IsBackground = true;
        }
        #region EventHandlers
        /// <summary>
        /// Minimize button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_Minimize(object sender, RoutedEventArgs e)
        {
            // PrintToLog("{Button_Click_Close}");
            WindowState = WindowState.Minimized;
        }
        /// <summary>
        /// Close button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_Close(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        /// <summary>
        /// Open Github to download the newest version
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Button_Update_Click(object sender, RoutedEventArgs e)
        {
            // PrintToLog("{Button_Update_Click}");
            bool UpdateAvailable = await UpdateHeaderVersion();

            if (UpdateAvailable)
            {
                Process.Start("https://download-directory.github.io/?url=https%3A%2F%2Fgithub.com%2FtsgsOFFICIAL%2FCSGO-AutoAccept.exe%2Ftree%2Fmain%2FCSGO-AutoAccept.exe%2Fbin%2FRelease%2Fnet6.0-windows%2Fpublish%2Fwin-x86");
            }
        }
        /// <summary>
        /// Open Discord
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_Discord(object sender, RoutedEventArgs e)
        {
            // PrintToLog("{Button_Click_Discord}");
            LaunchWeb(@"https://discord.gg/Cddu5aJ");
        }
        /// <summary>
        /// Launch CS:GO
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_LaunchCSGO(object sender, RoutedEventArgs e)
        {
            // PrintToLog("{Button_Click_LaunchCSGO}");
            LaunchWeb("steam://rungameid/730");
            Button_LaunchCSGO.Visibility = Visibility.Collapsed;
        }
        /// <summary>
        /// Drag header
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WindowHeader_Mousedown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
        /// <summary>
        /// State ON event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Program_state_Unchecked(object sender, RoutedEventArgs e)
        {
            // PrintToLog("{Program_state_Unchecked}");
            // Change to a darker color
            cts!.Cancel();
            Program_state.Foreground = new SolidColorBrush(Colors.Red);
            Program_state.Content = "AutoAccept (OFF)";
        }
        #endregion
        /// <summary>
        /// Launch a web URL on Windows, Linux and OSX
        /// </summary>
        /// <param name="url">The URL to open in the standard browser</param>
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
            // PrintToLog("{UpdateHeaderVersion}");
            // Create a new instance of WebClient, and search for the current version
            HttpClient client = new HttpClient();
            List<int> _serverVersion = new List<int>();
            int[] _clientVersion = new int[4];
            string[] version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion!.Split('.');
            string serverVersion = await client.GetStringAsync("https://raw.githubusercontent.com/tsgsOFFICIAL/CSGO-AutoAccept.exe/main/CSGO-AutoAccept.exe/version.txt");

            for (int i = 0; i < version.Length; i++)
            {
                _clientVersion[i] = int.Parse(version[i]);
            }

            try
            {
                // Convert the array to a list
                foreach (string onlineVersion in serverVersion.Split('.'))
                {
                    _serverVersion.Add(Convert.ToInt32(onlineVersion));
                }

                // Check if the update is newer
                if ((_clientVersion[0] < _serverVersion[0]) || (_clientVersion[1] < _serverVersion[1] && _clientVersion[0] <= _serverVersion[0]) || (_clientVersion[2] < _serverVersion[2] && _clientVersion[1] <= _serverVersion[1] && _clientVersion[0] <= _serverVersion[0]) || (_clientVersion[3] < _serverVersion[3] && _clientVersion[2] <= _serverVersion[2] && _clientVersion[1] <= _serverVersion[1] && _clientVersion[0] <= _serverVersion[0]))
                {
                    // PrintToLog("{UpdateHeaderVersion} Update available");
                    Button_Update.Content = "Update Now";
                    Button_Update.ToolTip = $"Version {_serverVersion[0]}.{_serverVersion[1]}.{_serverVersion[2]}.{_serverVersion[3]} is now available!\nYou're on version {_clientVersion[0]}.{_clientVersion[1]}.{_clientVersion[2]}.{_clientVersion[3]}\nClick to update now";
                    Button_Update.Foreground = new SolidColorBrush(Colors.Orange);
                    return true;
                }

                // Check if the user is on a newer build than the server
                if ((_clientVersion[0] > _serverVersion[0]) || (_clientVersion[1] > _serverVersion[1] && _clientVersion[0] >= _serverVersion[0]) || (_clientVersion[2] > _serverVersion[2] && _clientVersion[1] >= _serverVersion[1] && _clientVersion[0] >= _serverVersion[0]) || (_clientVersion[3] > _serverVersion[3] && _clientVersion[2] >= _serverVersion[2] && _clientVersion[1] >= _serverVersion[1] && _clientVersion[0] >= _serverVersion[0]))
                {
                    // PrintToLog("{UpdateHeaderVersion} You're on a dev build");
                    Button_Update.Content = "You're on a dev build";
                    Button_Update.ToolTip = $"Woooo! Look at you, you're on a dev build, version: {_clientVersion[0]}.{_clientVersion[1]}.{_clientVersion[2]}.{_clientVersion[3]}\nBe careful, Dev builds don't tend to be as stable.. ;)";
                    Button_Update.Foreground = new SolidColorBrush(Colors.GreenYellow);
                    return false;
                }
                // PrintToLog("{UpdateHeaderVersion} You are up-to-date!");
                Button_Update.Content = "You are up-to-date!";
                Button_Update.ToolTip = $"You are on the newest version ({_clientVersion[0]}.{_clientVersion[1]}.{_clientVersion[2]}.{_clientVersion[3]})\nYou can click at anytime to check again";
                Button_Update.Foreground = new SolidColorBrush(Colors.LawnGreen);
                return false;
                // Catch if the client.DownloadString failed, maybe the link changed, the server is down or the client is offline
            }
            catch (Exception)
            {
                // PrintToLog("{UpdateHeaderVersion} EXCEPTION: " + ex.Message);
                Button_Update.Foreground = new SolidColorBrush(Colors.Red);
                Button_Update.Content = "Could not establish a connection";
                Button_Update.ToolTip = $"You are on version ({_clientVersion[0]}.{_clientVersion[1]}.{_clientVersion[2]}.{_clientVersion[3]})";
                return false;
            }
        }
        /// <summary>
        /// This method continuesly runs and makes sure the game is running
        /// </summary>
        private void IsGameRunning()
        {
            // PrintToLog("{IsGameRunning}");
            try
            {
                _activeScreen = WindowFinder.FindApplication("csgo");

                if (_activeScreen != null)
                {
                    // PrintToLog("{IsGameRunning} Game is running");
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        string input = _activeScreen.DeviceName;
                        int lastBackslashIndex = input.LastIndexOf('\\');
                        string extractedString = input.Substring(lastBackslashIndex + 1).TrimStart('.');
                        string formattedString = extractedString.Insert(7, " ");

                        // Append the appropriate strings
                        TextBlock_Monitor.Foreground = new SolidColorBrush(Colors.GhostWhite);
                        Program_state.IsEnabled = true;
                        TextBlock_Monitor.Text = $"CS:GO is running on: {formattedString}";
                        TextBlock_MonitorSize.Text = $"Display size: {_activeScreen.Bounds.Width}x{_activeScreen.Bounds.Height} ({AspectRatio()})";
                        Button_LaunchCSGO.Visibility = Visibility.Collapsed;
                    }));

                    CalculateSizes();
                }
                else
                {
                    // PrintToLog("{IsGameRunning} Game is not running");
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        TextBlock_Monitor.Foreground = new SolidColorBrush(Colors.Red);
                        TextBlock_Monitor.Text = "CS:GO is not running, make sure the game is open!";
                        TextBlock_MonitorSize.Text = "";
                        Program_state.IsChecked = false;
                        Program_state.IsEnabled = false;
                        Button_LaunchCSGO.Visibility = Visibility.Visible;
                    }));
                }
            }
            catch (Exception)
            {
                // PrintToLog("{IsGameRunning} EXCEPTION: " + ex.Message);
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    TextBlock_Monitor.Foreground = new SolidColorBrush(Colors.Red);
                    TextBlock_Monitor.Text = "CS:GO is not running, make sure the game is open!";
                    TextBlock_MonitorSize.Text = "";
                    Program_state.IsChecked = false;
                    Program_state.IsEnabled = false;
                    Button_LaunchCSGO.Visibility = Visibility.Visible;
                }));
            }

            Thread.Sleep(5 * 1000);
            IsGameRunning();
        }
        /// <summary>
        /// Gets the aspect ratio of a given display
        /// </summary>
        /// <param name="x">Width</param>
        /// <param name="y">Height</param>
        /// <returns>This method returns eitehr 16:9 or 4:3</returns>
        private string AspectRatio()
        {
            // PrintToLog("{AspectRatio}");
            double value = (double)_activeScreen!.Bounds.Width / _activeScreen.Bounds.Height;
            if (value > 1.7)
            {
                // PrintToLog("{AspectRatio} 16:9");
                return "16:9";
            }
            else
            {
                // PrintToLog("{AspectRatio} 4:3");
                return "4:3";
            }
        }
        /// <summary>
        /// Take a screen capture assuming the screen is 16:9
        /// </summary>
        /// <param name="xwidth">Width in pixels</param>
        /// <param name="xheight">Height in pixels</param>
        /// <param name="xstartpos">X Starting position in pixels</param>
        /// <param name="ystartpos">Y Starting position in pixels</param>
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
                System.Windows.MessageBox.Show(ex.Message, "CS:GO AutoAccepter", MessageBoxButton.OK, MessageBoxImage.Error);
                return null!;
            }
        }
        /// <summary>
        /// Scanner thread method
        /// </summary>
        /// <param name="obj">CancellationToken</param>
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

                // Check the returned value
                if (valuePair.text.ToLower().Contains("accept") && valuePair.confidence > .9)
                {
                    // PrintToLog("{Scanner} Match found");
                    // Move the cursor and click the accept button

                    for (int i = 0; i < 10; i++)
                    {
                        if (i % 2 == 0)
                        {
                            System.Windows.Forms.Cursor.Position = new System.Drawing.Point(_clickPosX + 5 * i, _clickPosY + 5 * i);
                        }
                        else
                        {
                            System.Windows.Forms.Cursor.Position = new System.Drawing.Point(_clickPosX - 5 * i, _clickPosY - 5 * i);
                        }

                        uint X = (uint)System.Windows.Forms.Cursor.Position.X;
                        uint Y = (uint)System.Windows.Forms.Cursor.Position.Y;

                        mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, X, Y, 0, 0);
                        Thread.Sleep(50);
                    }

                    // PrintToLog("{Scanner} Match accpeted");

                    // Wait 25 seconds, to see if everyone accepted the match
                    Thread.Sleep(25 * 1000);

                    bitmap = CaptureScreen(_cancelWidth, _cancelHeight, _cancelPosX, _cancelPosY); // "Cancel Search" button

                    // Adjust the contrast, then sharpen the image
                    bitmap = OptimiseImage(bitmap);

                    // Read the image using OCR
                    valuePair = OCR(bitmap);

                    // Check the returned value
                    if (!(valuePair.text.ToLower().Contains("cancel search") && valuePair.confidence > .9))
                    {
                        this.Dispatcher.BeginInvoke(new Action(() =>
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
        /// Calculate the positions based on display size
        /// </summary>
        private void CalculateSizes()
        {
            // PrintToLog("{CalculateSizes}");
            // Base settings for 1440p
            int acceptPosX = 1130;
            int acceptPosY = 540;
            int acceptWidth = 299;
            int acceptHeight = 116;
            int cancelPosX = 2001;
            int cancelPosY = 1347;
            int cancelWidth = 386;
            int cancelHeight = 60;

            // Convert back to pixels for the specific display
            _acceptPosX = (int)(acceptPosX * (_activeScreen!.Bounds.Width / (float)2560));
            _acceptPosY = (int)(acceptPosY * (_activeScreen.Bounds.Height / (float)1440));

            _acceptWidth = (int)(acceptWidth * (_activeScreen.Bounds.Width / (float)2560));
            _acceptHeight = (int)(acceptHeight * (_activeScreen.Bounds.Height / (float)1440));

            _cancelPosX = (int)(cancelPosX * (_activeScreen.Bounds.Width / (float)2560));
            _cancelPosY = (int)(cancelPosY * (_activeScreen.Bounds.Height / (float)1440));

            _cancelWidth = (int)(cancelWidth * (_activeScreen.Bounds.Width / (float)2560));
            _cancelHeight = (int)(cancelHeight * (_activeScreen.Bounds.Height / (float)1440));

            _clickPosX = _acceptPosX + (_acceptWidth / 2);
            _clickPosY = _acceptPosY + (_acceptHeight / 2);

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
        /// <param name="bitmap">The image to optimise</param>
        /// <returns>This method returns a bitmap, optimised for OCR</returns>
        private Bitmap OptimiseImage(Bitmap bitmap)
        {
            // PrintToLog("{OptimiseImage}");
            // Adjust the contrast, then sharpen the image
            bitmap = ImageManipulator.AdjustContrast(bitmap, 100);
            bitmap = ImageManipulator.Sharpen(bitmap);

            // PrintToLog("{OptimiseImage} SUCCESS");
            return bitmap;
        }
        /// <summary>
        /// Perform OCR on an Image
        /// </summary>
        /// <param name="bitmap">The image</param>
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
                        using (Tesseract.Page page = engine.Process(img))
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
            catch (Exception)
            {
                return ("", 100);
            }
        }
        /// <summary>
        /// Converts an image to a Byte[]
        /// </summary>
        /// <param name="img">Image/Bitmap</param>
        /// <returns>This method returns a Byte[] containing the Image</returns>
        private byte[] ImageToByte(System.Drawing.Image img)
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
        /// Prints to the log
        /// </summary>
        /// <param name="log">Text to log</param>
        private async Task<bool> PrintToLog(string log)
        {
            try
            {
                string logLocation = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\CSGO AutoAccepter Log.txt";
                log = $"{DateTime.Now.ToString("[HH:mm:ss]")} {log}{Environment.NewLine}";
                await File.AppendAllTextAsync(logLocation, log);
            }
            catch (Exception)
            {
                try
                {
                    string logLocation = Environment.ExpandEnvironmentVariables("%userprofile%") + "\\onedrive\\Desktop\\CSGO AutoAccepter Log.txt";
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