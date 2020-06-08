using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AtlasLoader.CLI
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
        InvalidDependency = 11,
        InvalidAssemblyExtension = 12,

        // Up to 64 is reserved for immediate-return errors.

        UnableToWriteTemp = 1 << 6,
        BuildException = 1 << 7,
        UnableToDeleteAssembly = 1 << 8,
        UnableToChangeTemp = 1 << 9
    }

    public enum WorkMode
    {
        Publicizer,
        Patcher
    }

    class Program
    {
        static WorkMode _workMode;

        static void Main()
        {
            var args = Environment.GetCommandLineArgs();

            // Args cannot be empty
            if (args.Length == 0)
            {
                Helper.WriteLine("Invalid arguments.", ConsoleColor.Red);
                Helper.Exit(ErrorCode.InvalidArguments);
            }

            var globalArgs = Options.GetGobalOptions();
            globalArgs.Handler = CommandHandler.Create<string, bool, bool, DirectoryInfo, IEnumerable<string>?>((workMode, exit, verbose, path, dependencies) =>
            {
                Helper.needVerbose = verbose;
                Helper.WriteVerbose("Verbose activated", ConsoleColor.Magenta);

                Helper.WriteVerbose($"Source workMode: {workMode}", ConsoleColor.Yellow);
                _workMode = Enum.Parse<WorkMode>(workMode, true);
                Helper.WriteVerbose($"Parsed workMode: {_workMode} ({(int) _workMode})", ConsoleColor.Yellow);
                Helper.needExit = exit;
                Helper.WriteVerbose($"Need exit: {exit}", ConsoleColor.Yellow);

                Helper.WriteVerbose($"Working directory: {(!(path is null) ? path.FullName : "null")}", ConsoleColor.Yellow);
                Helper.WriteVerbose($"Working directory exist: {path?.Exists.ToString() ?? "null"}", ConsoleColor.Yellow);
                if (!(path is null) && path.Exists)
                {
                    Helper.WriteVerbose($"Setting working directory: {path.FullName}", ConsoleColor.Yellow);
                    Directory.SetCurrentDirectory(path.FullName);
                }
                if (dependencies is null)
                    Helper.WriteVerbose($"None dependencies", ConsoleColor.Yellow);
                else
                {
                    Helper.WriteVerbose($"Dependencies: {string.Join(", ", dependencies)}");
                    LocalDependencies(dependencies);
                }
            });

            globalArgs.Invoke(args);

            switch (_workMode)
            {
                case WorkMode.Patcher:
                    Patcher.Start(args);
                    break;
                case WorkMode.Publicizer:
                    Publicizer.Start(args);
                    break;
            }

            // Thus we allow the waiting
            Helper.Exit(ErrorCode.None);
        }

        static void LocalDependencies(IEnumerable<string> dependencies)
        {
            foreach (string dependency in dependencies!)
            {
                string fullDependency = Path.GetFullPath(dependency);
                Helper.WriteVerbose($"Dependency full path: {fullDependency}");
                if (File.Exists(fullDependency))
                {
                    Helper.WriteVerbose("Dependency exist");
                    Assembly.Load(File.ReadAllBytes(fullDependency));
                }
                else if (Directory.Exists(fullDependency))
                {
                    Helper.WriteVerbose("Dependency is a folder");
                    Dictionary<string, Assembly> nameAssemblies = Directory.GetFiles(fullDependency, "*.dll").Select(x => Assembly.Load(File.ReadAllBytes(x)))
                        .ToDictionary(x => x.GetName().FullName, x => x);
                    AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) => nameAssemblies.TryGetValue(eventArgs.Name!, out Assembly? assembly) ? assembly : null;
                }
                else
                {
                    Helper.WriteVerbose("Dependency doesn't exist");
                    Helper.WriteLine($"Invalid dependency: \"{dependency}\"");
                    Helper.Exit(ErrorCode.InvalidDependency);
                }
            }
        }
    }
}
