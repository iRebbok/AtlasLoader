using System;

namespace AtlasLoader
{
    /// <summary>
    /// 	The attribute to describe the annotated module's load and event call priority.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ModulePriorityAttribute : Attribute
    {
        /// <summary>
        /// 	The priority of the annotated module's load and event calls.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// 	Constructs an instance of <see cref="ModulePriorityAttribute"/>.
        /// </summary>
        /// <param name="priority">The priority of the annotated module's load and event calls.</param>
        public ModulePriorityAttribute(int priority)
        {
            Priority = priority;
        }
    }
}
