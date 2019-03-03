using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace clausewitz_performance_data_logger
{
    /// <summary>
    ///     Captures data from process.
    /// </summary>
    internal sealed class TestSession
    {
        #region Variables

        /// <summary>
        ///     Singleton instance.
        /// </summary>
        private static TestSession _instance;

        /// <summary>
        ///     Name of the test session.
        /// </summary>
        public string _sessionName;

        /// <summary>
        ///     Data logger to capture data.
        /// </summary>
        private static DataLogger _dataLogger;

        /// <summary>
        ///     Save watcher to back up saves.
        /// </summary>
        private static SaveWatcher _saveWatcher;

        /// <summary>
        ///     Data logger initialization flag.
        /// </summary>
        public bool _initializedDataLogger;

        /// <summary>
        ///     Save watcher initialization flag.
        /// </summary>
        public bool _initializedSaveWatcher;

        /// <summary>
        ///     Continuous mode flag.
        /// </summary>
        private const uint ES_CONTINUOUS = 0x80000000;

        /// <summary>
        ///     System working state enforcement flag.
        /// </summary>
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;

        /// <summary>
        ///     Display working state enforcement flag.
        /// </summary>
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;

        #endregion

        #region Constructors

        /// <summary>
        ///     Hidden constructor.
        /// </summary>
        /// <param name="samplingRatio">Data logger sampling ratio in [Hz].</param>
        /// <param name="refreshRate">Save watcher refresh rate in [Hz].</param>
        /// <param name="delay">Data logger file access delay in [ms].</param>
        /// <param name="dayPointerPath">CheatEngine-based pointer list for day memory address.</param>
        /// <param name="gameSpeedPointerPath">CheatEngine-based pointer list for game speed memory address.</param>
        /// <param name="gameStatePointerPath">CheatEngine-based pointer list for game state memory address.</param>
        /// <param name="frameTimePointerPath">CheatEngine-based pointer list for frame render times array memory address.</param>
        /// <param name="sessionName">Current session name.</param>
        private TestSession(int samplingRatio, int refreshRate, int delay, List<int> dayPointerPath, List<int> gameStatePointerPath, List<int> gameSpeedPointerPath, List<int> frameTimePointerPath, string sessionName)
        {
            Directory.CreateDirectory("sessions\\" + sessionName);
            Directory.CreateDirectory("sessions\\" + sessionName + "\\saves");
            
            //StreamWriter sw = File.AppendText("sessions\\" + sessionName + "\\" + sessionName);
            //sw.Close();

            _initializedDataLogger = false;
            _initializedSaveWatcher = false;

             _sessionName = sessionName;
            _dataLogger = DataLogger.SetInstance(samplingRatio, dayPointerPath, gameStatePointerPath, gameSpeedPointerPath, frameTimePointerPath, _sessionName);
            _saveWatcher = SaveWatcher.SetInstance(refreshRate, delay, _sessionName);
        }

        /// <summary>
        ///     Singleton instance creator.
        /// </summary>
        /// <param name="samplingRatio">Data logger sampling ratio in [Hz].</param>
        /// <param name="refreshRate">Save watcher refresh rate in [Hz].</param>
        /// <param name="delay">Data logger file access delay in [ms].</param>
        /// <param name="dayPointerPath">CheatEngine-based pointer list for day memory address.</param>
        /// <param name="gameSpeedPointerPath">CheatEngine-based pointer list for game speed memory address.</param>
        /// <param name="gameStatePointerPath">CheatEngine-based pointer list for game state memory address.</param>
        /// <param name="frameTimePointerPath">CheatEngine-based pointer list for frame render times array memory address.</param>
        /// <param name="sessionName">Current session name.</param>
        /// <returns>The single class instance.</returns>
        public static TestSession SetInstance(int samplingRatio, int refreshRate, int delay, List<int> dayPointerPath, List<int> gameSpeedPointerPath, List<int> gameStatePointerPath, List<int> frameTimePointerPath, string sessionName)
        {
            return _instance ?? (_instance = new TestSession(samplingRatio, refreshRate, delay, dayPointerPath, gameSpeedPointerPath, gameStatePointerPath, frameTimePointerPath, sessionName));
        }

        /// <summary>
        ///     Retrieve the singleton instance.
        /// </summary>
        /// <returns>The single class instance.</returns>
        public static TestSession GetInstance()
        {
            if (_instance == null)
                throw new Exception("Something terrible happened.");
            return _instance;
        }

        /// <summary>
        ///     Destructor.
        /// </summary>
        ~TestSession()
        {
            if (_dataLogger != null)
            {
                _dataLogger.Stop();
                _dataLogger = null;
            }

            if (_saveWatcher != null)
            {
                _saveWatcher.Stop();
                _saveWatcher = null;
            }

            RestoreSleepMode();
        }

        #endregion

        #region NativeMethods

        /// <summary>
        ///     Used to force specific states on system.
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint SetThreadExecutionState([In] uint esFlags);

        #endregion

        #region NativeMethods wrappers

        /// <summary>
        ///     Disables sleep mode.
        /// </summary>
        private void DisableSleepMode()
        {
            SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED);
        }

        /// <summary>
        ///     Restores sleep mode.
        /// </summary>
        private void RestoreSleepMode()
        {
            SetThreadExecutionState(ES_CONTINUOUS);
        }

        #endregion

        #region Methods

        /// <summary>
        ///     Initializes data logger.
        /// </summary>
        public void InitializeDataLogger(string process)
        {
            _dataLogger.Initialize(process);
            _initializedDataLogger = true;
        }

        /// <summary>
        ///     Initializes data logger.
        /// </summary>
        public void SetupSaveWatcher(string path)
        {
            _saveWatcher.Initialize(path);
            _initializedSaveWatcher = true;
        }

        /// <summary>
        ///     Starts data logging.
        /// </summary>
        public void StartLogging()
        {
            if ((_dataLogger != null && _dataLogger.IsInitialized()) && (_saveWatcher != null && _saveWatcher.IsInitialized()))
            {
                Parallel.Invoke(
                    () => _dataLogger.Log(),
                    () => _saveWatcher.Watch(),
                    () => DisableSleepMode()
                );
            }
            else if (_dataLogger != null && _dataLogger.IsInitialized())
            {
                Parallel.Invoke(
                    () => _dataLogger.Log(),
                    () => DisableSleepMode()
                );
            }
            else if (_saveWatcher != null && _saveWatcher.IsInitialized())
            {
                Parallel.Invoke(
                    () => _saveWatcher.Watch(),
                    () => DisableSleepMode()
                );
            }
        }

        /// <summary>
        ///     Stops data logging.
        /// </summary>
        public void StopLogging()
        {
            if (_dataLogger != null && _dataLogger.IsInitialized()) _dataLogger.Stop();
            if (_saveWatcher != null && _saveWatcher.IsInitialized()) _saveWatcher.Stop();
            RestoreSleepMode();
        }

        /// <summary>
        ///     Saves captured data.
        /// </summary>
        public void SaveData()
        {
            if (_dataLogger != null && _dataLogger.IsInitialized()) _dataLogger.Save();
        }

        /// <summary>
        ///     Closes test session.
        /// </summary>
        public void Close()
        {
            _sessionName = null;
            _dataLogger = null;
            _saveWatcher = null;
            _instance = null;
            _initializedDataLogger = false;
            _initializedSaveWatcher = false;
            RestoreSleepMode();
        }

        #endregion
    }
}