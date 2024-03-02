using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Update = MabiModManager.Update;


namespace MabiModManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // The URL for patching
        // By default patches are read in the format VERSION_to_VERSION+1.zip
        // Example: http://mabi.pro/patch/371_to_372.zip
        const string patchURL = "http://mabi.pro/patch/";

        // The URL holding patch information
        const string patchInfoURL = "https://mabi.pro/patch/p.txt";

        // The file name that the local client version is stored in
        // By default this file is stored in the same directory
        const string clientVerFile = "version.dat";

        // Holds the arguments for launching client.exe
        readonly string[] launchArgs = new string[3] { " code:1622 ver:200 logip:", " logport:11000 chatip:", " chatport:8002 setting:\"file://data/features.xml=Regular, Japan\"" };

        string[] logIp = { "funf.mabi.pro", "funf.mabi.pro", "drei.mabi.pro" };

        // Argument to look for when checking remote client version
        const string patchInfoArg = "main_version";

        // Default server version
        int mainVer = 350;

        // Default client version
        // 95 + 256 = 350?
        int clientVer = 350;
        byte[] rawClientVer = new byte[4] { 95, 1, 0, 0 };
        
        private readonly BackgroundWorker downloadWorker = new BackgroundWorker();

        public MainWindow()
        {
            downloadWorker.WorkerReportsProgress = true;
            downloadWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(downloadWorker_DoWork);
            downloadWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(downloadWorker_RunWorkerCompleted);

            // Uncomment if updating the patcher is needed
            // Update managerUpdate = new Update();

            InitializeComponent();
        }

        private void Browser_OnLoadCompleted(object sender, NavigatingCancelEventArgs e)
        {
            dynamic activeX = browser.GetType().InvokeMember("ActiveXInstance",
                   BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                   null, this.browser, new object[] { });

            activeX.Silent = true;
        }
        
        // Event for moving rectangle at top of window
        private void Rectangle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
        }

        // Event for closing window
        private void Label_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Environment.Exit(0);
        }

        private void Label_Loaded(object sender, RoutedEventArgs e)
        {
            Label lbl = sender as Label;
            if (lbl.IsManipulationEnabled)
            {
                lbl.Background = new SolidColorBrush(Color.FromRgb(158, 158, 158));
            }
            else
            {
                lbl.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
            }
        }

        private void Label_MouseLeftButtonDown_1(object sender, MouseButtonEventArgs e)
        {
            Label lbl = sender as Label;
            switch (lbl.Content)
            {
                case "Patcher":
                    lbl.Background = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                    lbl.IsManipulationEnabled = true;
                    PatcherGrid.Visibility = Visibility.Visible;
                    Options.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                    Options.IsManipulationEnabled = false;
                    OptionsGrid.Visibility = Visibility.Hidden;
                    break;
                case "Options":
                    lbl.Background = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                    lbl.IsManipulationEnabled = true;
                    OptionsGrid.Visibility = Visibility.Visible;
                    Patcher.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                    Patcher.IsManipulationEnabled = false;
                    PatcherGrid.Visibility = Visibility.Hidden;
                    break;
                default:
                    break;
            }
        }

        private void Label_MouseEnter(object sender, MouseEventArgs e)
        {
            Label lbl = sender as Label;
            if(lbl.IsManipulationEnabled)
            {
                lbl.Background = new SolidColorBrush(Color.FromRgb(100, 100, 100));
            }
            else
            {
                lbl.Background = new SolidColorBrush(Color.FromRgb(158, 158, 158));
            }
        }

        private void Label_MouseLeave(object sender, MouseEventArgs e)
        {
            Label lbl = sender as Label;
            if (lbl.IsManipulationEnabled)
            {
                lbl.Background = new SolidColorBrush(Color.FromRgb(158, 158, 158));
            }
            else
            {
                lbl.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
            }
        }

        private void Label_MouseLeftButtonDown_2(object sender, MouseButtonEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void StartButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("Client.exe", launchArgs[0] + logIp[LoginServer.SelectedIndex] + launchArgs[1] + logIp[LoginServer.SelectedIndex] + launchArgs[2]);
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to find Client.exe\n\nMake sure this program is in the your MabiPro folder", "Launch Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Environment.Exit(0);
        }
        
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        // ----------------------------------------------------------------------------
        // Patching Logic
        // ----------------------------------------------------------------------------

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Defining variables
            string html = string.Empty;
            // Get version
            try
            {
                rawClientVer = File.ReadAllBytes(clientVerFile);
                clientVer = rawClientVer[0] + (rawClientVer[1] * 256);
            }
            catch (Exception)
            {
            }

            // Grab patch.txt and assign values
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(patchInfoURL);
            try
            {
                request.Timeout = 500;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    html = reader.ReadLine();
                    string[] split = html.Split(' ');
                    mainVer = int.Parse(split[0]);
                    logIp[0] = split[1];
                }

                // Download Files
                update();
            }
            catch (Exception)
            {
                UpdateLabel.Content = "Unable to Update";
                StartButton.IsEnabled = true;
            }
        }

        private void update()
        {
            if (clientVer < mainVer)
            {
                UpdateLabel.Content = "Updating MabiPro " + Convert.ToString(clientVer) + " to " + Convert.ToString(clientVer + 1);

                try
                {
                    using (var newPatch = new WebClient())
                    {
                        newPatch.DownloadFileCompleted += wc_DownloadCompleted;
                        newPatch.DownloadProgressChanged += wc_DownloadProgressChanged;
                        newPatch.DownloadFileAsync(new Uri(patchURL + System.Convert.ToString(clientVer) + "_to_" + System.Convert.ToString(clientVer + 1) + ".zip"), "tmp.zip");
                    }
                    clientVer++;
                    if (rawClientVer[0] + 1 < 256)
                        rawClientVer[0] += 1;
                    else
                    {
                        rawClientVer[0] = 0;
                        rawClientVer[1] += 1;
                    }
                    File.WriteAllBytes(@".\" + clientVerFile, rawClientVer);
                }
                catch (WebException)
                {
                    UpdateLabel.Content = "Download Error";
                }
            }
            else
            {
                UpdateLabel.Content = "Up to date!";
                StartButton.IsEnabled = true;
            }
        }


        void wc_DownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            // Extract patch now that download is complete
            downloadWorker.RunWorkerAsync();
        }

        // Updates the progress bar when downloading
        void wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            DownloadProgressBar.Value = e.ProgressPercentage;
        }

        // Majority of patching done here and RunWorkerCompleted
        private void downloadWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Extract new patch
            using (ZipArchive archive = ZipFile.Open(@".\tmp.zip", ZipArchiveMode.Update))
            {
                archive.ExtractToDirectory(@".\", true);
            }
            File.Delete(@".\tmp.zip");
        }

        private void downloadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            update();
        }

    }

    // Inherit ExtractToDirectory from ZipArchiveExtensions 
    public static class ZipArchiveExtensions
    {
        // This function overwrites files in the directory when setting the bool argument to true
        public static void ExtractToDirectory(this ZipArchive archive, string destinationDirectoryName, bool overwrite)
        {
            if (!overwrite)
            {
                archive.ExtractToDirectory(destinationDirectoryName);
                return;
            }
            foreach (ZipArchiveEntry file in archive.Entries)
            {
                string completeFileName = System.IO.Path.Combine(destinationDirectoryName, file.FullName);
                string directory = System.IO.Path.GetDirectoryName(completeFileName);

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (file.Name != "")
                    file.ExtractToFile(completeFileName, true);
            }
        }
    }
}
