using System;
using System.IO;
using System.Threading;
using System.Linq;
using System.Text.RegularExpressions;

namespace clausewitz_performance_data_logger
{
    /// <summary>
    ///     Automatically backs up saves.
    /// </summary>
    internal sealed class SaveWatcher
    {
        #region Variables

        /// <summary>
        ///     Singleton instance.
        /// </summary>
        private static SaveWatcher _instance;

        /// <summary>
        ///     Indicates if save folder has been set.
        /// </summary>
        private bool _initialized;

        /// <summary>
        ///     Indicates if save watcher is watching.
        /// </summary>
        private bool _running;

        /// <summary>
        ///     File watcher.
        /// </summary>
        private FileSystemWatcher _watcher;

        /// <summary>
        ///     Save watcher's refresh rate in [Hz].
        /// </summary>
        private readonly int _refreshRate;

        /// <summary>
        ///     Timer period.
        /// </summary>
        private readonly int _period;

        /// <summary>
        ///     File access delay.
        /// </summary>
        private readonly int _delay;

        /// <summary>
        ///     Session path.
        /// </summary>
        private readonly string _path;

        /// <summary>
        ///     Path to saved games folder.
        /// </summary>
        private string _saveGamePath;

        /// <summary>
        ///     Ironman saves count.
        /// </summary>
        private uint _ironmanCount;

        /// <summary>
        ///     Ironman flag.
        /// </summary>
        private bool _isIronman;

        /// <summary>
        ///     Ironman save lock.
        /// </summary>
        private bool _ironmanSaveLock;

        #endregion

        #region Events

        /// <summary>
        ///     Timer event handler.
        /// </summary>
        private EventWaitHandle _waitHandler;

        #endregion

        #region Constructors

        /// <summary>
        ///     Hidden constructor.
        /// </summary>
        /// <param name="refreshRate">Refresh rate in [Hz].</param>
        /// <param name="delay">File access delay in [ms].</param>
        /// <param name="session">Session name.</param>
        private SaveWatcher(int refreshRate, int delay, string session)
        {
            // Divide by zero check
            if (refreshRate == 0)
            {
                throw new DivideByZeroException("Sampling ratio cannot be zero.");
            }

            // Setting up variables
            _refreshRate = refreshRate;
            _period = 1000 / _refreshRate;
            _delay = delay;
            _initialized = false;
            _running = false;
            _ironmanCount = 0;
            _isIronman = false;
            _ironmanSaveLock = true;

            _waitHandler = new EventWaitHandle(false, EventResetMode.AutoReset, Guid.NewGuid().ToString());

            _path = "sessions\\" + session + "\\saves";
        }

        /// <summary>
        ///     Singleton instance creator.
        /// </summary>
        /// <param name="refreshRate">Refresh rate in [Hz].</param>
        /// <param name="session">Session name.</param>
        /// <returns>The single class instance.</returns>
        public static SaveWatcher SetInstance(int refreshRate, int delay, string session)
        {
            return _instance ?? (_instance = new SaveWatcher(refreshRate, delay, session));
        }

        /// <summary>
        ///     Retrieve the singleton instance.
        /// </summary>
        /// <returns>The single class instance.</returns>
        public static SaveWatcher GetInstance()
        {
            if (_instance == null)
                throw new Exception("Something terrible happened.");
            return _instance;
        }

        #endregion

        #region Methods

        /// <summary>
        ///     Sets saved games folder path, prepares backup folder and sets ironman flag.
        /// </summary>
        /// <param name="path">Path to saved game folder.</param>
        public void Initialize(string path)
        {
            _saveGamePath = path;
            _watcher = new FileSystemWatcher
            {
                Path = _saveGamePath,
                NotifyFilter = NotifyFilters.LastWrite,
                Filter = "*.*"
            };

            if (!Directory.EnumerateFileSystemEntries(_path).Any())
            {
                foreach (var file in Directory.GetFiles(_saveGamePath))
                {
                    if (Regex.IsMatch(Path.GetFileName(file), @"ironman") && !_isIronman)
                    {
                        _isIronman = true;
                    }

                    if (_isIronman)
                    {
                        try
                        {
                            File.Copy(file, Path.Combine(_path, Path.GetFileNameWithoutExtension(file) + "_" + _ironmanCount.ToString() + Path.GetExtension(file)));
                            _ironmanCount++;
                        }
                        catch
                        {

                        }
                    }
                    else
                    {
                        try
                        {
                            File.Copy(file, Path.Combine(_path, Path.GetFileName(file)));
                        }
                        catch
                        {

                        }
                    }
                }
            }

            else
            {
                var file = Directory.EnumerateFiles(_path).OrderByDescending(filename => filename).FirstOrDefault();
                if (Regex.IsMatch(Path.GetFileName(file), @"ironman"))
                {
                    _isIronman = true;
                    _ironmanCount = Convert.ToUInt32(Path.GetFileNameWithoutExtension(file).Substring(Path.GetFileNameWithoutExtension(file).LastIndexOf('_') + 1)) + 1;
                }
            }

            _initialized = true;
        }

        /// <summary>
        ///     Gets initialized state.
        /// </summary>
        /// <returns>True if data logger has been attached to process.</returns>
        public bool IsInitialized()
        {
            return _initialized;
        }

        /// <summary>
        ///     Stops save watcher.
        /// </summary>
        public void Stop()
        {
            if (_running)
            {
                _running = false;
                _watcher.Changed -= new FileSystemEventHandler(OnChanged);
                _watcher.EnableRaisingEvents = false;
            }
        }

        /// <summary>
        ///     Starts watching saves.
        /// </summary>
        public void Watch()
        {
            _watcher.Changed += new FileSystemEventHandler(OnChanged);
            _watcher.EnableRaisingEvents = true;

            _running = true;

            do
            {
                _waitHandler.WaitOne(TimeSpan.FromMilliseconds(_period));
            }
            while (_running);
        }

        #endregion

        #region Events

        /// <summary>
        ///     Fires when change is detected in folder.
        /// </summary>
        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            // Need to wait a bit before file is accessible
            System.Threading.Thread.Sleep(_instance._delay);

            var file = (from f in new DirectoryInfo(_instance._saveGamePath).GetFiles()
                        orderby f.LastWriteTime descending
                        select f).First();

            // Ironman saves trigger two events, one needs to be ignored
            // This might be the case for saving manually game too
            // In that case it is probably better to just overwrite file
            // Probably worth extracting date from savegame to name the ironman file
            if (_instance._isIronman)
            {
                if (!_instance._ironmanSaveLock)
                {
                    try
                    {
                        File.Copy(file.FullName, Path.Combine(_instance._path, Path.GetFileNameWithoutExtension(file.FullName) + "_" + _instance._ironmanCount.ToString() + Path.GetExtension(file.FullName)));
                    }
                    catch
                    {

                    }
                    _instance._ironmanCount++;
                }
                _instance._ironmanSaveLock = !_instance._ironmanSaveLock;
            }
            else
            {
                try
                {
                    File.Copy(file.FullName, Path.Combine(_instance._path, file.Name));
                }
                catch
                {

                }
            }
        }

        #endregion
    }
}