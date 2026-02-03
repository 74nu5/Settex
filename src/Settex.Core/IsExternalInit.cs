// Polyfill for records in netstandard2.0

#if NETSTANDARD2_0
#pragma warning disable IDE0130 // Le namespace ne correspond pas à la structure de dossiers
namespace System.Runtime.CompilerServices;
#pragma warning restore IDE0130 // Le namespace ne correspond pas à la structure de dossiers

internal static class IsExternalInit
{
}
#endif
