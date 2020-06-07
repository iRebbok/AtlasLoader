using System;

namespace AtlasLoader
{
    /// <summary>
    ///     All of the information that a mod needs to initialize its own metadata properties.
    /// </summary>
    public readonly struct ModLoadInfo
    {
        /// <summary>
        ///     The unique ID of a mod, determined by the assembly and file name.
        /// </summary>
        public string Id { get; }

        /// <summary>
        ///     The version of a mod, determined by the assembly version.
        /// </summary>
        public string Version { get; }

        /// <summary>
        ///     The info attribute placed on a mod.
        /// </summary>
        public MetadataAttribute Metadata { get; }

        /// <summary>
        ///     Constructs an instance of <see cref="ModLoadInfo" />.
        /// </summary>
        /// <param name="id">The unique ID of the mod.</param>
        /// <param name="version">The version of the mod.</param>
        /// <param name="metadata">The self-appointed metadata of the mod.</param>
        /// <exception cref="ArgumentNullException"><paramref name="id" /> or <paramref name="metadata" /> is <see langword="null"/>.</exception>
        public ModLoadInfo(string id, string version, MetadataAttribute metadata)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Version = version;
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }
    }
}
