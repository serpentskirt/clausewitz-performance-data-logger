using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

namespace clausewitz_performance_data_logger
{
    /// <summary>
    ///     Captures data from process.
    /// </summary>
    internal sealed class DataLogger
    {
        #region Variables

        /// <summary>
        ///     Singleton instance.
        /// </summary>
        private static DataLogger _instance;

        /// <summary>
        ///     Memory interface.
        /// </summary>
        private MemoryInterface _m;

        /// <summary>
        ///     Binary log writer.
        /// </summary>
        private BinaryWriter _logWriter;

        /// <summary>
        ///     Data logger's sampling ratio in [Hz].
        /// </summary>
        private readonly int _samplingRatio;

        /// <summary>
        ///     Timer period.
        /// </summary>
        private readonly int _period;

        /// <summary>
        ///     Indicates if data logger has been attached to a process.
        /// </summary>
        private bool _initialized;

        /// <summary>
        ///     Indicates if data logger is running.
        /// </summary>
        private bool _running;

        /// <summary>
        ///     CheatEngine-based pointer list for day memory address.
        /// </summary>
        private readonly List<int> _dayPointerPath;

        /// <summary>
        ///     CheatEngine-based pointer list for game speed memory address.
        /// </summary>
        private readonly List<int> _gameSpeedPointerPath;

        /// <summary>
        ///     CheatEngine-based pointer list for game state memory address.
        /// </summary>
        private readonly List<int> _gameStatePointerPath;

        /// <summary>
        ///     CheatEngine-based pointer list for frame render times array memory address.
        /// </summary>
        private readonly List<int> _frameTimePointerPath;

        /// <summary>
        ///     Day array size.
        /// </summary>
        private const int _dayArraySize = 4;

        /// <summary>
        ///     Game speed array size.
        /// </summary>
        private const int _gameSpeedArraySize = 4;

        /// <summary>
        ///     Game state array size.
        /// </summary>
        private const int _gameStateArraySize = 1;

        /// <summary>
        ///     FPS array size.
        /// </summary>
        private const int _fpsArraySize = 4;

        /// <summary>
        ///     Session path.
        /// </summary>
        private readonly string _path;

        /// <summary>
        ///     Currently captured day array.
        /// </summary>
        private byte[] _day;

        /// <summary>
        ///     Previously captured day array.
        /// </summary>
        private byte[] _prevDay;

        /// <summary>
        ///     Currently captured game speed.
        /// </summary>
        private byte[] _gameSpeed;

        /// <summary>
        ///     Previously captured game speed.
        /// </summary>
        private byte[] _prevGameSpeed;

        /// <summary>
        ///     Currently captured game state.
        /// </summary>
        private byte[] _gameState;

        /// <summary>
        ///     Previously captured game state.
        /// </summary>
        private byte[] _prevGameState;

        /// <summary>
        ///     Currently captured FPS.
        /// </summary>
        private byte[] _fps;

        /// <summary>
        ///     Previously captured FPS.
        /// </summary>
        private byte[] _prevFps;

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
        /// <param name="samplingRatio">Sampling ratio in [Hz].</param>
        /// <param name="dayPointerPath">CheatEngine-based pointer list for day memory address.</param>
        /// <param name="gameSpeedPointerPath">CheatEngine-based pointer list for game speed memory address.</param>
        /// <param name="gameSpeedPointerPath">CheatEngine-based pointer list for game state memory address.</param>
        /// <param name="frameTimePointerPath">CheatEngine-based pointer list for frame render times array memory address.</param>
        /// <param name="session">Session name.</param>
        private DataLogger(int samplingRatio,
                           List<int> dayPointerPath,
                           List<int> gameSpeedPointerPath,
                           List<int> gameStatePointerPath,
                           List<int> frameTimePointerPath,
                           string session)
        {
            // Divide by zero check
            if (samplingRatio == 0)
            {
                throw new DivideByZeroException("Sampling ratio cannot be zero.");
            }

            // Setting up variables
            _samplingRatio = samplingRatio;
            _period = 1000 / _samplingRatio;

            _initialized = false;
            _running = false;

            _waitHandler = new EventWaitHandle(false, EventResetMode.AutoReset, Guid.NewGuid().ToString());

            _dayPointerPath = dayPointerPath;
            _gameSpeedPointerPath = gameSpeedPointerPath;
            _gameStatePointerPath = gameStatePointerPath;
            _frameTimePointerPath = frameTimePointerPath;

            _path = "sessions\\" + session;
        }

        /// <summary>
        ///     Singleton instance creator.
        /// </summary>
        /// <param name="samplingRatio">Sampling ratio in [Hz].</param>
        /// <param name="dayPointerPath">CheatEngine-based pointer list for day memory address.</param>
        /// <param name="gameSpeedPointerPath">CheatEngine-based pointer list for game speed memory address.</param>
        /// <param name="gameSpeedPointerPath">CheatEngine-based pointer list for game state memory address.</param>
        /// <param name="frameTimePointerPath">CheatEngine-based pointer list for frame render times array memory address.</param>
        /// <param name="session">Session name.</param>
        /// <returns>The single class instance.</returns>
        public static DataLogger SetInstance(int samplingRatio,
                                             List<int> dayPointerPath,
                                             List<int> gameSpeedPointerPath,
                                             List<int> gameStatePointerPath,
                                             List<int> frameTimePointerPath,
                                             string session)
        {
            return _instance ?? (_instance = new DataLogger(samplingRatio,
                                                            dayPointerPath,
                                                            gameSpeedPointerPath,
                                                            gameStatePointerPath,
                                                            frameTimePointerPath,
                                                            session));
        }

        /// <summary>
        ///     Retrieve the singleton instance.
        /// </summary>
        /// <returns>The single class instance.</returns>
        public static DataLogger GetInstance()
        {
            if (_instance == null)
                throw new Exception("Something terrible happened.");
            return _instance;
        }

        #endregion

        #region NativeMethods

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1,
                                 byte[] b2,
                                 long count);

        #endregion

        #region NativeMethods wrappers

        /// <summary>
        ///     memcmp wrapper.
        /// </summary>
        /// <param name="byteArray1">First array to compare.</param>
        /// <param name="byteArray2">Second array to compare.</param>
        /// <returns>True if arrys are identical, false otherwise.</returns>
        private static bool ByteArrayCompare(byte[] byteArray1, byte[] byteArray2)
        {
            return byteArray1.Length == byteArray2.Length && memcmp(byteArray1, byteArray2, byteArray1.Length) == 0;
        }

        #endregion

        #region Methods

        /// <summary>
        ///     Attaches data logger to process.
        /// </summary>
        /// <param name="targetProcess">Process name.</param>
        public void Initialize(string targetProcess)
        {
            Process process;

            Process[] p = Process.GetProcessesByName(targetProcess);

            if (p.Length <= 0)
                throw new InvalidOperationException("No " + targetProcess + " process found.");
            else if (p.Length > 1)
                throw new NotImplementedException("Multiple processes are not supported.");
            else
            {
                process = p[0];
                _m = MemoryInterface.SetInstance(process);
                _initialized = true;
            }
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
        ///     Stops data logger, saves all captured data and empties the buffer, saves snapshot.
        /// </summary>
        public void Stop()
        {
            if (_running)
            { 
                _running = false;
                _logWriter.Flush();
                _logWriter.Close();

                // This needs to be merged into one function 
                WriteSnapshot(_path + "\\dayLast.bin", _day);
                WriteSnapshot(_path + "\\gameSpeedLast.bin", _gameSpeed);
                WriteSnapshot(_path + "\\gameStateLast.bin", _gameState);
                WriteSnapshot(_path + "\\fpsLast.bin", _fps);
            }
        }

        /// <summary>
        ///     Saves all captured data to file and empties the buffer.
        /// </summary>
        public void Save()
        {
            _logWriter.Flush();
            _logWriter.Close();
            _logWriter = new BinaryWriter(File.Open(_path + "\\performance.bin", FileMode.Append));
        }

        /// <summary>
        ///     Reads day value from memory.
        /// </summary>
        /// <returns>Int32 value represented as byte array.</returns>
        private byte[] GetDay()
        {
            uint size = _dayArraySize;
            byte[] result = new byte[size];
            return _m.ReadAddress(_m.FollowPointerPath(_dayPointerPath), size, out int readBytes);
        }

        /// <summary>
        ///     Reads game speed from memory.
        /// </summary>
        /// <returns>Int32 value merged with Boolean value, represented as byte array.</returns>
        private byte[] GetGameSpeed()
        {
            uint size = _gameSpeedArraySize;
            byte[] result = new byte[size];
            return _m.ReadAddress(_m.FollowPointerPath(_gameSpeedPointerPath), size, out int readBytes);
        }

        /// <summary>
        ///     Reads game state from memory.
        /// </summary>
        /// <returns>Int32 value merged with Boolean value, represented as byte array.</returns>
        private byte[] GetGameState()
        {
            uint size = _gameStateArraySize;
            byte[] result = new byte[size];
            return _m.ReadAddress(_m.FollowPointerPath(_gameStatePointerPath), size, out int readBytes);
        }

        /// <summary>
        ///     Reads frame render times from memory and caclulates FPS.
        /// </summary>
        /// <returns>Single value represented as byte array.</returns>
        private byte[] GetFps()
        {
            uint size = _fpsArraySize;
            uint renderedFramesArraySize = 100;
            byte[] result = new byte[size];
            float frames = 0;

            for (int i = 0; i < renderedFramesArraySize; i++)
            {
                frames += BitConverter.ToSingle(_m.ReadAddress(_m.FollowPointerPath(_frameTimePointerPath) + (i * (int)size), size, out int readBytes), 0);
            }

            // That's probably not necessary
            if (frames != 0)
            {
                result = BitConverter.GetBytes(100000 / frames);
            }
            else
            {
                result = BitConverter.GetBytes(0);
            }

            return result;
        }

        /// <summary>
        ///     Writes captured data to buffer.
        /// </summary>
        private void WriteLog(byte[] day, byte[] speed, byte[] state, byte[] fps)
        {
            _logWriter.Write(day);
            _logWriter.Write(DateTime.UtcNow.Ticks);
            _logWriter.Write(speed);
            _logWriter.Write(state);
            _logWriter.Write(fps);
        }

        /// <summary>
        ///     Saves last captured data to file.
        /// </summary>
        private void WriteSnapshot(string path, byte[] data)
        {
            if (data != null)
            {
                // After merging snapshots this should create a file with hardcoded name
                if (File.Exists(path)) { File.Delete(path); }

                BinaryWriter stateSaver = new BinaryWriter(File.Open(path, FileMode.Append));

                stateSaver.Write(data);
                stateSaver.Flush();
                stateSaver.Close();
            }
        }

        /// <summary>
        ///     Starts capturing data.
        /// </summary>
        public void Log()
        {
            _running = true;

            _day = new byte[_dayArraySize];
            _prevDay = new byte[_dayArraySize];

            _gameSpeed = new byte[_gameSpeedArraySize];
            _prevGameSpeed = new byte[_gameSpeedArraySize];

            _gameState = new byte[_gameStateArraySize];
            _prevGameState = new byte[_gameStateArraySize];

            _fps = new byte[_fpsArraySize];
            _prevFps = new byte[_fpsArraySize];

            _logWriter = new BinaryWriter(File.Open(_path + "\\performance.bin", FileMode.Append));

            // This needs to be merged into one snapshot

            string nextfile = _path + "\\dayLast.bin";
            if (File.Exists(nextfile)) { _prevDay = File.ReadAllBytes(nextfile); }
            nextfile = _path + "\\gameSpeedLast.bin";
            if (File.Exists(nextfile)) { _prevGameSpeed = File.ReadAllBytes(nextfile); }
            nextfile = _path + "\\gameStateLast.bin";
            if (File.Exists(nextfile)) { _prevGameState = File.ReadAllBytes(nextfile); }
            nextfile = _path + "\\fpsLast.bin";
            if (File.Exists(nextfile)) { _prevFps = File.ReadAllBytes(nextfile); }

            do
            {
                _day = GetDay();
                _gameSpeed = GetGameSpeed();
                _gameState = GetGameState();
                _fps = GetFps();

                if (!ByteArrayCompare(_day, _prevDay) || !ByteArrayCompare(_gameSpeed, _prevGameSpeed) || !ByteArrayCompare(_gameState, _prevGameState))
                {
                    WriteLog(_day, _gameSpeed, _gameState, _fps);
                }

                _prevDay = _day;
                _prevGameSpeed = _gameSpeed;
                _prevGameState = _gameState;
                _prevFps = _fps;

                _waitHandler.WaitOne(TimeSpan.FromMilliseconds(_period));
            } while (_running);
        }
    }

    #endregion
}