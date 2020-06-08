using System;

namespace AtlasLoader
{
    /// <summary>
    ///     Data about a specific log message.
    /// </summary>
    public readonly struct LogMessage
    {
        private static readonly Format[] Formats =
        {
            Format.Severity,
            Format.Source,
            Format.Message
        };

        /// <summary>
        ///     The includable information within a formatted log message.
        /// </summary>
        [Flags]
        public enum Format
        {
            /// <summary>
            ///     Includes <seealso cref="LogMessage.Severity" />.
            /// </summary>
            Severity = 1 << 0,

            /// <summary>
            ///     Includes <seealso cref="LogMessage.Source" />.
            /// </summary>
            Source = 1 << 1,

            /// <summary>
            ///     Includes <seealso cref="LogMessage.Message" />.
            /// </summary>
            Message = 1 << 2
        }

        /// <summary>
        ///     Importance of the log message.
        /// </summary>
        public LogSeverity Severity { get; }

        /// <summary>
        ///     Where the log message was created from (often <code>nameof&lt;T&gt;</code>).
        /// </summary>
        public string Source { get; }

        /// <summary>
        ///     The data of the log message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 	The time the log message represents.
        /// </summary>
        public DateTime Time { get; }

        /// <summary>
        ///     Constructs an instance of <see cref="LogMessage" />.
        /// </summary>
        /// <param name="severity">The degree of importance that the log message is.</param>
        /// <param name="source">The name of the type source of the message.</param>
        /// <param name="message">The content of the message.</param>
        /// <exception cref="ArgumentNullException"><paramref name="source" /> or <paramref name="message" /> is <see langword="null"/>.</exception>
        public LogMessage(LogSeverity severity, string source, string message) : this(severity, source, message, DateTime.Now)
        {
        }

        /// <summary>
        ///     Constructs an instance of <see cref="LogMessage" />.
        /// </summary>
        /// <param name="severity">The degree of importance that the log message is.</param>
        /// <param name="source">The name of the type source of the message.</param>
        /// <param name="message">The content of the message.</param>
        /// <param name="time">The time at which this message was created.</param>
        /// <exception cref="ArgumentNullException"><paramref name="source" /> or <paramref name="message" /> is <see langword="null"/>.</exception>
        public LogMessage(LogSeverity severity, string source, string message, DateTime time)
        {
            Severity = severity;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Time = time;
        }

        /// <summary>
        ///     Formats the log message with all the data included.
        /// </summary>
        public override string ToString() => $"[{Time:yyyy-MM-dd hh:mm:ss.fff}] [{Severity}] [{Source}] {Message}";

        private static string AddSpaced(string current, string addition) =>
            current == null
                ? addition
                : current + " " + addition;

        /// <summary>
        ///     Formats the log message with only the data specified by the format flags.
        /// </summary>
        /// <param name="format">The collection of <seealso cref="Format" /> flags that specify what data should be included.</param>
        public string ToString(Format format)
        {
            string result = null;
            foreach (Format possibleFormat in Formats)
            {
                if (!format.HasFlag(possibleFormat))
                {
                    continue;
                }

                switch (possibleFormat)
                {
                    case Format.Severity:
                        result = AddSpaced(result, $"[{Severity}]");
                        break;

                    case Format.Source:
                        result = AddSpaced(result, $"[{Source}]");
                        break;

                    case Format.Message:
                        result = AddSpaced(result, Message);
                        break;
                }
            }

            return result;
        }
    }
}
