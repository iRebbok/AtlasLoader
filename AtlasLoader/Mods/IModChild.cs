namespace AtlasLoader
{
    /// <summary>
    ///     An object whose instance is parented by a mod.
    /// </summary>
    public interface IModChild
    {
        /// <summary>
        ///     The parent mod of this object.
        /// </summary>
        Mod Owner { get; }
    }

    /// <inheritdoc />
    public interface IModChild<out TMod> : IModChild where TMod : Mod
    {
        /// <inheritdoc cref="IModChild.Owner" />
        new TMod Owner { get; }
    }
}
