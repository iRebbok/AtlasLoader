using System;
using System.IO;
using System.Threading.Tasks;

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

    internal static class Program
    {
        private static async Task Main() =>
            await GlobalOptions.Parse(Environment.GetCommandLineArgs()).ConfigureAwait(false);

        internal static async Task Start(GlobalOptions options)
        {
            if (options is null)
            {
                Helper.WriteLine("Invalid arguments");
                await GlobalOptions.Parse(new[] { "--help" }).ConfigureAwait(false);
                Helper.needExit = true;
                Helper.Exit(ErrorCode.InvalidArguments);
            }

            Helper.needVerbose = options.Verbose;
            Helper.WriteVerbose("Verbose activated", ConsoleColor.Magenta);
            Helper.WriteVerbose($"Work mode: {options.Mode}", ConsoleColor.Yellow);
            Helper.needExit = options.Exit;
            Helper.WriteVerbose($"Need exit: {options.Exit}", ConsoleColor.Yellow);
            Helper.WriteVerbose($"Working directory: {(!(options.Path is null) ? options.Path.FullName : "null")}", ConsoleColor.Yellow);
            Helper.WriteVerbose($"Working directory exist: {options.Path?.Exists.ToString() ?? "null"}", ConsoleColor.Yellow);
            if (!(options.Path is null) && options.Path.Exists)
            {
                Helper.WriteVerbose($"Setting working directory: {options.Path.FullName}", ConsoleColor.Yellow);
                Directory.SetCurrentDirectory(options.Path.FullName);
            }

            switch (options.Mode)
            {
                case WorkMode.Patcher:
                    await Patcher.Start(options).ConfigureAwait(false);
                    break;
                case WorkMode.Publicizer:
                    await Publicizer.Start(options).ConfigureAwait(false);
                    break;
                default:
                    Helper.WriteLine("Invalid work mode given", ConsoleColor.Red);
                    Helper.Exit(ErrorCode.InvalidMode);
                    break;
            }

            // Thus we allow the waiting
            Helper.Exit(ErrorCode.None);
        }
    }
}
