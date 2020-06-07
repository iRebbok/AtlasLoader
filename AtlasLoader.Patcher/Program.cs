using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AtlasLoader.Patcher
{
    internal class Program
    {
        [Flags]
        public enum ErrorCode
        {
            None = 0,
            InvalidArguments = 1, // Too little or too many arguments.
            AssemblyNotFound = 2, // Could not find the assembly file.
            UnableToReadAssembly = 3, // Could not open the assembly file.
            InvalidMode = 6, // Mode does not exist.
            NotPatched = 7, // Assembly file is not already patched.
            AlreadyPatched = 8, // Assembly file is already patched and patching is not forced.
            InvalidPatchers = 9,
            PatchException = 10,
            UnableToReadDependenciesList = 11,
            InvalidDependency = 12,

            // Up to 64 is reserved for immediate-return errors.

            UnableToWriteTemp = 1 << 6,
            BuildException = 1 << 7,
            UnableToDeleteAssembly = 1 << 8,
            UnableToChangeTemp = 1 << 9
        }

        private static Type PatcherType
        {
            get
            {
                Type[] patches = Patcher.Implementations.ToArray();
                if (patches.Length == 0)
                {
                    throw new InvalidOperationException("No patch type found. Install a patcher library.");
                }

                if (patches.Length > 1)
                {
                    throw new InvalidOperationException("Too many patch types. Only install the patcher library for the application you are patching.");
                }

                return patches[0];
            }
        }

        private static void LoadAssemblies()
        {
            foreach (string patcher in Directory.GetFiles(Environment.CurrentDirectory, "Atlas.Patcher.*.dll"))
            {
                Assembly.Load(File.ReadAllBytes(patcher));
            }
        }

        private static Dictionary<string, string> SwitchesFromArguments(IEnumerable<string> arguments, IReadOnlyDictionary<string, string> shortToVerbose, out List<string> args)
        {
            Dictionary<string, string> switches = new Dictionary<string, string>();
            args = new List<string>();

            string curSwitch = null;
            foreach (string arg in arguments)
            {
                if (arg.StartsWith("--"))
                {
                    curSwitch = arg.Substring(2);
                }
                else if (shortToVerbose != null && arg.StartsWith("-"))
                {
                    shortToVerbose.TryGetValue(arg.Substring(1), out curSwitch);
                }
                else if (curSwitch != null)
                {
                    switches.Add(curSwitch, arg);
                    curSwitch = null;
                }
                else
                {
                    args.Add(arg);
                }
            }

            return switches;
        }

        private static void WriteLine(string message, ConsoleColor color = ConsoleColor.Gray)
        {
            ConsoleColor prevColor = Console.ForegroundColor;

            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = prevColor;
        }

        public static int Main(string[] argsArray)
        {
            ErrorCode error = ErrorCode.None;

            Dictionary<string, string> switches = SwitchesFromArguments(argsArray, new Dictionary<string, string>
            {
                {
                    "m", "mode"
                },
                {
                    "p", "path"
                }
            }, out List<string> args);

            if (args.Count != 0)
            {
                WriteLine("Invalid arguments.", ConsoleColor.Red);
                return (int) ErrorCode.InvalidArguments;
            }

            const string dependenciesFile = "dependencies.txt";
            if (File.Exists(dependenciesFile))
            {
                string[] dependencies;
                try
                {
                    dependencies = File.ReadAllLines(dependenciesFile);
                }
                catch (IOException)
                {
                    WriteLine("Failed to read dependencies.", ConsoleColor.Red);
                    return (int) ErrorCode.UnableToReadDependenciesList;
                }

                foreach (string dependency in dependencies)
                {
                    string fullDependency = Path.GetFullPath(dependency);
                    if (File.Exists(fullDependency))
                    {
                        Assembly.Load(File.ReadAllBytes(fullDependency));
                    }
                    else if (Directory.Exists(fullDependency))
                    {
                        Dictionary<string, Assembly> nameAssemblies = Directory.GetFiles(fullDependency, "*.dll").Select(x => Assembly.Load(File.ReadAllBytes(x)))
                            .ToDictionary(x => x.GetName().FullName, x => x);
                        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) => nameAssemblies.TryGetValue(eventArgs.Name, out Assembly assembly) ? assembly : null;
                    }
                    else
                    {
                        WriteLine($"Invalid dependency: \"{dependency}\"");
                        return (int) ErrorCode.InvalidDependency;
                    }
                }
            }

            LoadAssemblies();

            Type patcherType;
            try
            {
                patcherType = PatcherType;
            }
            catch (InvalidOperationException e)
            {
                WriteLine($"Invalid patchers: {e.Message}");
                return (int) ErrorCode.InvalidPatchers;
            }

            Patcher patcher = (Patcher) Activator.CreateInstance(patcherType);

            if (!switches.TryGetValue("path", out string path))
            {
                path = patcher.DefaultPath;
            }

            if (!File.Exists(path))
            {
                WriteLine("Assembly not found.", ConsoleColor.Red);
                return (int) ErrorCode.AssemblyNotFound;
            }

            byte[] assemblyData;
            try
            {
                assemblyData = File.ReadAllBytes(path);
            }
            catch
            {
                WriteLine("Failed to read the assembly.", ConsoleColor.Red);
                return (int) ErrorCode.UnableToReadAssembly;
            }

            string tempPatchedFilePath = Path.ChangeExtension(path, ".tmp");

            FileStream tempPatchedFile;
            using (ModuleDefMD module = ModuleDefMD.Load(assemblyData))
            {
                patcher.Module = module;

                switches.TryGetValue("mode", out string mode);
                switch (mode ?? "patch")
                {
                    case "patch":
                        {
                            if (patcher.Info() != null)
                            {
                                WriteLine("Assembly is already patched.", ConsoleColor.Red);
                                return (int) ErrorCode.AlreadyPatched;
                            }

                            try
                            {
                                patcher.Patch(path);
                            }
                            catch (Exception e)
                            {
                                WriteLine($"Failed to patch:{Environment.NewLine}{e}", ConsoleColor.Red);
                                return (int) ErrorCode.PatchException;
                            }

                            break;
                        }

                    case "forcepatch":
                        {
                            if (patcher.Info() != null)
                            {
                                WriteLine("Assembly is already patched. Proceeding to wipe and patch.", ConsoleColor.Yellow);
                                patcher.Unpatch(path);
                            }

                            try
                            {
                                patcher.Patch(path);
                            }
                            catch (Exception e)
                            {
                                WriteLine($"Failed to patch:{Environment.NewLine}{e}", ConsoleColor.Red);
                                return (int) ErrorCode.PatchException;
                            }

                            break;
                        }

                    case "repatch":
                        {
                            if (patcher.Info() == null)
                            {
                                WriteLine("Assembly is not patched.", ConsoleColor.Red);
                                return (int) ErrorCode.NotPatched;
                            }

                            try
                            {
                                patcher.Unpatch(path);
                                patcher.Patch(path);
                            }
                            catch (Exception e)
                            {
                                WriteLine($"Failed to patch:{Environment.NewLine}{e}", ConsoleColor.Red);
                                return (int) ErrorCode.PatchException;
                            }

                            break;
                        }

                    case "unpatch":
                        {
                            if (patcher.Info() == null)
                            {
                                WriteLine("Assembly is not patched.", ConsoleColor.Red);
                                return (int) ErrorCode.NotPatched;
                            }

                            try
                            {
                                patcher.Unpatch(path);
                            }
                            catch (Exception e)
                            {
                                WriteLine($"Failed to patch:{Environment.NewLine}{e}", ConsoleColor.Red);
                                return (int) ErrorCode.PatchException;
                            }

                            break;
                        }

                    case "info":
                        {
                            PatchedAttribute info = patcher.Info();
                            if (info == null)
                            {
                                WriteLine("Assembly is not patched.", ConsoleColor.Red);
                                return (int) ErrorCode.NotPatched;
                            }

                            WriteLine("Acquired patch info:", ConsoleColor.Green);
                            WriteLine($"- Version: {info.Version}{Environment.NewLine}" +
                                      $"- Start index: {info.StartIndex}{Environment.NewLine}" +
                                      $"- End index: {info.EndIndex}{Environment.NewLine}");

                            return (int) ErrorCode.None;
                        }

                    default:
                        {
                            WriteLine("Invalid mode.", ConsoleColor.Red);
                            return (int) ErrorCode.InvalidMode;
                        }
                }

                try
                {
                    tempPatchedFile = new FileStream(tempPatchedFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
                }
                catch
                {
                    WriteLine("Unable to create the temporary assembly.");
                    return (int) ErrorCode.UnableToWriteTemp;
                }

                try
                {
                    module.Write(tempPatchedFile);
                }
                catch (Exception e)
                {
                    WriteLine($"An error occurred during the write operation; a build-time error has occured:{Environment.NewLine}{e}", ConsoleColor.Red);
                    error |= ErrorCode.BuildException;
                }
            }

            bool empty = tempPatchedFile.Length == 0;
            tempPatchedFile.Dispose();

            if (error.HasFlag(ErrorCode.BuildException) && empty)
            {
                try
                {
                    File.Delete(tempPatchedFilePath);
                }
                catch
                {
                    WriteLine("Unable to delete the temporary assembly (attempted to clean up after the inability to build).", ConsoleColor.Red);
                    error |= ErrorCode.UnableToChangeTemp;
                }
            }
            else
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception)
                {
                    WriteLine("Unable to delete the assembly.", ConsoleColor.Red);

                    try
                    {
                        File.Delete(tempPatchedFilePath);
                    }
                    catch
                    {
                        WriteLine("Unable to delete the temporary assembly (attempted to clean up after the inability to delete the assembly).", ConsoleColor.Red);
                        error |= ErrorCode.UnableToChangeTemp;
                    }

                    return (int) error;
                }

                try
                {
                    File.Move(tempPatchedFilePath, path);
                }
                catch
                {
                    WriteLine("Unable to rename the temporary assembly.", ConsoleColor.Red);
                    return (int) ErrorCode.UnableToChangeTemp;
                }

                WriteLine("Successfully patched the assembly on disk.", ConsoleColor.Green);
            }

            return (int) error;
        }
    }
}
