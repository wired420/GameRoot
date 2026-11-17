// Lets C# records compile on .NET Framework, which lacks this marker type.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
