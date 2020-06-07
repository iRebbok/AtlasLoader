using System;

namespace AtlasLoader
{
    /// <summary>
    ///     Provides additional info on a Atlas mod.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class MetadataAttribute : Attribute
    {
        /// <summary>
        ///     The creator(s) of the mod.
        /// </summary>
        public string[] Authors { get; }

        /// <summary>
        ///     The description of what the mod does.
        /// </summary>
        public string Description { get; }
        /// <summary>
        ///     The human-readable name of the mod.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Constructs an instance of <see cref="MetadataAttribute" />.
        /// </summary>
        /// <param name="name">The human-readable name of the mod.</param>
        /// <param name="description">A description of the purpose of the mod.</param>
        /// <param name="authors">The names or aliases of the authors of the mod.</param>
        public MetadataAttribute(string name = null, string description = null, params string[] authors)
        {
            Name = name;
            Description = description;
            Authors = authors;
        }
    }
}
