using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace clausewitz_performance_data_logger
{
    /// <summary>
    ///     Provides process memory-reading methods.
    /// </summary>
    internal sealed class MemoryInterface
    {
        #region Variables

        /// <summary>
        ///     Singleton instance.
        /// </summary>
        private static MemoryInterface _instance;

        /// <summary>
        ///     Target process to attach to.
        /// </summary>
        private readonly Process _targetProcess;

        /// <summary>
        ///     Target process handle.
        /// </summary>
        private IntPtr _processHandle;

        /// <summary>
        ///     Access flags necessary for OpenProcess P/Invoke.
        /// </summary>
        public enum ProcessAccessFlags : uint
        {
            /// <summary>
            ///     Read process memory.
            /// </summary>
            VMRead = 0x00000010
        }

        #endregion

        #region [C|D]tors

        /// <summary>
        ///     Hidden constructor.
        /// </summary>
        /// <param name="targetProcess">Process to attach to.</param>
        private MemoryInterface(Process targetProcess)
        {
            _targetProcess = targetProcess;
            OpenProcess((uint)targetProcess.Id);
        }

        /// <summary>
        ///     Destructor.
        /// </summary>
        ~MemoryInterface()
        {
            CloseHandle(_processHandle);
        }

        /// <summary>
        ///     Singleton instance creator.
        /// </summary>
        /// <param name="targetProcess">Process to attach to.</param>
        /// <returns>The single class instance.</returns>
        public static MemoryInterface SetInstance(Process targetProcess)
        {
            return _instance ?? (_instance = new MemoryInterface(targetProcess));
        }

        /// <summary>
        ///     Retrieve the singleton instance.
        /// </summary>
        /// <returns>The single class instance.</returns>
        public static MemoryInterface GetInstance()
        {
            if (_instance == null)
                throw new Exception("Something terrible happened.");
            return _instance;
        }

        #endregion

        #region NativeMethods

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess,
                                                 [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
                                                 UInt32 dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern Int32 CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern Int32 ReadProcessMemory(IntPtr hProcess,
                                                      IntPtr lpBaseAddress,
                                                      [In, Out] byte[] buffer,
                                                      UInt32 size,
                                                      out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess,
                                                      IntPtr lpBaseAddress,
                                                      byte[] lpBuffer,
                                                      int nSize,
                                                      out IntPtr lpNumberOfBytesWritten);

        #endregion

        #region NativeMethods wrappers

        /// <summary>
        ///     ReadProcessMemory wrapper.
        /// </summary>
        /// <param name="memoryAddress">Address to read.</param>
        /// <param name="bytesToRead">Number of bytes to read.</param>
        /// <param name="bytesRead">Variable to write read bytes to.</param>
        /// <returns>Array of read bytes or [0, 0, 0, 0] in case of failure.</returns>
        public byte[] ReadAddress(IntPtr memoryAddress, uint bytesToRead, out int bytesRead)
        {
            try
            {
                if (bytesToRead > 0)
                {
                    var buffer = new byte[bytesToRead];
                    ReadProcessMemory(_processHandle, memoryAddress, buffer, bytesToRead, out IntPtr ptrBytesReaded);
                    bytesRead = ptrBytesReaded.ToInt32();
                    return buffer;
                }
                bytesRead = 0;
                return new byte[] { 0, 0, 0, 0 };   // check if this has performance impact
            }
            catch
            {
                bytesRead = 0;
                return new byte[] { 0, 0, 0, 0 };   // check if this has performance impact
            }
        }

        #endregion

        #region Methods

        /// <summary>
        ///     Opens and attaches to process.
        /// </summary>
        /// <param name="processpid">Process ID to attach to.</param>
        private void OpenProcess(uint processpid)
        {
            try
            {
                _processHandle = OpenProcess(ProcessAccessFlags.VMRead, false, processpid);
            }
            catch { }
        }

        /// <summary>
        ///     Follows a CheatEngine-based pointer path.
        /// </summary>
        /// <param name="path">List of pointers to follow, last element is an offset to be added to the final result.</param>
        /// <returns>Memory address pointers point to.</returns>
        public IntPtr FollowPointerPath(List<long> path)
        {
            IntPtr result;
            long currentPointer = _targetProcess.MainModule.BaseAddress.ToInt64();
            int i = 0;
            int count = path.Count;

            foreach (int pointer in path)
            {
                if (++i < count)
                {
                    currentPointer += pointer;
                    currentPointer = BitConverter.ToInt64(ReadAddress((IntPtr)currentPointer, 8, out int readBytes), 0);
                }
                else
                    currentPointer += pointer;
            }

            result = (IntPtr)currentPointer;
            return result;
        }

        #endregion

    }
}