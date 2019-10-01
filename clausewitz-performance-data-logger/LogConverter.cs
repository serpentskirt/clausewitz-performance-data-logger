using System;
using System.Text;

namespace clausewitz_performance_data_logger
{
    /// <summary>
    ///     Converts binary log to csv.
    /// </summary>
    public static class LogConverter
    {
        /// <summary>
        ///     Binary entry size.
        /// </summary>
        private const int _size = 45;

        /// <summary>
        ///     Captures data from process.
        /// </summary>
        /// <param name="data">Captured data as byte array.</param>
        /// <param name="filterSpeed">Speed the game was played on.</param>
        public static string ConvertBinaryLog(byte[] data, int filterSpeed)
        {
            int entries = data.Length / _size;
            const char separator = ',';

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("day,delta,fps,pagedMemorySize,virtualMemorySize,ioData");

            int day                 = 0;
            int prevDay             = 0;
            long ticks              = 0;
            long prevTicks          = 0;
            int speed               = 0;
            bool paused             = false;
            bool isPaused           = false;
            float fps               = 0;
            long delta              = 0;
            long transitionDelta    = 0;
            long pagedMemorySize    = 0;
            long virtualMemorySize  = 0;
            double ioData            = 0;

            for (int i = 0; i < entries; i++)
            {
                // Parsing entry

                // 4 bytes of date
                day = BitConverter.ToInt32(data, i * _size) - 792000;

                // 8 bytes of ticks
                ticks = BitConverter.ToInt64(data, i * _size + 4);

                // 4 bytes of speed
                speed = BitConverter.ToInt32(data, i * _size + 12);

                // 1 byte of paused flag
                paused = BitConverter.ToBoolean(data, i * _size + 16);

                // 4 bytes of fps
                fps = BitConverter.ToSingle(data, i * _size + 17);

                // 8 bytes of paged memory size
                pagedMemorySize = BitConverter.ToInt64(data, i * _size + 21);

                // 8 bytes of virtual memory size
                virtualMemorySize = BitConverter.ToInt64(data, i * _size + 29);

                // 8 bytes of I/O data
                ioData = BitConverter.ToDouble(data, i * _size + 37);

                // Skipping first entry to avoid huge peak
                if (i == 0) prevDay = day;

                if (speed == filterSpeed)
                {
                    delta = ticks - prevTicks;

                    // If paused note how much time passed since pause was initiated
                    if (paused)
                    {
                        isPaused = true;
                        transitionDelta = delta;
                        if (i == 0)
                        {
                            transitionDelta -= delta;
                        }
                    }

                    if (prevDay != day)
                    {
                        // If game was paused add time it took before pause was inititated
                        if (!paused && isPaused)
                        {
                            isPaused = false;
                            delta += transitionDelta;
                        }

                        sb.Append(day.ToString());
                        sb.Append(separator);
                        sb.Append(delta.ToString());
                        sb.Append(separator);
                        sb.Append(fps.ToString());
                        sb.Append(separator);
                        sb.Append(pagedMemorySize.ToString());
                        sb.Append(separator);
                        sb.Append(virtualMemorySize.ToString());
                        sb.Append(separator);
                        sb.Append(ioData.ToString());

                        if (i < entries - 1) sb.AppendLine();
                    }

                    prevDay = day;
                    prevTicks = ticks;  
                }
            }

            return sb.ToString();
        }
    }
}
