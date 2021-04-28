namespace Shoko.Plugin.Abstractions
{
    /// <summary>
    /// Implement this if your renamer expects a scriptable interface
    /// </summary>
    public interface IScriptedRenamer
    {
        /// <summary>
        /// The script for this implementation
        /// </summary>
        IRenameScript Script { get; }
    }
}
