namespace AtlasLoader
{
    /// <summary>
    ///     A module of the Atlas framework.
    /// </summary>
    public interface IModule
    {
        /// <summary>
        ///     The name of the settings file after the Atlas namespace in the format of <code>Atlas.</code><seealso cref="AtlasNamespace" /><code>.json</code>.
        /// </summary>
        string AtlasNamespace { get; }

        /// <summary>
        ///     Loads a sibling module into the module and holds the module responsible for accompanying it.
        /// </summary>
        /// <param name="module">The sibling module to load into this module.</param>
        void LoadModule(IModule module);

        /// <summary>
        ///     Verifies that the mod can be loaded into the module.
        /// </summary>
        /// <param name="info">Information about the mod that will be loaded.</param>
        /// <returns>Whether or not the module will accept the mod.</returns>
        bool PreloadMod(ModPreloadInfo info);

        /// <summary>
        ///     Loads a mod into the module and holds the module responsible for maintaining it.
        /// </summary>
        /// <param name="mod">The mod to load into the module.</param>
        /// <returns>Whether or not the mod successfully loaded (if not, it was already loaded).</returns>
        bool LoadMod(Mod mod);

        /// <summary>
        /// 	Performed after the mod passes through all modules' <seealso cref="LoadMod"/> implementations.
        /// </summary>
        /// <param name="mod">The mod that was loaded into the modules.</param>
        void AfterLoadMod(Mod mod);
    }
}
