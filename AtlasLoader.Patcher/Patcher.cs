using AtlasLoader.Patcher;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AtlasLoader.Patcher
{
    public abstract class Patcher
    {
        protected static TypeDef IgnoredAttribute { get; }
        protected static TypeDef InjectedAttribute { get; }
        protected static MethodDef InjectedAttributeCtor { get; }
        protected static TypeDef PatchedAttribute { get; }

        protected static MethodDef PatchedAttributeCtor { get; }
        protected static ModuleDefMD PatcherModule { get; }

        public static IEnumerable<Type> Implementations
        {
            get
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;

                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException e)
                    {
                        types = e.Types;
                    }

                    foreach (Type type in types)
                    {
                        if (type != null && !type.IsAbstract && typeof(Patcher).IsAssignableFrom(type))
                        {
                            yield return type;
                        }
                    }
                }
            }
        }

        static Patcher()
        {
            PatcherModule = ModuleDefMD.Load(typeof(Patcher).Module);
            IgnoredAttribute = PatcherModule.Find(typeof(InjectorIgnoredAttribute).FullName, true);
            PatchedAttribute = PatcherModule.Find(typeof(PatchedAttribute).FullName, true);
            InjectedAttribute = PatcherModule.Find(typeof(InjectedAttribute).FullName, true);
            

            const string ctor = ".ctor";
            PatchedAttributeCtor = PatchedAttribute.FindMethod(ctor);
            InjectedAttributeCtor = InjectedAttribute.FindMethod(ctor);
        }

        protected static void ForSuccessLoop<T>(IList<T> values, Func<T, bool> predicate)
        {
            for (int i = 0; i < values.Count;)
            {
                if (!predicate(values[i]))
                {
                    i++;
                }
            }
        }

        private ModuleDefMD _module;

        public abstract string CurrentVersion { get; }
        public abstract string DefaultPath { get; }

        public abstract int ILIndex { get; }

        public abstract MethodDef InitMethod { get; protected set; }
        public abstract MethodDef ModLoader { get; }

        public ModuleDefMD Module
        {
            get => _module;
            set
            {
                LoadModule(value);
                _module = value;
            }
        }

        public abstract void LoadModule(ModuleDefMD module);

        protected void EjectAllMembers(TypeDef target, Func<IMemberDef, bool> predicate)
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

        protected void EjectAllMembers(TypeDef target) => EjectAllMembers(target, member => member.CustomAttributes.Any(x => x.TypeFullName == InjectedAttribute.FullName));

        protected void Eject(TypeDef target) => target.Module.Types.Remove(target);

        protected void EjectAllTypes(Func<TypeDef, bool> predicate)
        {
            for (int i = 0; i < Module.Types.Count;)
            {
                TypeDef type = Module.Types[i];

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

        protected void EjectAllTypes() => EjectAllTypes(type => type.CustomAttributes.Any(x => x.TypeFullName == InjectedAttribute.FullName));

        protected void InjectAllMembers(TypeDef source, TypeDef target, Func<IMemberDef, bool> predicate)
        {
            bool NewPredicate(IMemberDef member)
            {
                if (predicate(member))
                {
                    member.CustomAttributes.Add(new CustomAttribute(InjectedAttributeCtor));

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
                if (method.Name == ".ctor")
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

        protected void InjectAllMembers(TypeDef source, TypeDef target) =>
            InjectAllMembers(source, target, member => member.CustomAttributes.All(x => x.AttributeType != IgnoredAttribute));

        protected void Inject(TypeDef type)
        {
            EjectAllMembers(type, member => member.CustomAttributes.Any(x => x.AttributeType == IgnoredAttribute));

            type.Module.Types.Remove(type);
            _module.Types.Add(type);

            type.CustomAttributes.Add(new CustomAttribute(InjectedAttributeCtor));
        }

        public virtual void Patch(string path)
        {
            Inject(InjectedAttribute);
            Inject(PatchedAttribute);

            int index = ILIndex;

            InjectAllMembers(ModLoader.DeclaringType, InitMethod.DeclaringType);
            InitMethod.Body.Instructions.Insert(index++, OpCodes.Call.ToInstruction(ModLoader));

            InitMethod.CustomAttributes.Add(new CustomAttribute(PatchedAttributeCtor,
                new[]
                {
                    new CAArgument(InitMethod.Module.CorLibTypes.String, CurrentVersion), new CAArgument(InitMethod.Module.CorLibTypes.Int32, ILIndex),
                    new CAArgument(InitMethod.Module.CorLibTypes.Int32, index)
                }));
        }

        public virtual void Unpatch(string path)
        {
            PatchedAttribute patchInfo = Info();
            if (patchInfo == null)
            {
                return;
            }

            EjectAllTypes();
            EjectAllMembers(InitMethod.DeclaringType);

            int length = patchInfo.EndIndex - patchInfo.StartIndex;
            for (int i = 0; i < length; i++)
            {
                InitMethod.Body.Instructions.RemoveAt(patchInfo.StartIndex);
            }

            InitMethod.CustomAttributes.Remove(InitMethod.CustomAttributes.FirstOrDefault(x => x.TypeFullName == PatchedAttribute.FullName));
        }

        public virtual PatchedAttribute Info() =>
            AtlasLoader.Patcher.PatchedAttribute.Create(InitMethod.CustomAttributes.FirstOrDefault(x => x.TypeFullName == PatchedAttribute.FullName));
    }
}
