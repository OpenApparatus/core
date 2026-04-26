// Polyfills for language features used by Core that aren't available in
// netstandard2.1's BCL. Internal-only — never referenced from outside the assembly.

#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Reserved by the compiler for record types and `init` setters. The marker is
    /// shipped in .NET 5+; we polyfill it here so we can use those features while
    /// still multi-targeting netstandard2.1 for Unity compatibility.
    /// </summary>
    internal static class IsExternalInit { }
}
#endif
