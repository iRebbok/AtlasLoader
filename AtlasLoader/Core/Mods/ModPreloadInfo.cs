using System;

namespace AtlasLoader
{
    /// <summary>
    ///     Information about a mod before it is loaded.
    /// </summary>
    public readonly struct ModPreloadInfo
    {
        /// <summary>
        ///     The type of the mod.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        ///     The load info that will be used if the preload succeeds.
        /// </summary>
        public ModLoadInfo LoadInfo { get; }

        /// <summary>
        ///     Constructs an instance of <see cref="ModPreloadInfo" />.
        /// </summary>
        /// <param name="type">The type of the mod.</param>
        /// <param name="loadInfo">The load info that would be used.</param>
        /// <exception cref="ArgumentNullException"><paramref name="type" /> is <see langword="null"/>.</exception>
        public ModPreloadInfo(Type type, ModLoadInfo loadInfo)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            LoadInfo = loadInfo;
        }
    }
}
