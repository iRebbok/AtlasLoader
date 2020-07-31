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
            { IsRequired = true },

            new Option<bool>("--exit",
                getDefaultValue: () => false,
                description: "Determines whether to wait for the button to be clicked before exiting")
            { IsRequired = false },

            new Option<bool>("--verbose",
                getDefaultValue: () => false,
                description: "Defines the debugging of each step")
            { IsRequired = false },

            new Option<DirectoryInfo>("--path",
                description: "Defines the working directory (current by default)")
            { IsRequired = false },
        };

        static GlobalOptions()
        {
            RootCommand.TreatUnmatchedTokensAsErrors = false;
        }

        public WorkMode Mode { get; set; }
        public bool Exit { get; set; }
        public bool Verbose { get; set; }
        public DirectoryInfo Path { get; set; }

        public static async Task Parse(string[] args)
        {
            RootCommand.Handler = CommandHandler.Create<GlobalOptions>(async options => await Program.Start(options).ConfigureAwait(false));
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
            { IsRequired = false },

            new Option<string>(
                new[] { "-o", "--output" },
                description: "Defines the path and name of the dll that will be output")
            { IsRequired = false }
        };

        static ModeOptions()
        {
            RootCommand.TreatUnmatchedTokensAsErrors = false;
        }

        public string Input { get; set; }
        public string Output { get; set; }

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
            { IsRequired = false }
        };

        static PatcherOptions()
        {
            RootCommand.TreatUnmatchedTokensAsErrors = false;
        }

        public PatcherMode Mode { get; set; }

        public static new async Task Parse(GlobalOptions goptions, string[] args)
        {
            RootCommand.Handler = CommandHandler.Create<PatcherOptions>(options => Patcher.Main(goptions, options));
            await RootCommand.InvokeAsync(args).ConfigureAwait(false);
        }
    }
}
