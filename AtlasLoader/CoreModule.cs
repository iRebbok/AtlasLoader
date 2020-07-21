using System;
using System.IO;
using System.Reflection;

namespace AtlasLoader
{
    /// <summary>
    ///     Central class of the AtlasLoader.
    /// </summary>
    public static class CoreModule
    {
        public const string VERSION = "1.0.0";

        /// <summary>
        ///     Relative path to the directory containing all of AtlasLoader.
        /// </summary>
        public const string RootDirectory = "atlasLoader/";

        /// <summary>
        ///     Relative path to the mods directory.
        /// </summary>
        const string ModsDirectory = RootDirectory + "mods/";

        /// <summary>
        ///     Whether or not AtlasLoader has fully intialized. If this is false during any load or unload, it is during the first mod load.
        /// </summary>
        public static bool Initialized { get; private set; }

        /* <---------- Will be called from the assembly ----------> */
        static void Initializer()
        {
            if (Initialized)
            {
                throw new InvalidOperationException("Already instantiated.");
            }

            Initialized = true;

            try
            {
                InitMods();

                Logger.AtlasDebug(nameof(CoreModule), "Initialized!");
            }
            catch (Exception e)
            {
                Logger.Error(nameof(CoreModule), $"Error during initialization:{Environment.NewLine}{e}");
            }
        }

        /// <summary>
        ///     Loads all mods.
        /// </summary>
        static void InitMods()
        {
            if (!Directory.Exists(ModsDirectory))
            {
                return;
            }

            string[] modsToLoad = Directory.GetFiles(ModsDirectory, "*.dll");

            foreach (string mod in modsToLoad)
            {
                try
                {
                    LoadModAssembly(mod);
                }
                catch (Exception e)
                {
                    Logger.Error(nameof(CoreModule), $"{Path.GetFileNameWithoutExtension(mod)} failed to load. Full exception:{Environment.NewLine}{e}");
                }
            }
        }

        /// <summary>
        ///     Loads an assembly from a path.
        ///     This task must be called from the main thread synchronization context.
        /// </summary>
        /// <param name="path">Path to the assembly, relative or full, with extension.</param>
        /// <exception cref="ArgumentException"><paramref name="path" /> is <code>null</code>, empty, or whitespace.</exception>
        /// <exception cref="FileNotFoundException">Could not find file specified by <paramref name="path" />.</exception>
        /// <exception cref="FileLoadException">File was not an assembly (.DLL).</exception>
        /// <return>Whether or not the assembly could be loaded. Note that this is not the same as whether the mod loaded or not.</return>
        static void LoadModAssembly(string path)
        {
            try
            {
                LoadModAssemblyUnsafe(path);
            }
            catch (Exception e)
            {
                Logger.Error(nameof(CoreModule), $"{Path.GetFileNameWithoutExtension(path)} failed to load. Full exception:{Environment.NewLine}{e}");
            }
        }

        static void LoadModAssemblyUnsafe(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Empty or whitespace paths are invalid.", nameof(path));
            }

            path = Path.GetFullPath(path);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Could not find the assembly specified.", path);
            }

            if (Path.GetExtension(path) != ".dll")
            {
                throw new FileLoadException("File is not an assembly.", path);
            }

            Assembly assembly = Assembly.Load(File.ReadAllBytes(path));
            LoadModAssemblyUnsafe(assembly);
        }

        static void LoadModAssemblyUnsafe(Assembly assembly)
        {
            Logger.AtlasDebug(nameof(CoreModule), $"Loading assembly {assembly}");

            var mainAttribute = assembly.GetCustomAttribute<ModDefineAttribute>();
            if (mainAttribute is null)
                throw new ArgumentException($"{assembly.GetName().Name} doesn't implement the mod attribute", nameof(ModDefineAttribute));

            var modType = mainAttribute.EntryPoint;
            if (modType is null)
                throw new ArgumentNullException($"{assembly.GetName().Name} doesn't implement entry point", nameof(ModDefineAttribute.EntryPoint));
            // why?
            // this way can resolve missing dependencies
            else if (modType.BaseType != typeof(Mod))
                throw new ArgumentException($"{modType.Name} not a basic type of {nameof(Mod)}");

            ModLoadInfo loadInfo = new ModLoadInfo(mainAttribute.Id, assembly.GetName().Version.ToString(),
                modType.GetCustomAttribute<MetadataAttribute>() ?? new MetadataAttribute());

            var ctor = modType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.CreateInstance,
                null, new[] { typeof(ModLoadInfo) }, null);
            if (ctor is null)
                throw new ArgumentException("Standard constructor not found.");

            Mod? mod;
            try
            {
                mod = ctor.Invoke(new object[] { loadInfo }) as Mod;
            }
            catch (Exception e)
            {
                throw new TargetInvocationException("Unhandled exception during construction.", e);
            }

            LoadMod(mod!);

            Logger.AtlasDebug(nameof(CoreModule), $"Loaded {assembly}.");
        }

        static void LoadMod(Mod mod)
        {
            try
            {
                mod.InvokeAwake();
            }
            catch (Exception e)
            {
                mod.Error($"Awake call error");
                mod.Error(e.ToString());
            }
        }
    }
}
