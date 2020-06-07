using dnlib.DotNet;
using Synergize;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AtlasLoader
{
    /// <summary>
    ///     Central class of the Atlas framework.
    /// </summary>
    public sealed class CoreModule : Module<CoreModule>
    {
        /// <summary>
        ///     Relative path to the mods directory.
        /// </summary>
        public const string ModsDirectory = RootDirectory + "mods/";

        /// <summary>
        ///     Relative path to the directory containing all of Atlas.
        /// </summary>
        public const string RootDirectory = "Atlas/";

        /// <summary>
        ///     The version of the Atlas assembly, found in <code>AssemblyInfo.cs</code>. Accessible here for ease of use.
        /// </summary>
        public static string AssemblyVersion { get; } = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        /// <summary>
        ///     All of the mods that are completely loaded into Atlas.
        /// </summary>
        public static IEnumerable<Mod> FullyLoadedMods => Instance.LoadedMods.Values;

        /// <summary>
        ///     Whether or not Atlas has fully intialized. If this is false during any load or unload, it is during the first mod load.
        /// </summary>
        public static bool Initialized { get; private set; }

        private static void BootstrapFramework()
        {
            Instance.InitModules();
            Instance.InitMods();
        }

        private static IEnumerable<Type> GetAllTypesSafe(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                Logger.Warning(nameof(CoreModule),
                    $"Failed to load all types of assembly {assembly}. This may be an indication that the assembly is missing dependencies, or similarly, out of date.");

                return e.Types;
            }
        }

        // ReSharper disable once UnusedMember.Local
        private static void Initializer()
        {
            if (Instance != null)
            {
                throw new InvalidOperationException("Already instantiated.");
            }

            try
            {
                PropertyInfo instance = AccessTools.Property<CoreModule>(nameof(Instance)) ??
                                        throw new TypeLoadException(nameof(CoreModule) + " had no \"" + nameof(Instance) + "\" property.");

                MethodInfo instanceSetter = instance.SetMethod ?? throw new TypeResolveException(nameof(CoreModule) + " \"" + nameof(Instance) + "\" property had no setter.");
                instanceSetter.Invoke(null, new object[]
                {
                    new CoreModule()
                });

                BootstrapFramework();

                Initialized = true;

                Logger.AtlasDebug(nameof(CoreModule), "Initialized!");
            }
            catch (Exception e)
            {
                Logger.Error(nameof(CoreModule), $"Error during initialization:{Environment.NewLine}{e}");
            }
        }

        private static bool IsFileReady(string path)
        {
            // If the file can be opened for exclusive access it means that the file
            // is no longer locked by another process.
            try
            {
                using (FileStream inputStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return inputStream.Length > 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private readonly Dictionary<string, Assembly> _assemblyPaths;
        private readonly HashSet<Assembly> _submoduleAssemblies;
        private readonly List<IModule> _submodules;

        /// <inheritdoc />
        public override string AtlasNamespace { get; } = "Core";

        /// <summary>
        ///     All modules currently managed by the core module.
        /// </summary>
        public IReadOnlyList<IModule> Submodules { get; }

        private CoreModule()
        {
            _assemblyPaths = new Dictionary<string, Assembly>();

            _submoduleAssemblies = new HashSet<Assembly>();
            _submodules = new List<IModule>();

            Submodules = new ReadOnlyCollection<IModule>(_submodules);
        }

        private void InitModules()
        {
            foreach (Type type in AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetAllTypesSafe)
                .Where(x => x != null && !x.IsAbstract && x != typeof(CoreModule) && typeof(Module<>).IsAssignableFromGeneric(x))
                .OrderByDescending(x => x.GetCustomAttribute<ModulePriorityAttribute>()?.Priority ?? 0))
            {
                ConstructorInfo ctor = AccessTools.Constructor(type);
                // Private check because NonPublic is just !IsPublic.
                if (ctor == null || !ctor.IsPrivate)
                {
                    throw new TypeLoadException($"Private parameterless constructor not found on \"{type}\".");
                }

                PropertyInfo instance = AccessTools.Property(type, nameof(Instance));
                if (instance == null || instance.GetMethod == null || instance.SetMethod == null || !instance.GetMethod.IsPublic || !instance.SetMethod.IsPrivate)
                {
                    throw new TypeLoadException($"Public static \"{nameof(Instance)}\" property with public getter and private setter not found on \"{type}\".");
                }

                object module = ctor.Invoke(null);
                instance.SetMethod.Invoke(null, new[]
                {
                    module
                });

                _submodules.Add((IModule) module);
                _submoduleAssemblies.Add(type.Assembly);

                Logger.AtlasDebug(nameof(CoreModule), $"Loaded module {type}.");
            }

            foreach (IModule submodule in _submodules)
            {
                try
                {
                    submodule.LoadModule(this);
                }
                catch (Exception e)
                {
                    throw new AggregateException($"Unable to load core module into module {submodule}.", e);
                }

                foreach (IModule otherSubmodule in _submodules)
                {
                    if (submodule == otherSubmodule)
                    {
                        continue;
                    }

                    try
                    {
                        submodule.LoadModule(otherSubmodule);
                    }
                    catch (Exception e)
                    {
                        throw new AggregateException($"Unable to load module {otherSubmodule} into module {submodule}.", e);
                    }
                }
            }
        }

        private void InitMods()
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

        private bool UntilFileIsReady(string path)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (!IsFileReady(path))
            {
                // todo: yaml settings
                if (sw.ElapsedTicks >= 5 * TimeSpan.TicksPerSecond)
                {
                    Logger.Error(nameof(CoreModule), "File lock exceeded max lock length.");
                    return false;
                }
            }

            return true;
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
        public void LoadModAssembly(string path)
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

        private void LoadModAssemblyUnsafe(string path)
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

            Assembly assembly;
            string name;
            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (ModuleDef module = ModuleDefMD.Load(file))
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    module.Write(stream);
                    name = module.Assembly.Name;
                    assembly = Assembly.Load(stream.ToArray());
                }

                if (assembly == null)
                {
                    _assemblyPaths.Add(path, assembly);
                }
            }

            if (name != Path.GetFileNameWithoutExtension(path))
            {
                throw new FormatException($"The file name does not match the assembly name ({name})");
            }

            LoadModAssemblyUnsafe(assembly, name);
        }

        private void LoadModAssemblyUnsafe(Assembly assembly, string modName)
        {
            Logger.AtlasDebug(nameof(CoreModule), $"Loading assembly {assembly}");

            Type modType = null;
            foreach (Type type in GetAllTypesSafe(assembly))
            {
                if (type == null || type.IsAbstract || !typeof(Mod).IsAssignableFrom(type))
                {
                    continue;
                }

                if (modType != null)
                {
                    throw new ArgumentException("Only one non-abstract class can inherit from " + nameof(Mod) + ".");
                }

                modType = type;
            }

            if (modType == null)
            {
                throw new ArgumentException("No mod found.");
            }

            ModLoadInfo loadInfo = new ModLoadInfo(modName, assembly.GetName().Version.ToString(),
                modType.GetCustomAttribute<MetadataAttribute>() ?? new MetadataAttribute());

            foreach (IModule submodule in _submodules)
            {
                bool preloaded;
                try
                {
                    preloaded = submodule.PreloadMod(new ModPreloadInfo(modType, loadInfo));
                }
                catch (Exception e)
                {
                    Logger.Error(nameof(CoreModule), $"{submodule} threw an exception while preloading {modName}. Full exception:{Environment.NewLine}{e}");
                    return;
                }

                if (!preloaded)
                {
                    Logger.AtlasDebug(nameof(CoreModule), $"{submodule} prevented {modName} from preloading.");
                    return;
                }
            }

            ConstructorInfo ctor = modType.GetConstructor(new[]
            {
                typeof(ModLoadInfo)
            });

            if (ctor == null)
            {
                throw new ArgumentException("Standard constructor not found.");
            }

            Mod mod;
            try
            {
                mod = (Mod) ctor.Invoke(new object[]
                {
                    loadInfo
                });
            }
            catch (Exception e)
            {
                throw new TargetInvocationException("Unhandled exception during construction.", e);
            }

            LoadMod(mod);

            Logger.AtlasDebug(nameof(CoreModule), $"Loaded {assembly}.");
        }

        private bool KeepBeforeLoad(Mod mod)
        {
            foreach (IModule submodule in _submodules)
            {
                bool loaded;
                try
                {
                    loaded = submodule.LoadMod(mod);
                }
                catch (Exception e)
                {
                    Logger.Error(nameof(CoreModule), $"{submodule} threw an exception while loading {mod}. Full exception:{Environment.NewLine}{e}");
                    return false;
                }

                if (!loaded)
                {
                    Logger.AtlasDebug(nameof(CoreModule), $"{submodule} prevented {mod} from loading.");
                    return false;
                }
            }

            foreach (IModule submodule in _submodules)
            {
                try
                {
                    submodule.AfterLoadMod(mod);
                }
                catch (Exception e)
                {
                    Logger.Error(nameof(CoreModule), $"{submodule} threw an exception after loading {mod}. Full exception:{Environment.NewLine}{e}");
                }
            }

            try
            {
                mod.InvokeAwake();
            }
            catch (Exception e)
            {
                Logger.Error(nameof(CoreModule), $"{mod} threw an exception while awakening. Full exception:{Environment.NewLine}{e}");
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        protected override bool BeforeLoadMod(Mod mod)
        {
            Logger.AtlasDebug(nameof(CoreModule), $"Loading {mod}...");

            if (!KeepBeforeLoad(mod))
            {
                Logger.AtlasDebug(nameof(CoreModule), $"Loading {mod} was canceled.");

                return false;
            }

            Logger.Info(nameof(CoreModule), $"Loaded {mod}.");
            return true;
        }
    }
}
