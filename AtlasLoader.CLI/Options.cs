using System.Collections.Generic;
using System.CommandLine;
using System.IO;

namespace AtlasLoader.CLI
{
    public static class Options
    {
        public static RootCommand GetGobalOptions()
        {
            var root = new RootCommand
            {
                new Option<string>("--work-mode",
                description: "Defines what the CLI will use (publicizer, patcher)")
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

                new Option<IEnumerable<string>?>("--dependencies",
                description: "Defines the download dependencies that will be loaded")
                { Required = false }
            };

            root.TreatUnmatchedTokensAsErrors = false;
            return root;
        }

        public static RootCommand GetBaseModeOptions()
        {
            var root = new RootCommand
            {
                new Option<string>(new[] { "-i", "--input" },
                getDefaultValue: () => "Assembly-CSharp.dll",
                description: "Defines the path and name of the dll to be used")
                { Required = false },

                new Option<string>(new[] { "-o", "--output" },
                description: "Defines the path and name of the dll that will be output")
                { Required = false }
            };

            root.TreatUnmatchedTokensAsErrors = false;
            return root;
        }

        public static RootCommand GetPatcherOptions()
        {
            var @base = GetBaseModeOptions();
            @base.TreatUnmatchedTokensAsErrors = false;
            @base.AddOption(new Option<string>("--patcher-mode",
                getDefaultValue: () => PatcherMode.Patch.ToString().ToLowerInvariant(),
                description: "Defines the patch mode to will be used")
            { Required = false });
            return @base;
        }
    }
}
