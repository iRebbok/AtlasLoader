using System;

namespace AtlasLoader
{
    public class LogProvider
    {
        /// <inheritdoc />
        public Mod Owner { get; }

        /// <summary>
        ///     Constructs an instance of <seealso cref="LogProvider" />.
        /// </summary>
        /// <param name="owner">The parent mod of this child.</param>
        /// <exception cref="ArgumentNullException"><paramref name="owner" /> is <see langword="null"/>.</exception>
        public LogProvider(Mod owner)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>
        ///     Shortcut to <see cref="Logger" />'s info method.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="message" /> is <see langword="null"/>.</exception>
        public void Info(string message) => Logger.Info(Owner.Id, message ?? throw new ArgumentNullException(nameof(message)));

        /// <summary>
        ///     Shortcut to <see cref="Logger" />'s warn method.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="message" /> is <see langword="null"/>.</exception>
        public void Warn(string message) => Logger.Warning(Owner.Id, message ?? throw new ArgumentNullException(nameof(message)));

        /// <summary>
        ///     Shortcut to <see cref="Logger" />'s error method.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="message" /> is <see langword="null"/>.</exception>
        public void Error(string message) => Logger.Error(Owner.Id, message ?? throw new ArgumentNullException(nameof(message)));

        /// <summary>
        ///     Shortcut to <see cref="Logger" />'s debug method, only called if the <see cref="Mod.Settings" /> has debug set to <code>true</code>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="message" /> is <see langword="null"/>.</exception>
        public void Debug(string message)
        {
            // todo: yaml settings
            Logger.Debug(Owner.Id, message ?? throw new ArgumentNullException(nameof(message)));
        }
    }
}
