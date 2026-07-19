using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Settex.LanguageServer;

/// <summary>
/// Handler pour "Go to Definition".
/// Permet de naviguer vers la déclaration des variables et environnements.
/// </summary>
public class SettexDefinitionHandler : DefinitionHandlerBase
{
    private readonly SettexWorkspace workspace;
    private readonly ScopeResolver scopeResolver;
    private readonly TextDocumentSelector documentSelector = new(
        new TextDocumentFilter { Pattern = "**/*.settex" }
    );

    public SettexDefinitionHandler(SettexWorkspace workspace)
    {
        this.workspace = workspace;
        this.scopeResolver = new ScopeResolver();
    }

    public override Task<LocationOrLocationLinks?> Handle(
        DefinitionParams request,
        CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var document = this.workspace.GetDocument(uri);

        if (document == null)
        {
            return Task.FromResult<LocationOrLocationLinks?>(null);
        }

        // Capturer un seul snapshot immuable : le texte (pour l'offset du curseur)
        // et l'AST restent cohérents même si le document est modifié en parallèle.
        var snapshot = document.Current;

        if (snapshot.Ast == null)
        {
            return Task.FromResult<LocationOrLocationLinks?>(null);
        }

        // Extraire le mot à la position
        var word = GetWordAtPosition(snapshot.Text, request.Position);

        if (string.IsNullOrEmpty(word))
        {
            return Task.FromResult<LocationOrLocationLinks?>(null);
        }

        // Construire la hiérarchie des scopes
        var rootScope = this.scopeResolver.BuildScopeHierarchy(snapshot.Ast);

        // Trouver le scope actif à la position du curseur
        var activeScope = this.scopeResolver.FindScopeAt(rootScope, request.Position);

        if (activeScope != null)
        {
            // Chercher la variable dans le scope actif (avec remontée aux parents)
            var letNode = this.scopeResolver.FindVariableInScope(word, activeScope);

            if (letNode != null)
            {
                var location = SettexDocument.ToLspLocation(letNode.Location, request.TextDocument.Uri);
                return Task.FromResult<LocationOrLocationLinks?>(new LocationOrLocationLinks(location));
            }
        }

        // Chercher la définition d'un environnement
        var envNode = snapshot.Ast.Statements
            .OfType<Core.Parser.Ast.EnvBlockNode>()
            .FirstOrDefault(env => env.EnvironmentName == word);

        if (envNode != null)
        {
            var location = SettexDocument.ToLspLocation(envNode.Location, request.TextDocument.Uri);
            return Task.FromResult<LocationOrLocationLinks?>(new LocationOrLocationLinks(location));
        }

        return Task.FromResult<LocationOrLocationLinks?>(null);
    }

    protected override DefinitionRegistrationOptions CreateRegistrationOptions(
        DefinitionCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new DefinitionRegistrationOptions
        {
            DocumentSelector = this.documentSelector
        };
    }

    private static string GetWordAtPosition(string text, Position position)
    {
        var lines = text.Split('\n');
        if (position.Line >= lines.Length)
        {
            return string.Empty;
        }

        var line = lines[position.Line];
        if (position.Character >= line.Length)
        {
            return string.Empty;
        }

        var start = position.Character;
        var end = position.Character;

        while (start > 0 && IsWordChar(line[start - 1]))
        {
            start--;
        }

        while (end < line.Length && IsWordChar(line[end]))
        {
            end++;
        }

        return line.Substring(start, end - start);
    }

    private static bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }
}
