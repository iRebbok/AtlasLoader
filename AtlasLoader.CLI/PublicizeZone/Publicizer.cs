using dnlib.DotNet;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;

namespace AtlasLoader.CLI
{
    public static class Publicizer
    {
        static ulong publicizedFields = 0;
        static ulong publicizedMethods = 0;
        static ulong publicizedClasses = 0;
        static ulong publicizedProperties = 0;

        static string _input;
        static string _output;

        public static void Start(string[] args)
        {
            Helper.WriteVerbose("Switching to publicizer", System.ConsoleColor.Green);

            var baseArgs = Options.GetBaseModeOptions();
            baseArgs.Handler = CommandHandler.Create<string, string>((input, output) =>
            {
                Helper.WriteVerbose($"Input: {input}", System.ConsoleColor.Yellow);
                _input = input;
                Helper.WriteVerbose($"Written output: {output}", System.ConsoleColor.Yellow);
                _output = !string.IsNullOrEmpty(output) ? output : $"{Path.GetFileNameWithoutExtension(input)}_publicized{Path.GetExtension(input)}";
                Helper.WriteVerbose($"Final output: {_output}", System.ConsoleColor.Yellow);
            });

            baseArgs.Invoke(args);

            var assembly = Helper.ReadAssembly(_input);

            Helper.WriteVerbose("Loading assembly as module");
            var module = ModuleDefMD.Load(assembly);
            Helper.WriteVerbose("Getting assembly types");
            foreach (var type in module.GetTypes())
                Publicize(type);
            Helper.WriteVerbose("Finalize publicizing");

            var publicizedDirectory = Path.GetDirectoryName(_output);
            Helper.WriteVerbose($"Publicized result directory: {publicizedDirectory}");
            if (!string.IsNullOrEmpty(publicizedDirectory) && !Directory.Exists(publicizedDirectory))
            {
                Helper.WriteVerbose($"Publicized directory doesn't exist, creating...", System.ConsoleColor.Cyan);
                Directory.CreateDirectory(publicizedDirectory);
            }

            Helper.WriteLine(string.Join("\n", new[]
            {
                "Publicize result -",
                $"Publicized classes: {publicizedClasses}",
                $"Publicized methods: {publicizedMethods}",
                $"Publicized fields: {publicizedFields}",
                $"Publicized properties: {publicizedProperties}"
            }), System.ConsoleColor.Magenta);
            Helper.WriteVerbose($"Writing publicized assembly to path: {_output}");

            module.Write(_output);
        }

        static void Publicize(TypeDef typeDef)
        {
            if (typeDef is null)
            {
                Helper.WriteVerbose($"Skipping nullable typedef");
                return;
            }

            if (typeDef.IsNotPublic)
            {
                Helper.WriteVerbose($"Publicizing class: {typeDef.FullName}");
                typeDef.Visibility = typeDef.IsNestedPrivate ? TypeAttributes.NestedPublic : TypeAttributes.Public;
                publicizedClasses++;
            }

            if (typeDef.HasFields)
                foreach (var field in typeDef.Fields)
                {
                    if (field is null || field.IsCompilerControlled)
                    {
                        Helper.WriteVerbose("Skipping nullable field...");
                        continue;
                    }

                    if (!field.IsPublic)
                    {
                        Helper.WriteVerbose($"Publicizing field: {field.FullName}");
                        var result = FieldAttributes.Public;

                        field.Access = result;
                        publicizedFields++;
                    }
                }

            if (typeDef.HasMethods)
                foreach (var method in typeDef.Methods)
                {
                    if (method is null || method.IsCompilerControlled)
                    {
                        Helper.WriteVerbose("Skipping nullable method...");
                        continue;
                    }

                    if (!method.IsPublic)
                    {
                        Helper.WriteVerbose($"Publicizing method: {method.FullName}");
                        method.Access = MethodAttributes.Public;
                        publicizedMethods++;
                    }
                }

            if (typeDef.HasProperties)
                foreach (var propery in typeDef.Properties)
                {
                    if (propery is null)
                    {
                        Helper.WriteVerbose("Skipping nullable property...");
                        continue;
                    }

                    var count = false;
                    if (!propery.GetMethod?.IsPublic ?? false)
                    {
                        Helper.WriteVerbose($"Publicizing setter of the property: {propery.FullName}");
                        propery.GetMethod.Access = MethodAttributes.Public;
                        count = true;
                    }

                    if (!propery.SetMethod?.IsPublic ?? false)
                    {
                        Helper.WriteVerbose($"Publicizing getter of the property: {propery.FullName}");
                        propery.SetMethod.Access = MethodAttributes.Public;
                        count = true;
                    }

                    if (count)
                        publicizedProperties++;
                }
        }
    }
}
