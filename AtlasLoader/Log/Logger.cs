using System;
using System.IO;

namespace AtlasLoader
{
    /// <summary>
    ///     Handles logging messages from AtlasLoader /mods.
    /// </summary>
    public static class Logger
    {
        /// <summary>
        /// 	The file extension used for log files.
        /// </summary>
        public const string FileExtension = "log";

        /// <summary>
        ///     Relative path to the logs directory.
        /// </summary>
        public const string LogsDirectory = CoreModule.RootDirectory + "logs/";

        /// <summary>
        /// 	The time format of the log times, used to name them.
        /// </summary>
        public const string TimeFormat = "yyyy-MM-ddTHH-mm-ssZ";

        private static readonly StreamWriter MessageWriter;

        public static DateTime StuckDate { get; }

        /// <summary>
        /// 	The path to the log file.
        /// </summary>
        public static string LogPath { get; }

        static Logger()
        {
            if (!Directory.Exists(LogsDirectory))
                Directory.CreateDirectory(LogsDirectory);

            StuckDate = DateTime.Now;
            LogPath = GetFilePath(StuckDate);
            MessageWriter = new StreamWriter(new FileStream(LogPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true
            };
        }

        private static string GetFilePath(DateTime time)
        {
            return LogsDirectory + time.ToString(TimeFormat) + "." + FileExtension;
        }

        private static void Log(LogSeverity logSeverity, string source, string message) => LogSilent(logSeverity, source, message);

        private static void LogGuards(string source, string message)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
        }

        private static LogMessage LogSilent(LogSeverity logSeverity, string source, string message)
        {
            LogMessage data = new LogMessage(logSeverity, source, message);
            MessageWriter.WriteLine(data.ToString());
            return data;
        }

        /// <summary>
        ///     Logs an entry with DEBUG severity and checks the debug mode of AtlasLoader.
        /// </summary>
        /// <param name="source">The origin of the entry; who's logging this?</param>
        /// <param name="message">The data of the entry; what's being logged?</param>
        /// <exception cref="ArgumentNullException"><paramref name="source" /> or <paramref name="message" /> is <see langword="null" />.</exception>
        public static void AtlasDebug(string source, string message)
        {
            LogGuards(source, message);
            LogSilent(LogSeverity.Debug, source, message);
        }

        /// <summary>
        ///     Logs an entry with DEBUG severity.
        /// </summary>
        /// <param name="source">The origin of the entry; who's logging this?</param>
        /// <param name="message">The data of the entry; what's being logged?</param>
        /// <exception cref="ArgumentNullException"><paramref name="source" /> or <paramref name="message" /> is <see langword="null" />.</exception>
        public static void Debug(string source, string message)
        {
            LogGuards(source, message);
            Log(LogSeverity.Debug, source, message);
        }

        /// <summary>
        ///     Logs an entry with ERROR severity.
        /// </summary>
        /// <param name="source">The origin of the entry; who's logging this?</param>
        /// <param name="message">The data of the entry; what's being logged?</param>
        /// <exception cref="ArgumentNullException"><paramref name="source" /> or <paramref name="message" /> is <see langword="null" />.</exception>
        public static void Error(string source, string message)
        {
            LogGuards(source, message);
            Log(LogSeverity.Error, source, message);
        }

        /// <summary>
        ///     Logs an entry with ERROR severity but does not run the event.
        /// </summary>
        /// <param name="source">The origin of the entry; who's logging this?</param>
        /// <param name="message">The data of the entry; what's being logged?</param>
        /// <exception cref="ArgumentNullException"><paramref name="source" /> or <paramref name="message" /> is <see langword="null" />.</exception>
        public static void ErrorSilent(string source, string message)
        {
            LogGuards(source, message);
            LogSilent(LogSeverity.Error, source, message);
        }

        /// <summary>
        ///     Logs an entry with INFO severity.
        /// </summary>
        /// <param name="source">The origin of the entry; who's logging this?</param>
        /// <param name="message">The data of the entry; what's being logged?</param>
        /// <exception cref="ArgumentNullException"><paramref name="source" /> or <paramref name="message" /> is <see langword="null" />.</exception>
        public static void Info(string source, string message)
        {
            LogGuards(source, message);
            Log(LogSeverity.Info, source, message);
        }

        /// <summary>
        ///     Logs an entry with INFO severity but does not run the event.
        /// </summary>
        /// <param name="source">The origin of the entry; who's logging this?</param>
        /// <param name="message">The data of the entry; what's being logged?</param>
        /// <exception cref="ArgumentNullException"><paramref name="source" /> or <paramref name="message" /> is <see langword="null" />.</exception>
        public static void InfoSilent(string source, string message)
        {
            LogGuards(source, message);
            LogSilent(LogSeverity.Info, source, message);
        }

        /// <summary>
        ///     Logs an entry with WARNING severity.
        /// </summary>
        /// <param name="source">The origin of the entry; who's logging this?</param>
        /// <param name="message">The data of the entry; what's being logged?</param>
        /// <exception cref="ArgumentNullException"><paramref name="source" /> or <paramref name="message" /> is <see langword="null" />.</exception>
        public static void Warning(string source, string message)
        {
            LogGuards(source, message);
            Log(LogSeverity.Warning, source, message);
        }

        /// <summary>
        ///     Logs an entry with WARNING severity but does not run the event.
        /// </summary>
        /// <param name="source">The origin of the entry; who's logging this?</param>
        /// <param name="message">The data of the entry; what's being logged?</param>
        /// <exception cref="ArgumentNullException"><paramref name="source" /> or <paramref name="message" /> is <see langword="null" />.</exception>
        public static void WarningSilent(string source, string message)
        {
            LogGuards(source, message);
            LogSilent(LogSeverity.Warning, source, message);
        }
    }
}
