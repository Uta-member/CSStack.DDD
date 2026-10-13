#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
	/// <summary>
	/// Enables <see langword="init"/> accessors and records on target frameworks that do not define this type.
	/// </summary>
	internal static class IsExternalInit
	{
	}
}
#endif
