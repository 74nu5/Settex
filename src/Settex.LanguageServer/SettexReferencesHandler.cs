using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Settex.LanguageServer;

/// <summary>
/// Handler pour "Find All References".
/// Trouve toutes les utilisations d'une variable ou environnement.
/// </summary>
public class SettexReferencesHandler : ReferencesHandlerBase
{
    private readonly SettexWorkspace workspace;
    private readonly ILogger<SettexReferencesHandler> logger;
    private readonly ScopeResolver scopeResolver;
    private readonly TextDocumentSelector documentSelector = new(
        new TextDocumentFilter { Pattern = "**/*.settex" }
    );

    public SettexReferencesHandler(SettexWorkspace workspace, ILogger<SettexReferencesHandler> logger)
    {
        this.workspace = workspace;
        this.logger = logger;
        this.scopeResolver = new ScopeResolver();
    }

    /// <summary>
    /// Degrades to "no result" instead of faulting the request.
    /// </summary>
    public override async Task<LocationContainer?> Handle(
        ReferenceParams request,
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
            this.logger.LogError(ex, "Find references failed for {Uri}", request.TextDocument.Uri);
            return null;
        }
    }

    private Task<LocationContainer?> HandleCoreAsync(
        ReferenceParams request,
        CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var document = this.workspace.GetDocument(uri);

        if (document == null)
        {
            return Task.FromResult<LocationContainer?>(null);
        }

        // Snapshot unique : texte et AST cohérents malgré des updates concurrents.
        var snapshot = document.Current;

        if (snapshot.Ast == null)
        {
            return Task.FromResult<LocationContainer?>(null);
        }

        var word = GetWordAtPosition(snapshot.Text, request.Position);

        if (string.IsNullOrEmpty(word))
        {
            return Task.FromResult<LocationContainer?>(null);
        }

        var locations = new List<Location>();

        // Construire la hiérarchie des scopes
        var rootScope = this.scopeResolver.BuildScopeHierarchy(snapshot.Ast);
        
        // Trouver le scope actif à la position du curseur
        var activeScope = this.scopeResolver.FindScopeAt(rootScope, request.Position, snapshot.FilePath);
        
        if (activeScope == null)
        {
            return Task.FromResult<LocationContainer?>(null);
        }

        // Trouver la définition de la variable dans le scope actif
        var targetLetNode = this.scopeResolver.FindVariableInScope(word, activeScope);
        
        if (targetLetNode == null)
        {
            return Task.FromResult<LocationContainer?>(null);
        }

        // Inclure la déclaration si demandée
        if (request.Context.IncludeDeclaration)
        {
            locations.Add(SettexDocument.ToLspLocation(targetLetNode.Location, request.TextDocument.Uri));
        }

        // Trouver toutes les références qui résolvent vers cette même définition
        var references = FindScopedReferences(snapshot.Ast, word, targetLetNode, rootScope);

        foreach (var reference in references)
        {
            locations.Add(SettexDocument.ToLspLocation(reference.Location, request.TextDocument.Uri));
        }

        if (locations.Count == 0)
        {
            return Task.FromResult<LocationContainer?>(null);
        }

        return Task.FromResult<LocationContainer?>(new LocationContainer(locations));
    }

    protected override ReferenceRegistrationOptions CreateRegistrationOptions(
        ReferenceCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new ReferenceRegistrationOptions
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

    /// <summary>
    /// Trouve toutes les références à une variable qui résolvent vers la même déclaration,
    /// en tenant compte des scopes pour éviter la confusion entre homonymes.
    /// </summary>
    private List<Core.Parser.Ast.VariableRefNode> FindScopedReferences(
        Core.Parser.Ast.FileNode ast,
        string name,
        Core.Parser.Ast.LetNode targetDeclaration,
        ScopeInfo rootScope)
    {
        var scopedReferences = new List<Core.Parser.Ast.VariableRefNode>();
        
        // Trouver toutes les références brutes
        var allReferences = FindVariableReferences(ast, name);
        
        // Filtrer pour ne garder que celles qui résolvent vers la même déclaration
        foreach (var reference in allReferences)
        {
            // Trouver le scope de cette référence
            var refScope = FindScopeAtLocation(rootScope, reference.Location);
            
            if (refScope != null)
            {
                // Résoudre la variable dans ce scope
                var resolvedDeclaration = this.scopeResolver.FindVariableInScope(name, refScope);
                
                // Si elle résout vers la même déclaration que notre cible, l'inclure
                if (resolvedDeclaration == targetDeclaration)
                {
                    scopedReferences.Add(reference);
                }
            }
        }
        
        return scopedReferences;
    }

    /// <summary>
    /// Trouve le scope qui contient une position donnée (basé sur SourceLocation).
    /// La recherche est restreinte aux scopes du <strong>fichier de la référence</strong> :
    /// les références sont collectées sur l'AST aplati et peuvent donc provenir d'un
    /// fichier inclus, auquel cas ce sont ses scopes à lui qui font autorité, pas ceux
    /// qui occupent les mêmes lignes ailleurs.
    /// </summary>
    private static ScopeInfo? FindScopeAtLocation(ScopeInfo rootScope, Core.Diagnostics.SourceLocation location)
    {
        return FindScopeAtRecursive(rootScope, location.Line, location.Column, location.FilePath);
    }

    /// <summary>
    /// Recherche récursive du scope à une position donnée.
    /// </summary>
    private static ScopeInfo? FindScopeAtRecursive(ScopeInfo scope, int line, int column, string? filePath)
    {
        // Vérifier si la position est dans ce scope
        if (!scope.ContainsPosition(line, column))
        {
            return null;
        }

        // Chercher dans les scopes enfants (ordre inverse pour avoir le plus récent)
        for (var i = scope.Children.Count - 1; i >= 0; i--)
        {
            var child = scope.Children[i];

            // Ne pas descendre dans un scope d'un autre fichier : ses lignes se
            // superposeraient à celles de la référence. Voir ScopeResolver.
            if (!SettexDocument.IsFromSameFile(child.Location, filePath))
            {
                continue;
            }

            var childResult = FindScopeAtRecursive(child, line, column, filePath);
            if (childResult != null)
            {
                return childResult;
            }
        }

        // Aucun enfant ne contient la position, ce scope est le plus spécifique
        return scope;
    }

    private static List<Core.Parser.Ast.VariableRefNode> FindVariableReferences(
        Core.Parser.Ast.FileNode ast,
        string name)
    {
        var references = new List<Core.Parser.Ast.VariableRefNode>();
        
        foreach (var statement in ast.Statements)
        {
            FindReferencesInStatement(statement, name, references);
        }

        return references;
    }

    private static void FindReferencesInStatement(
        Core.Parser.Ast.ITopLevelStatement statement,
        string name,
        List<Core.Parser.Ast.VariableRefNode> references)
    {
        switch (statement)
        {
            case Core.Parser.Ast.LetNode let:
                FindReferencesInExpression(let.Value, name, references);
                break;
            
            case Core.Parser.Ast.EnvBlockNode env:
                FindReferencesInBlock(env.SettingsBlock.Block, name, references);
                break;
            
            case Core.Parser.Ast.SettingsBlockNode settings:
                FindReferencesInBlock(settings.Block, name, references);
                break;
        }
    }

    private static void FindReferencesInBlock(
        Core.Parser.Ast.BlockNode block,
        string name,
        List<Core.Parser.Ast.VariableRefNode> references)
    {
        foreach (var statement in block.Statements)
        {
            FindReferencesInBlockStatement(statement, name, references);
        }
    }

    private static void FindReferencesInBlockStatement(
        Core.Parser.Ast.IStatement statement,
        string name,
        List<Core.Parser.Ast.VariableRefNode> references)
    {
        if (statement is Core.Parser.Ast.AssignmentNode assignment)
        {
            FindReferencesInExpression(assignment.Value, name, references);
        }
        else if (statement is Core.Parser.Ast.LetNode let)
        {
            FindReferencesInExpression(let.Value, name, references);
        }
        else if (statement is Core.Parser.Ast.NestedBlockNode nested)
        {
            foreach (var stmt in nested.Block.Statements)
            {
                FindReferencesInBlockStatement(stmt, name, references);
            }
        }
    }

    private static void FindReferencesInExpression(
        Core.Parser.Ast.IExpression expression,
        string name,
        List<Core.Parser.Ast.VariableRefNode> references)
    {
        switch (expression)
        {
            case Core.Parser.Ast.VariableRefNode varRef when varRef.Name == name:
                references.Add(varRef);
                break;

            case Core.Parser.Ast.BinaryOpNode binary:
                FindReferencesInExpression(binary.Left, name, references);
                FindReferencesInExpression(binary.Right, name, references);
                break;

            case Core.Parser.Ast.UnaryOpNode unary:
                FindReferencesInExpression(unary.Operand, name, references);
                break;

            case Core.Parser.Ast.InterpolatedStringNode interpolated:
                foreach (var segment in interpolated.Segments)
                {
                    if (segment is Core.Parser.Ast.ExpressionSegment exprSeg)
                    {
                        FindReferencesInExpression(exprSeg.Expression, name, references);
                    }
                }
                break;

            case Core.Parser.Ast.ArrayNode array:
                foreach (var element in array.Elements)
                {
                    if (element is Core.Parser.Ast.IExpression expr)
                    {
                        FindReferencesInExpression(expr, name, references);
                    }
                }
                break;

            case Core.Parser.Ast.TaggedObjectNode tagged:
                foreach (var stmt in tagged.Block.Statements)
                {
                    FindReferencesInBlockStatement(stmt, name, references);
                }
                break;

            case Core.Parser.Ast.MemberAccessNode member:
                FindReferencesInExpression(member.Object, name, references);
                break;
        }
    }
}
