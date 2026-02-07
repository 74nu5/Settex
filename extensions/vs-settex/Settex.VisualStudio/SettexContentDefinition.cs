namespace Settex.VisualStudio;

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Utilities;

/// <summary>
/// Content type definition for Settex files.
/// </summary>
internal static class SettexContentDefinition
{
    /// <summary>
    /// Settex content type definition.
    /// </summary>
    [Export]
    [Name("settex")]
    [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
    internal static ContentTypeDefinition SettexContentTypeDefinition;

    /// <summary>
    /// File extension to content type mapping for .settex files.
    /// </summary>
    [Export]
    [FileExtension(".settex")]
    [ContentType("settex")]
    internal static FileExtensionToContentTypeDefinition SettexFileExtensionDefinition;
}
