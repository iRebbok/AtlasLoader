using System;
using System.IO;

namespace AtlasLoader.CLI
{
    public static class Helper
    {
        public const string Version = "1.0.0";

        public static bool needExit = false;
        public static bool needVerbose = false;

        public static void WriteLine(string message, ConsoleColor color = ConsoleColor.Gray)
        {
            ConsoleColor prevColor = Console.ForegroundColor;

            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = prevColor;
        }

        public static void WriteVerbose(string message, ConsoleColor color = ConsoleColor.Cyan)
        {
            if (!needVerbose)
                return;

            ConsoleColor prevColor = Console.ForegroundColor;

            Console.ForegroundColor = color;
            Console.WriteLine($"[VERBOSE] {message}");
            Console.ForegroundColor = prevColor;
        }

        public static void Exit(ErrorCode errCode = ErrorCode.NotPatched, bool forceWaitForKey = false)
        {
            WriteLine($"Exiting with code {errCode} ({(int) errCode})", ConsoleColor.Magenta);
            if (forceWaitForKey || !needExit)
            {
                WriteLine("Press any key to exit...", ConsoleColor.Green);
                Console.Read();
            }

            Environment.Exit((int) errCode);
        }

        public static byte[] ReadAssembly(string path)
        {
            WriteVerbose($"Reading assembly from: {path}");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                WriteVerbose("Assembly doesn't exist");
                Exit(ErrorCode.AssemblyNotFound);
            }

            WriteVerbose("Checking assembly extension");
            if (Path.GetExtension(path) != ".dll")
            {
                WriteVerbose("It extension isn't equivalent to `.dll`");
                Exit(ErrorCode.InvalidAssemblyExtension);
            }

            WriteVerbose("Trying read assembly");
            byte[] assembly = null;
            try { assembly = File.ReadAllBytes(path); }
            catch (IOException e)
            {
                WriteLine("Failed to read dependencies.", ConsoleColor.Red);
                WriteLine(e.ToString());
                Exit(ErrorCode.UnableToReadAssembly);
            }

            return assembly;
        }
    }
}
