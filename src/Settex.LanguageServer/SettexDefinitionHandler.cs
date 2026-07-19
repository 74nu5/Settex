using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<SettexDefinitionHandler> logger;
    private readonly ScopeResolver scopeResolver;
    private readonly TextDocumentSelector documentSelector = new(
        new TextDocumentFilter { Pattern = "**/*.settex" }
    );

    public SettexDefinitionHandler(SettexWorkspace workspace, ILogger<SettexDefinitionHandler> logger)
    {
        this.workspace = workspace;
        this.logger = logger;
        this.scopeResolver = new ScopeResolver();
    }

    /// <summary>
    /// Degrades to "no result" instead of faulting the request: an unexpected
    /// failure in analysis should not surface as a broken LSP call in the editor.
    /// </summary>
    public override async Task<LocationOrLocationLinks?> Handle(
        DefinitionParams request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await this.HandleCoreAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Go to definition failed for {Uri}", request.TextDocument.Uri);
            return null;
        }
    }

    private Task<LocationOrLocationLinks?> HandleCoreAsync(
        DefinitionParams request,
        CancellationToken cancellationToken)
    {
        // Makes the OperationCanceledException arm of the caller's guard real
        // instead of dead code: a request the client already withdrew does no work.
        cancellationToken.ThrowIfCancellationRequested();

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
        var activeScope = this.scopeResolver.FindScopeAt(rootScope, request.Position, snapshot.FilePath);

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
