using dnlib.DotNet;
using System;

namespace AtlasLoader.CLI
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class PatchedAttribute : Attribute
    {
        [InjectorIgnored]
        public static PatchedAttribute Create(CustomAttribute attribute) =>
            attribute == null
                ? null
                : new PatchedAttribute((UTF8String) attribute.ConstructorArguments[0].Value,
                    (int) attribute.ConstructorArguments[1].Value,
                    (int) attribute.ConstructorArguments[2].Value);

        public int EndIndex { get; }
        public int StartIndex { get; }
        public string Version { get; }

        public PatchedAttribute(string version, int startIndex, int endIndex)
        {
            Version = version;
            StartIndex = startIndex;
            EndIndex = endIndex;
        }
    }
}
