using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AtlasLoader
{
    /// <inheritdoc cref="IModule" />
    /// <typeparam name="TModule">The type of the module.</typeparam>
    public abstract class Module<TModule> : IModule where TModule : Module<TModule>
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Local	Setter is used in CoreModule.InitModules.
        /// <summary>
        ///     The single instance of <typeparamref name="TModule" />.
        /// </summary>
        public static TModule Instance { get; private set; }

        private readonly Dictionary<string, Mod> _loadedMods;

        /// <summary>
        ///     The mods that the module is responsible for maintaining.
        /// </summary>
        protected IReadOnlyDictionary<string, Mod> LoadedMods { get; }

        /// <inheritdoc />
        public abstract string AtlasNamespace { get; }

        /// <summary>
        ///     Creates an instance of the module.
        /// </summary>
        protected Module()
        {
            _loadedMods = new Dictionary<string, Mod>();
            LoadedMods = new ReadOnlyDictionary<string, Mod>(_loadedMods);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException"><paramref name="module" /> is <see langword="null"/>.</exception>
        public virtual void LoadModule(IModule module)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }
        }

        /// <inheritdoc />
        public virtual bool PreloadMod(ModPreloadInfo info)
        {
            return true;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException"><paramref name="mod" /> is null.</exception>
        public bool LoadMod(Mod mod)
        {
            if (mod == null)
            {
                throw new ArgumentNullException(nameof(mod));
            }

            if (!_loadedMods.ContainsKey(mod.Id) && BeforeLoadMod(mod))
            {
                _loadedMods.Add(mod.Id, mod);
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException"><paramref name="mod" /> is null.</exception>
        public virtual void AfterLoadMod(Mod mod)
        {
            if (mod == null)
            {
                throw new ArgumentNullException(nameof(mod));
            }
        }

        /// <summary>
        ///     Determines if a mod should load after checking if it has been loaded already.
        /// </summary>
        /// <param name="mod">The mod that would load.</param>
        /// <returns>Whether or not the mod should load.</returns>
        protected virtual bool BeforeLoadMod(Mod mod)
        {
            return true;
        }

        /// <summary>
        ///     Determines if a mod should load after checking if it has been loaded already.
        /// </summary>
        /// <param name="mod">The mod that would load.</param>
        /// <returns>Whether or not the mod should load.</returns>
        protected virtual bool BeforeUnloadMod(Mod mod)
        {
            return true;
        }
    }
}
