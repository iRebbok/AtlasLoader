using System;

namespace AtlasLoader
{
    /// <summary>
    ///     Main attribute defining mod.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class ModDefineAttribute : Attribute
    {
        public string Id { get; }
        public Type EntryPoint { get; }

        public ModDefineAttribute(string id, Type entryPoint)
        {
            Id = id;
            EntryPoint = entryPoint;
        }
    }
}
