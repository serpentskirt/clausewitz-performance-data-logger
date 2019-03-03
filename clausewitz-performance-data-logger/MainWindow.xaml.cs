using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Configuration;
using System.ComponentModel;
using System.IO;
using System.Diagnostics;
using System.Text;

namespace clausewitz_performance_data_logger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Variables

        /// <summary>
        /// Current test session.
        /// </summary>
        private TestSession _testSession;

        /// <summary>
        /// Private log text.
        /// </summary>
        private string _logText;

        /// <summary>
        /// Log text getter/setter.
        /// </summary>
        public string LogText
        {
            get { return _logText; }
            set
            {
                _logText = value;
                RaisePropertyChanged("LogText");
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Log text event.
        /// </summary>
        private void RaisePropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        /// <summary>
        /// Log text event handler.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region UI elements

        /// <summary>
        /// Main window.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            this.TargetTextBox.Text = ConfigurationManager.AppSettings["target"] + " v. " + ConfigurationManager.AppSettings["version"] + " (" + ConfigurationManager.AppSettings["checksum"] + ") - " + ConfigurationManager.AppSettings["platform"]; ;
            Directory.CreateDirectory("sessions");
            LogText += DateTime.Now.ToString("[HH:mm:ss.fff]") + " Activate test session";
        }

        /// <summary>
        /// Create session button click.
        /// </summary>
        private void CreateSessionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _testSession = TestSession.SetInstance(
                                                       Convert.ToInt32(ConfigurationManager.AppSettings["samplingratio"]),
                                                       Convert.ToInt32(ConfigurationManager.AppSettings["refreshrate"]),
                                                       Convert.ToInt32(ConfigurationManager.AppSettings["delay"]),
                                                       ConfigurationManager.AppSettings["daypointerpath"].Split(new char[] { ',' }).Select(s => Convert.ToInt32(s, 16)).ToList(),
                                                       ConfigurationManager.AppSettings["gamespeedpointerpath"].Split(new char[] { ',' }).Select(s => Convert.ToInt32(s, 16)).ToList(),
                                                       ConfigurationManager.AppSettings["gamestatepointerpath"].Split(new char[] { ',' }).Select(s => Convert.ToInt32(s, 16)).ToList(),
                                                       ConfigurationManager.AppSettings["framearraypointerpath"].Split(new char[] { ',' }).Select(s => Convert.ToInt32(s, 16)).ToList(),
                                                       this.NameTextBox.Text
                                                       );
                this.CreateSessionButton.IsEnabled = false;
                this.InitializeDataLoggerButton.IsEnabled = true;
                this.SetupSaveFolderButton.IsEnabled = true;
                this.CloseButton.IsEnabled = true;
                this.NameTextBox.IsEnabled = false;
                this.ConvertLogButton.IsEnabled = true;
                this.filterSpeedSelector.IsEnabled = true;
                LogText += DateTime.Now.ToString("\r\n[HH:mm:ss.fff]") + " Session " + _testSession._sessionName + " activated. Initialize data logger and/or set up save watcher (game needs to be saved at least once)";
            }
            catch (Exception ex)
            {
                LogText += DateTime.Now.ToString("\r\n[HH:mm:ss.fff]") + " Failed to activate " + this.NameTextBox.Text + " session. Following exception occured: " + ex;
            }
        }

        /// <summary>
        /// Session name textbox text change.
        /// </summary>
        private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.NameTextBox.Text != "") this.CreateSessionButton.IsEnabled = true;
            else this.CreateSessionButton.IsEnabled = false;
        }

        /// <summary>
        /// Close session button click.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            LogText += DateTime.Now.ToString("\r\n[HH:mm:ss.fff]") + " Closing " + _testSession._sessionName + " session";
            _testSession.Close();
            _testSession = null;
            this.CreateSessionButton.IsEnabled = true;
            this.StartButton.IsEnabled = false;
            this.CloseButton.IsEnabled = false;
            this.InitializeDataLoggerButton.IsEnabled = false;
            this.SetupSaveFolderButton.IsEnabled = false;
            this.NameTextBox.IsEnabled = true;
            this.ConvertLogButton.IsEnabled = false;
            this.filterSpeedSelector.IsEnabled = false;
            LogText += DateTime.Now.ToString("\r\n[HH:mm:ss.fff]") + " Session closed";
        }

        /// <summary>
        /// Run/stop session button click.
        /// </summary>
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.StartButton.Content.ToString() == "Run")
            {
                this.StartButton.Content = "Stop";
                this.CloseButton.IsEnabled = false;
                this.SaveButton.IsEnabled = true;
                this.ConvertLogButton.IsEnabled = false;
                this.filterSpeedSelector.IsEnabled = false;
                this.InitializeDataLoggerButton.IsEnabled = false;
                this.SetupSaveFolderButton.IsEnabled = false;
                LogText += DateTime.Now.ToString("\r\n[HH:mm:ss.fff]") + ": Started capturing data";
                Task.Factory.StartNew(() =>
                {
                    _testSession.StartLogging();
                });
            }
            else if (this.StartButton.Content.ToString() == "Stop")
            {
                _testSession.StopLogging();
                this.StartButton.Content = "Run";
                this.CloseButton.IsEnabled = true;
                this.SaveButton.IsEnabled = false;
                this.ConvertLogButton.IsEnabled = true;
                this.filterSpeedSelector.IsEnabled = false;
                if (!_testSession._initializedDataLogger) this.InitializeDataLoggerButton.IsEnabled = true;
                if (!_testSession._initializedSaveWatcher) this.SetupSaveFolderButton.IsEnabled = true;              
                LogText += DateTime.Now.ToString("\r\n[HH:mm:ss.fff]") + ": Stopped capturing data";
            }
        }

        /// <summary>
        /// Initialize data logger button click.
        /// </summary>
        private void InitializeDataLoggerButton_Click(object sender, RoutedEventArgs e)
        {
            bool antiCheatPassed = true;
            bool first = true;
            string warning = "";
            StringBuilder sb = new StringBuilder();

            foreach (string name in ConfigurationManager.AppSettings["anticheat"].Split(','))
            {
                Process[] p = Process.GetProcessesByName(name);

                if (p.Length > 0)
                {
                    antiCheatPassed = false;

                    if (first)
                    {
                        first = false;
                        sb.Append(name.ToString());
                    }
                    else
                    {
                        sb.AppendLine();
                        sb.Append(name.ToString());
                    }
                }
            }

            warning = sb.ToString();

            if (antiCheatPassed)
            {
                try
                {
                    _testSession.InitializeDataLogger(ConfigurationManager.AppSettings["process"]);
                    this.InitializeDataLoggerButton.IsEnabled = false;
                    this.StartButton.IsEnabled = true;
                    LogText += DateTime.Now.ToString("\r\n[HH:mm:ss.fff]") + ": Data logger is attached to " + ConfigurationManager.AppSettings["process"] + " process. Set up desired game speed; activate 3dstats command if FPS needed to be tracked; set up save watcher if needed. Start/continiue session by clicking Run button";
                }
                catch (Exception ex)
                {
                    LogText += DateTime.Now.ToString("\r\n[HH:mm:ss.fff]") + ": Data logger failed to attach to " + ConfigurationManager.AppSettings["process"] + " process. Following exception occured: " + ex;
                }
            }
            else
            {
                LogText += DateTime.Now.ToString("\r\n[HH:mm:ss.fff]") + ": Cannot initialize data logger, please close the following processes: \r\n" + warning;
            }
        }

        /// <summary>
        /// Setup save watcher button click.
        /// </summary>
        private void SetupSaveFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.ShowDialog(this);
            try
            {
                if (Path.GetFullPath(ofd.FileName.Substring(0, ofd.FileName.LastIndexOf('\\'))) != Path.GetFullPath("sessions\\" + _testSession._sessionName + "\\saves"))
                { 
                    _testSession.SetupSaveWatcher(ofd.FileName.Substring(0, ofd.FileName.LastIndexOf('\\')));
                    this.SetupSaveFolderButton.IsEnabled = false;
                    this.StartButton.IsEnabled = true;
                    LogText += DateTime.Now.ToString("\r\n[HH:mm:ss.fff]") + " Save watcher is set to track changes in " + ofd.FileName.Substring(0, ofd.FileName.LastIndexOf('\\')) + " folder. Initialize data logger if needed. Start/continiue session by clicking Run button";
                }
                else LogText += DateTime.Now.ToString("\r\n[HH:mm:ss.fff]") + " Source and target saved games folders cannot be the same!";
            }
            catch (Exception ex)
            {
                LogText += DateTime.Now.ToString("\r\n[HH:mm:ss.fff]") + ": Failed to set saved games folder. Following exception occured: " + ex;
            }
        }

        /// <summary>
        /// Convert log button click.
        /// </summary>
        private void ConvertLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int filterSpeed = Int32.Parse(this.filterSpeedSelector.SelectedItem.ToString().Split(':')[1]);

                if (File.Exists("sessions\\" + _testSession._sessionName + "\\performance.bin"))
                {
                    LogText += DateTime.Now.ToString("\r\n[HH:mm:ss.fff]") + ": Started converting log";
                    Task.Factory.StartNew(() =>
                    {
                        File.WriteAllText("sessions\\" + _testSession._sessionName + "\\" + _testSession._sessionName + "_performance.csv", LogConverter.ConvertBinaryLog(File.ReadAllBytes("sessions\\" + _testSession._sessionName + "\\performance.bin"), filterSpeed));
                        LogText += DateTime.Now.ToString("\r\n[HH:mm:ss.fff]") + ": Finished converting log";
                    });
                }
                else { LogText += DateTime.Now.ToString("\r\n[HH:mm:ss.fff]") + ": No captured data is found! Record some using data logger"; }
            }
            catch (Exception ex)
            {
                LogText += DateTime.Now.ToString("\r\n[HH:mm:ss.fff]") + ": Failed to convert log. Following exception occured: " + ex;
            }
        }

        /// <summary>
        /// Open sessions folder button click.
        /// </summary>
        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", "sessions");
        }

        /// <summary>
        /// Save session data button click.
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _testSession.SaveData();
            LogText += DateTime.Now.ToString("\r\n[HH:mm:ss.fff]") + ": Saved captured data.";
        }

        #endregion
    }
}
