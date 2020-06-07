using dnlib.DotNet;
using System;
using System.IO;
using System.Reflection;

namespace AtlasLoader.Patcher
{
    public class ScpSlPatcher : Patcher
    {
        public const BindingFlags CoreModuleBootstrapBinding = BindingFlags.NonPublic | BindingFlags.Static;
        public const string CoreModuleBootstrapMethodName = "Initializer";
        public const string CoreModuleFullTypeName = "Atlas.CoreModule";
        public const string UnityAssemblyCSharpFile = "../../SCPSL_Data/Managed/Assembly-CSharp.dll";

        public override string CurrentVersion { get; }
        public override string DefaultPath { get; }

        public override int ILIndex { get; }

        public override MethodDef InitMethod { get; protected set; }
        public override MethodDef ModLoader { get; }

        public ScpSlPatcher()
        {
            CurrentVersion = "1.0.0";
            DefaultPath = UnityAssemblyCSharpFile;

            ModLoader = ModuleDefMD.Load(typeof(ScpSlPayload).Module).Find(typeof(ScpSlPayload).FullName, true).FindMethod(nameof(ScpSlPayload.AtlasModLoader));
            ILIndex = 0;
        }

        public override void LoadModule(ModuleDefMD module)
        {
            //InitMethod = module.Find(typeof(DebugScreenController).FullName, true).FindMethod("Awake");
        }

        private static class ScpSlPayload
        {
            internal static void AtlasModLoader()
            {
                //Debug.Log("Bootstrapping Atlas...");

                try
                {
                    MethodInfo bootstrap = null;
                    foreach (string file in Directory.GetFiles("Atlas/bin/", "*.dll"))
                    {
                        //Debug.Log($"Loading {file}...");

                        Assembly assembly = Assembly.LoadFrom(file);
                        if (bootstrap != null)
                        {
                            continue;
                        }

                        Type core = assembly.GetType(CoreModuleFullTypeName);
                        if (core == null)
                        {
                            continue;
                        }

                        bootstrap = core.GetMethod(CoreModuleBootstrapMethodName, CoreModuleBootstrapBinding);
                        if (bootstrap == null)
                        {
                            throw new MissingMethodException("The " + CoreModuleBootstrapMethodName + " method of " + CoreModuleFullTypeName + " does not exist.");
                        }
                    }

                    if (bootstrap == null)
                    {
                        throw new MissingMethodException("The bootstrap method was not found.");
                    }

                    bootstrap.Invoke(null, null);
                }
                catch (Exception e)
                {
                    //Debug.Log("Failed to bootstrap Atlas.");
                    //Debug.LogException(e);

                    return;
                }

                //Debug.Log("Successfully bootstrapped Atlas.");
            }
        }
    }
}
