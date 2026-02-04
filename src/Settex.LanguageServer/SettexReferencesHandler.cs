using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly TextDocumentSelector documentSelector = new(
        new TextDocumentFilter { Pattern = "**/*.settex" }
    );

    public SettexReferencesHandler(SettexWorkspace workspace)
    {
        this.workspace = workspace;
    }

    public override Task<LocationContainer?> Handle(
        ReferenceParams request,
        CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var document = this.workspace.GetDocument(uri);

        if (document?.Ast == null || document.Text == null)
        {
            return Task.FromResult<LocationContainer?>(null);
        }

        var word = GetWordAtPosition(document.Text, request.Position);
        
        if (string.IsNullOrEmpty(word))
        {
            return Task.FromResult<LocationContainer?>(null);
        }

        var locations = new List<Location>();

        // Inclure la déclaration si demandée
        if (request.Context.IncludeDeclaration)
        {
            var letNode = document.Ast.Statements
                .OfType<Core.Parser.Ast.LetNode>()
                .FirstOrDefault(let => let.Name == word);

            if (letNode != null)
            {
                locations.Add(new Location
                {
                    Uri = request.TextDocument.Uri,
                    Range = SettexDocument.LocationToRange(letNode.Location)
                });
            }
        }

        // Trouver toutes les références (VariableRefNode)
        var references = FindVariableReferences(document.Ast, word);
        
        foreach (var reference in references)
        {
            locations.Add(new Location
            {
                Uri = request.TextDocument.Uri,
                Range = SettexDocument.LocationToRange(reference.Location)
            });
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
