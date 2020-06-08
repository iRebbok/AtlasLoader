using System;

namespace AtlasLoader
{
    /// <summary>
    ///     Class that modifies the original behavior of the modded application, on top of the framework. Only one mod may be in a single assembly.
    /// </summary>
    public abstract class Mod
    {
        /// <summary>
        ///     The log handler for this mod. Shortcut methods are already provided, but this can be passed to give log access to other objects.
        /// </summary>
        protected LogProvider Logs { get; }

        /// <summary>
        ///     The unique ID of the mod, determined by the assembly and file name.
        /// </summary>
        public string Id { get; }

        /// <summary>
        ///     The info attribute placed on this mod.
        /// </summary>
        public MetadataAttribute Metadata { get; }

        /// <summary>
        ///     The version of the mod, determined by the assembly version.
        /// </summary>
        public string Version { get; }

        /// <summary>
        ///     Constructs an instance of <see cref="Mod" />.
        /// </summary>
        /// <param name="data">All of the information that the mod needs to initialize its own metadata properties.</param>
        protected Mod(ModLoadInfo data)
        {
            Id = data.Id;
            Metadata = data.Metadata;
            Version = data.Version;

            Logs = new LogProvider(this);
        }

        internal void InvokeAwake() => Awake();

        /// <summary>
        ///     A framework hook, called after this mod is added to modules. Use this method to initialize this mod by interacting with modules.
        /// </summary>
        protected virtual void Awake() { }

        /// <summary>
        ///     Shortcut to <seealso cref="Logs" />'s info method.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="message" /> is <see langword="null"/>.</exception>
        public void Info(string message) => Logs.Info(message ?? throw new ArgumentNullException(nameof(message)));

        /// <summary>
        ///     Shortcut to <seealso cref="Logs" />'s warn method.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="message" /> is <see langword="null"/>.</exception>
        public void Warn(string message) => Logs.Warn(message ?? throw new ArgumentNullException(nameof(message)));

        /// <summary>
        ///     Shortcut to <seealso cref="Logs" />'s error method.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="message" /> is <see langword="null"/>.</exception>
        public void Error(string message) => Logs.Error(message ?? throw new ArgumentNullException(nameof(message)));

        /// <summary>
        ///     Shortcut to <seealso cref="Logs" />'s debug method, only called if the <seealso cref="ModSettings" /> has debug set to <code>true</code>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="message" /> is <see langword="null"/>.</exception>
        public void Debug(string message) => Logs.Debug(message ?? throw new ArgumentNullException(nameof(message)));

        /// <summary>
        ///     Formats the ID and name into a single string.
        /// </summary>
        public override string ToString() => Metadata.Name == null ? Id : $"{Metadata.Name} ({Id})";
    }
}
