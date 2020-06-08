using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

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
        public const string TimeFormat = "yyyy-MM-dd HH-mm-ss-fff";

        private static readonly StreamWriter? MessageWriter;
        private static readonly Stack<LogMessage>? MessageBuffer;

        /// <summary>
        /// 	The path to the log file.
        /// </summary>
        public static string? LogPath { get; }

        /// <summary>
        /// 	The past messages in order of most recent to least recent.
        /// </summary>
        public static IEnumerable<LogMessage> Messages => MessageBuffer!;

        /// <summary>
        /// 	The count of <seealso cref="Messages"/>.
        /// </summary>
        public static int MessageCount => MessageBuffer!.Count;

        static Logger()
        {
            if (!Directory.Exists(LogsDirectory))
            {
                Directory.CreateDirectory(LogsDirectory);
            }
            else
            {
                CompressLatest();

                LogPath = GetFilePath(DateTime.Now);
                MessageWriter = new StreamWriter(new FileStream(LogPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    AutoFlush = true
                };
                MessageBuffer = new Stack<LogMessage>(500);
            }
        }

        private static void CompressLatest()
        {
            FileStream? latest = GetLatestUnlocked(out string? path);
            if (latest is null)
                return;

            using (latest)
            {
                AtlasDebug(nameof(Logger), "Compressing previous log file: " + path);

                using (latest)
                using (FileStream compressed = new FileStream(GetFilePath(File.GetCreationTime(path)) + ".gz", FileMode.Create, FileAccess.Write, FileShare.None))
                using (GZipStream zip = new GZipStream(compressed, CompressionLevel.Optimal))
                {
                    latest.CopyTo(zip);
                }
            }

            File.Delete(path);

            AtlasDebug(nameof(Logger), "Compressed log file: " + path);
        }

        private static string GetFilePath(DateTime time)
        {
            return LogsDirectory + time.ToString(TimeFormat) + "." + FileExtension;
        }

        private static FileStream? GetLatestUnlocked(out string? path)
        {
            foreach (string file in Directory.EnumerateFiles(LogsDirectory, "*." + FileExtension))
            {
                try
                {
                    FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);

                    path = file;
                    return stream;
                }
                catch (IOException)
                {
                    // Locked.
                }
            }

            path = null;
            return null;
        }

        private static void Log(LogSeverity logSeverity, string source, string message) => LogEvent(LogSilent(logSeverity, source, message));

        private static void LogEvent(LogMessage data)
        {
            try
            {
                Invoked?.Invoke(data);
            }
            catch (Exception e)
            {
                LogSilent(LogSeverity.Error, nameof(Logger), $"Error while running the log event:{Environment.NewLine}{e}");
            }
        }

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

            MessageWriter?.WriteLine(data.ToString());
            MessageBuffer?.Push(data);

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

            LogMessage data = LogSilent(LogSeverity.Debug, source, message);

            // Getting rid of the settings in the form .json and log debug by default
            LogEvent(data);
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

        /// <summary>
        ///     Fired when a message is logged.
        /// </summary>
        public static event Action<LogMessage>? Invoked;
    }
}
