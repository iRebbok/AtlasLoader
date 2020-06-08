namespace AtlasLoader
{
    /// <summary>
    ///     The importance of the a log message.
    /// </summary>
    public enum LogSeverity : byte
    {
        /// <summary>
        ///     For user's information.
        /// </summary>
        Info,

        /// <summary>
        ///     For a non-fatal error or an indicator that a future error may occur.
        /// </summary>
        Warning,

        /// <summary>
        ///     For fatal errors only.
        /// </summary>
        Error,

        /// <summary>
        ///     For excessive information, always logged to file but opt-in to display to console.
        /// </summary>
        Debug
    }
}
