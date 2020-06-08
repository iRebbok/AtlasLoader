using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AtlasLoader.CLI
{
    public enum PatcherMode
    {
        Patch,
        ForcePatch,
        RePatch,
        UnPatch,
        Info
    }

    public static class Patcher
    {
        const string ctor = ".ctor";
        const string typeToPatch = "DebugScreenController";
        const string methodToPath = "Awake";

        // current class
        static readonly ModuleDefMD patcherModule;
        // method to be inserted
        static MethodDef modMethod;
        // that is ignored when injecting
        static readonly TypeDef ignoredAttribute;
        // attribute that is applied to the method that was added
        static readonly TypeDef injectedAttribute;
        // attribute that is applied to the method that was modified
        static readonly TypeDef patchedAttribute;

        static readonly MethodDef injectedAttributeCtor;
        static readonly MethodDef patchedAttributeCtor;

        // the index that the method call is inserted by
        const int ILIndex = 0;

        // the method that will be patched
        static MethodDef? initMethod;
        // assembly-csharp itself
        static ModuleDef? module;

        static Patcher()
        {
            patcherModule = ModuleDefMD.Load(typeof(Patcher).Module);
            modMethod = patcherModule.Find(typeof(slPayload).FullName, true).FindMethod(nameof(slPayload.atlasLoaderBootstrap));

            ignoredAttribute = patcherModule.Find(typeof(InjectorIgnoredAttribute).FullName, true);
            patchedAttribute = patcherModule.Find(typeof(PatchedAttribute).FullName, true);
            injectedAttribute = patcherModule.Find(typeof(InjectedAttribute).FullName, true);

            patchedAttributeCtor = patchedAttribute.FindMethod(ctor);
            injectedAttributeCtor = injectedAttribute.FindMethod(ctor);
        }

        #region Low-level manipulation

        static void ForSuccessLoop<T>(IList<T> values, Func<T, bool> predicate)
        {
            for (int i = 0; i < values.Count;)
            {
                if (!predicate(values[i]))
                {
                    i++;
                }
            }
        }

        static void EjectAllMembers(TypeDef target, Func<IMemberDef, bool> predicate)
        {
            ForSuccessLoop(target.Fields, field =>
            {
                if (predicate(field))
                {
                    target.Fields.Remove(field);
                    return true;
                }

                return false;
            });

            ForSuccessLoop(target.Properties, property =>
            {
                if (predicate(property))
                {
                    target.Properties.Remove(property);
                    return true;
                }

                return false;
            });

            ForSuccessLoop(target.Methods, method =>
            {
                if (predicate(method))
                {
                    target.Methods.Remove(method);
                    return true;
                }

                return false;
            });

            ForSuccessLoop(target.NestedTypes, type =>
            {
                if (predicate(type))
                {
                    target.NestedTypes.Remove(type);
                    return true;
                }

                return false;
            });
        }

        static void EjectAllMembers(TypeDef target) => EjectAllMembers(target, member => member.CustomAttributes.Any(x => x.TypeFullName == injectedAttribute.FullName));

        static void Eject(TypeDef target) => target.Module.Types.Remove(target);

        static void EjectAllTypes(Func<TypeDef, bool> predicate)
        {
            for (int i = 0; i < module!.Types.Count;)
            {
                TypeDef type = module!.Types[i];

                if (predicate(type))
                {
                    Eject(type);
                }
                else
                {
                    i++;
                }
            }
        }

        static void EjectAllTypes() => EjectAllTypes(type => type.CustomAttributes.Any(x => x.TypeFullName == injectedAttribute.FullName));

        static void InjectAllMembers(TypeDef source, TypeDef target, Func<IMemberDef, bool> predicate)
        {
            bool NewPredicate(IMemberDef member)
            {
                if (predicate(member))
                {
                    member.CustomAttributes.Add(new CustomAttribute(injectedAttributeCtor));

                    return true;
                }

                return false;
            }

            ForSuccessLoop(source.Fields, field =>
            {
                if (NewPredicate(field))
                {
                    field.DeclaringType = target;
                    return true;
                }

                return false;
            });

            ForSuccessLoop(source.Properties, property =>
            {
                if (NewPredicate(property))
                {
                    property.DeclaringType = target;
                    return true;
                }

                return false;
            });

            ForSuccessLoop(source.Methods, method =>
            {
                if (method.Name == ctor)
                {
                    return false;
                }

                if (NewPredicate(method))
                {
                    method.DeclaringType = target;
                    return true;
                }

                return false;
            });

            ForSuccessLoop(source.NestedTypes, type =>
            {
                if (NewPredicate(type))
                {
                    type.DeclaringType = target;
                    return true;
                }

                return false;
            });
        }

        static void InjectAllMembers(TypeDef source, TypeDef target) =>
            InjectAllMembers(source, target, member => member.CustomAttributes.All(x => x.AttributeType != ignoredAttribute));

        #endregion

        static string? _input;
        static string? _output;
        static PatcherMode _mode;

        public static void Start(string[] args)
        {
            var baseArgs = Options.GetPatcherOptions();
            baseArgs.Handler = CommandHandler.Create<string, string?, string>((input, output, patcherMode) =>
            {
                Helper.WriteVerbose($"Written pather mode: {patcherMode}");
                _mode = Enum.Parse<PatcherMode>(patcherMode, true);
                Helper.WriteVerbose($"Final pather mode: {_mode} ({(int) _mode})");
                Helper.WriteVerbose($"Input: {input}", ConsoleColor.Yellow);
                _input = input;
                Helper.WriteVerbose($"Written output: {output}", ConsoleColor.Yellow);
                _output = !string.IsNullOrEmpty(output) ? output : $"{Path.GetFileNameWithoutExtension(input)}_publicized{Path.GetExtension(input)}";
                Helper.WriteVerbose($"Final output: {_output}", ConsoleColor.Yellow);
            });

            baseArgs.Invoke(args);

            var assembly = Helper.ReadAssembly(_input);

            Helper.WriteVerbose("Loading assembly as module");
            module = ModuleDefMD.Load(assembly);

            Helper.WriteVerbose($"Search for the desired class with a name: {typeToPatch}");
            var type = module.GetTypes().FirstOrDefault(t => t.Name.Equals(typeToPatch));
            Helper.WriteVerbose($"The found class is null: {type is null}");
            MethodDef? method = null;
            if (!(type is null))
            {
                Helper.WriteVerbose($"Search for the desired method with a name: {methodToPath}");
                method = type.FindMethod(methodToPath);
                Helper.WriteVerbose($"The found method is null: {method is null}");
            }
            else
            {
                Helper.WriteVerbose($"The method wasnt found");
                Helper.Exit(ErrorCode.NotPatched);
            }

            initMethod = method;

            switch (_mode)
            {
                case PatcherMode.Info:
                    var info = Info();
                    if (info is null)
                    {
                        Helper.WriteLine("Assembly is not patched.", ConsoleColor.Red);
                        Helper.Exit(ErrorCode.NotPatched);
                    }

                    Helper.WriteLine("Acquired patch info:", ConsoleColor.Green);
                    Helper.WriteLine($"- Version: {info!.Version}{Environment.NewLine}" +
                                $"- Start index: {info.StartIndex}{Environment.NewLine}" +
                                $"- End index: {info.EndIndex}{Environment.NewLine}");
                    break;
                case PatcherMode.Patch:
                    if (Info() is null)
                    {
                        Helper.WriteLine("Assembly is already patched.", ConsoleColor.Red);
                        Helper.Exit(ErrorCode.AlreadyPatched);
                    }

                    try
                    {
                        Patch();
                    }
                    catch (Exception e)
                    {
                        Helper.WriteLine($"Failed to patch:{Environment.NewLine}{e}", ConsoleColor.Red);
                        Helper.Exit(ErrorCode.PatchException);
                    }

                    break;
                case PatcherMode.UnPatch:
                    if (Info() is null)
                    {
                        Helper.WriteLine("Assembly is not patched.", ConsoleColor.Red);
                        Helper.Exit(ErrorCode.NotPatched);
                    }

                    try
                    {
                        Unpatch();
                    }
                    catch (Exception e)
                    {
                        Helper.WriteLine($"Failed to patch:{Environment.NewLine}{e}", ConsoleColor.Red);
                        Helper.Exit(ErrorCode.PatchException);
                    }

                    break;
                case PatcherMode.RePatch:
                    if (Info() == null)
                    {
                        Helper.WriteLine("Assembly is not patched.", ConsoleColor.Red);
                        Helper.Exit(ErrorCode.NotPatched);
                    }

                    try
                    {
                        Unpatch();
                        Patch();
                    }
                    catch (Exception e)
                    {
                        Helper.WriteLine($"Failed to patch:{Environment.NewLine}{e}", ConsoleColor.Red);
                        Helper.Exit(ErrorCode.PatchException);
                    }

                    break;
                case PatcherMode.ForcePatch:
                    if (Info() != null)
                    {
                        Helper.WriteLine("Assembly is already patched. Proceeding to wipe and patch.", ConsoleColor.Yellow);
                        Unpatch();
                    }

                    try
                    {
                        Patch();
                    }
                    catch (Exception e)
                    {
                        Helper.WriteLine($"Failed to patch:{Environment.NewLine}{e}", ConsoleColor.Red);
                        Helper.Exit(ErrorCode.PatchException);
                    }
                    break;
            }

            var patchedDirectory = Path.GetDirectoryName(_output);
            Helper.WriteVerbose($"Patched result directory: {patchedDirectory}");
            if (!string.IsNullOrEmpty(patchedDirectory) && !Directory.Exists(patchedDirectory))
            {
                Helper.WriteVerbose($"Patched directory doesn't exist, creating...", System.ConsoleColor.Cyan);
                Directory.CreateDirectory(patchedDirectory);
            }
            module.Write(_output);
        }

        static void Inject(TypeDef type)
        {
            EjectAllMembers(type, member => member.CustomAttributes.Any(x => x.AttributeType == ignoredAttribute));

            type.Module.Types.Remove(type);
            module!.Types.Add(type);

            type.CustomAttributes.Add(new CustomAttribute(injectedAttributeCtor));
        }

        static void Patch()
        {
            Inject(injectedAttribute);
            Inject(patchedAttribute);

            int index = ILIndex;

            InjectAllMembers(modMethod!.DeclaringType, initMethod!.DeclaringType);

            initMethod.Body.Instructions.Insert(index++, OpCodes.Call.ToInstruction(modMethod));

            initMethod.CustomAttributes.Add(new CustomAttribute(patchedAttributeCtor,
                new[]
                {
                    new CAArgument(initMethod.Module.CorLibTypes.String, "1.0.0"), new CAArgument(initMethod.Module.CorLibTypes.Int32, ILIndex),
                    new CAArgument(initMethod.Module.CorLibTypes.Int32, index)
                }));
        }

        static void Unpatch()
        {
            PatchedAttribute? patchInfo = Info();
            if (patchInfo is null)
                return;

            EjectAllTypes();
            EjectAllMembers(initMethod!.DeclaringType);

            int length = patchInfo.EndIndex - patchInfo.StartIndex;
            for (int i = 0; i < length; i++)
            {
                initMethod.Body.Instructions.RemoveAt(patchInfo.StartIndex);
            }

            initMethod.CustomAttributes.Remove(initMethod.CustomAttributes.FirstOrDefault(x => x.TypeFullName == patchedAttribute.FullName));
        }

        static PatchedAttribute? Info() => PatchedAttribute.Create(initMethod!.CustomAttributes.FirstOrDefault(x => x.TypeFullName == patchedAttribute.FullName));

        const BindingFlags coreModuleBootstrapBinding = BindingFlags.NonPublic | BindingFlags.Static;
        const string coreModuleBootstrapMethodName = "Initializer";
        const string coreModuleFullTypeName = "AtlasLoader.CoreModule";
        const string loaderPath = "atlasLoader/bin";

        private static class slPayload
        {
            internal static void atlasLoaderBootstrap()
            {
                Debug.Log("Bootstrapping AtlasLoader...");

                try
                {
                    MethodInfo? bootstrap = null;
                    foreach (string file in Directory.GetFiles(loaderPath, "*.dll"))
                    {
                        Debug.Log($"Loading {file}...");

                        Assembly? assembly = null;
                        try
                        { assembly = Assembly.Load(File.ReadAllBytes(file)); }
                        catch (IOException e)
                        { Debug.Log($"Failed loader file: {file}"); Debug.LogException(e); }
                        catch (BadImageFormatException e)
                        { Debug.Log($"Failed loader file: {file}"); Debug.LogException(e); }

                        if (!(bootstrap is null) || assembly is null)
                            continue;

                        Type? core = assembly.GetType(coreModuleFullTypeName);
                        if (core is null)
                            continue;

                        bootstrap = core.GetMethod(coreModuleBootstrapMethodName, coreModuleBootstrapBinding);
                        if (bootstrap is null)
                            throw new MissingMethodException($"The {coreModuleBootstrapMethodName} method of {coreModuleFullTypeName} does not exist.");
                    }

                    if (bootstrap is null)
                        throw new MissingMethodException("The bootstrap method was not found.");

                    bootstrap.Invoke(null, null);
                }
                catch (Exception e)
                {
                    Debug.Log("Failed to bootstrap AtlasLoader.");
                    Debug.LogException(e);

                    return;
                }

                Debug.Log("Successfully bootstrapped AtlasLoader.");
            }
        }
    }
}
