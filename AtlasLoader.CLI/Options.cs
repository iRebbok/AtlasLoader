using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;

namespace AtlasLoader.CLI
{
    public sealed class GlobalOptions
    {
        public static readonly RootCommand RootCommand = new RootCommand
        {
            new Option<WorkMode>("--work-mode",
                description: "Defines what the CLI will use")
            { Required = true },

            new Option<bool>("--exit",
                getDefaultValue: () => false,
                description: "Determines whether to wait for the button to be clicked before exiting")
            { Required = false },

            new Option<bool>("--verbose",
                getDefaultValue: () => false,
                description: "Defines the debugging of each step")
            { Required = false },

            new Option<DirectoryInfo>("--path",
                description: "Defines the working directory (current by default)")
            { Required = false },
        };

        static GlobalOptions()
        {
            RootCommand.TreatUnmatchedTokensAsErrors = false;
        }

        public readonly WorkMode Mode;
        public readonly bool Exit;
        public readonly bool Verbose;
        public readonly DirectoryInfo Path;

        public GlobalOptions(WorkMode mode, bool exit, bool verbose, DirectoryInfo path)
        {
            Mode = mode;
            Exit = exit;
            Verbose = verbose;
            Path = path;
        }

        public static async Task Parse(string[] args)
        {
            RootCommand.Handler = CommandHandler.Create<GlobalOptions>(async options => await Program.Main(options).ConfigureAwait(false));
            await RootCommand.InvokeAsync(args).ConfigureAwait(false);
        }
    }

    public class ModeOptions
    {
        public static readonly RootCommand RootCommand = new RootCommand
        {
            new Option<string>(
                new[] { "-i", "--input" },
                getDefaultValue: () => "Assembly-CSharp.dll",
                description: "Defines the path and name of the dll to be used")
            { Required = false },

            new Option<string>(
                new[] { "-o", "--output" },
                description: "Defines the path and name of the dll that will be output")
            { Required = false }
        };

        static ModeOptions()
        {
            RootCommand.TreatUnmatchedTokensAsErrors = false;
        }

        public readonly string Input;
        public readonly string Output;

        public ModeOptions(string input, string output)
        {
            Input = input;
            Output = output;
        }

        public static async Task Parse(GlobalOptions goptions, string[] args)
        {
            RootCommand.Handler = CommandHandler.Create<ModeOptions>(options => Publicizer.Main(goptions, options));
            await RootCommand.InvokeAsync(args).ConfigureAwait(false);
        }
    }

    public sealed class PatcherOptions : ModeOptions
    {
        public static new readonly RootCommand RootCommand = new RootCommand
        {
            ModeOptions.RootCommand,

            new Option<PatcherMode>(
                "--patcher-mode",
                getDefaultValue: () => PatcherMode.Patch,
                description: "Defines the patch mode to will be used")
            { Required = false }
        };

        static PatcherOptions()
        {
            RootCommand.TreatUnmatchedTokensAsErrors = false;
        }

        public readonly PatcherMode Mode;

        public PatcherOptions(string input, string output, PatcherMode mode) : base(input, output)
        {
            Mode = mode;
        }

        public static new async Task Parse(GlobalOptions goptions, string[] args)
        {
            RootCommand.Handler = CommandHandler.Create<PatcherOptions>(options => Patcher.Main(goptions, options));
            await RootCommand.InvokeAsync(args).ConfigureAwait(false);
        }
    }
}
