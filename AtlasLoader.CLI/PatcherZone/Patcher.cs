using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
        static MethodDef initMethod;
        // assembly-csharp itself
        static ModuleDef module;

        static Patcher()
        {
            patcherModule = ModuleDefMD.Load(typeof(Patcher).Module);
            modMethod = patcherModule.Find(typeof(Patcher).FullName, true).FindMethod(nameof(AtlasLoaderBootstrap));

            ignoredAttribute = patcherModule.Find(typeof(InjectorIgnoredAttribute).FullName, true);
            patchedAttribute = patcherModule.Find(typeof(PatchedAttribute).FullName, true);
            patchedAttribute.Namespace = null;
            injectedAttribute = patcherModule.Find(typeof(InjectedAttribute).FullName, true);
            injectedAttribute.Namespace = null;

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
            for (int i = 0; i < module.Types.Count;)
            {
                TypeDef type = module.Types[i];

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

        #endregion

        public static async Task Start(GlobalOptions options) =>
            await PatcherOptions.Parse(options, Environment.GetCommandLineArgs()).ConfigureAwait(false);

        internal static void Main(GlobalOptions options, PatcherOptions patcherOptions)
        {
            Helper.WriteVerbose($"Written pather mode: {patcherOptions.Mode}");
            Helper.WriteVerbose($"Input: {patcherOptions.Input}", ConsoleColor.Yellow);
            Helper.WriteVerbose($"Written output: {patcherOptions.Output}", ConsoleColor.Yellow);
            var output = !string.IsNullOrEmpty(patcherOptions.Output) ? patcherOptions.Output : patcherOptions.Input;
            Helper.WriteVerbose($"Final output: {patcherOptions.Output}", ConsoleColor.Yellow);

            var assembly = Helper.ReadAssembly(patcherOptions.Input);

            Helper.WriteVerbose("Loading assembly as module");
            module = ModuleDefMD.Load(assembly);

            Helper.WriteVerbose($"Search for the desired class with a name: {typeToPatch}");
            var type = module.GetTypes().FirstOrDefault(t => t.Name.Equals(typeToPatch));
            Helper.WriteVerbose($"The found class is null: {type is null}");
            MethodDef method = null;
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

            ProcessMode(options, patcherOptions);
            if (patcherOptions.Mode == PatcherMode.Info)
                return;

            var patchedDirectory = Path.GetDirectoryName(output);
            var isEmpty = string.IsNullOrEmpty(patchedDirectory);
            Helper.WriteVerbose($"Patched result directory: {(!isEmpty ? patchedDirectory : "null")}");
            if (!string.IsNullOrEmpty(patchedDirectory) && !Directory.Exists(patchedDirectory))
            {
                Helper.WriteVerbose("Patched directory doesn't exist, creating...", ConsoleColor.Cyan);
                Directory.CreateDirectory(patchedDirectory);
            }
            module.Write(output);
        }

        private static void ProcessMode(GlobalOptions options, PatcherOptions patcherOptions)
        {
            switch (patcherOptions.Mode)
            {
                default:
                    Helper.WriteLine("Invalid patcher mode given", ConsoleColor.Red);
                    break;
                case PatcherMode.Info:
                    var info = Info();
                    if (info is null)
                    {
                        Helper.WriteLine("Assembly is not patched.", ConsoleColor.Red);
                        Helper.Exit(ErrorCode.NotPatched);
                    }

                    Helper.WriteLine("Acquired patch info:", ConsoleColor.Green);
                    Helper.WriteLine($"- Version: {info.Version}{Environment.NewLine}" +
                                $"- Start index: {info.StartIndex}{Environment.NewLine}" +
                                $"- End index: {info.EndIndex}{Environment.NewLine}");
                    return;
                case PatcherMode.Patch:
                    if (!(Info() is null))
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
                    if (Info() is null)
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
                    if (!(Info() is null))
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
        }

        static void Inject(TypeDef type)
        {
            EjectAllMembers(type, member => member.CustomAttributes.Any(x => x.AttributeType == ignoredAttribute));

            type.Module.Types.Remove(type);
            module.Types.Add(type);

            type.CustomAttributes.Add(new CustomAttribute(injectedAttributeCtor));
        }

        static void Patch()
        {
            Inject(injectedAttribute);
            Inject(patchedAttribute);

            int index = ILIndex;

            modMethod.DeclaringType = null;
            modMethod.CustomAttributes.Add(new CustomAttribute(injectedAttributeCtor));
            initMethod.DeclaringType.Methods.Insert(initMethod.DeclaringType.Methods.IndexOf(initMethod) + 1, modMethod);
            initMethod.Body.Instructions.Insert(index++, OpCodes.Call.ToInstruction(modMethod));

            initMethod.CustomAttributes.Add(new CustomAttribute(patchedAttributeCtor,
                new[]
                {
                    new CAArgument(initMethod.Module.CorLibTypes.String, CoreModule.VERSION), new CAArgument(initMethod.Module.CorLibTypes.Int32, ILIndex),
                    new CAArgument(initMethod.Module.CorLibTypes.Int32, index)
                }));
        }

        static void Unpatch()
        {
            PatchedAttribute patchInfo = Info();
            if (patchInfo is null)
                return;

            EjectAllTypes();
            EjectAllMembers(initMethod.DeclaringType);

            int length = patchInfo.EndIndex - patchInfo.StartIndex;
            for (int i = 0; i < length; i++)
            {
                initMethod.Body.Instructions.RemoveAt(patchInfo.StartIndex);
            }

            initMethod.CustomAttributes.Remove(initMethod.CustomAttributes.FirstOrDefault(x => x.TypeFullName == patchedAttribute.FullName));
        }

        static PatchedAttribute Info() => PatchedAttribute.Create(initMethod.CustomAttributes.FirstOrDefault(x => x.TypeFullName == patchedAttribute.FullName));

        const BindingFlags coreModuleBootstrapBinding = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod;
        const string coreModuleBootstrapMethodName = "Initializer";
        const string coreModuleFullTypeName = "AtlasLoader.CoreModule";
        const string loaderPath = "atlasLoader/bin";

        private static void AtlasLoaderBootstrap()
        {
            Debug.Log("Bootstrapping AtlasLoader...");

            try
            {
                MethodInfo bootstrap = null;
                var path = Path.GetFullPath(loaderPath);
                foreach (string file in Directory.GetFiles(path, "*.dll"))
                {
                    Debug.Log($"Loading {file}...");

                    Assembly assembly = null;
                    try
                    { assembly = Assembly.Load(File.ReadAllBytes(file)); }
                    catch (Exception e)
                    { Debug.Log($"Failed loader file: {file}"); Debug.LogException(e); }

                    if (bootstrap != null || assembly == null)
                        continue;

                    Type core = assembly.GetType(coreModuleFullTypeName);
                    if (core == null)
                        continue;

                    bootstrap = core.GetMethod(coreModuleBootstrapMethodName, coreModuleBootstrapBinding);
                    if (bootstrap == null)
                        throw new MissingMethodException($"The '{coreModuleBootstrapMethodName}' method of '{coreModuleFullTypeName}' does not exist.");
                }

                if (bootstrap == null)
                    throw new MissingMethodException("The bootstrap method was not found.");

                bootstrap.Invoke(null, new[] { Directory.GetCurrentDirectory(), path });
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
