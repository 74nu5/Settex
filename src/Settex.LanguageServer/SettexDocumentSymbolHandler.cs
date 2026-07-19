using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Settex.LanguageServer;

/// <summary>
/// Handler pour Document Symbols (outline / structure du document).
/// </summary>
public class SettexDocumentSymbolHandler : DocumentSymbolHandlerBase
{
    private readonly SettexWorkspace workspace;
    private readonly TextDocumentSelector documentSelector = new(
        new TextDocumentFilter { Pattern = "**/*.settex" }
    );

    public SettexDocumentSymbolHandler(SettexWorkspace workspace)
    {
        this.workspace = workspace;
    }

    public override Task<SymbolInformationOrDocumentSymbolContainer?> Handle(
        DocumentSymbolParams request,
        CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var document = this.workspace.GetDocument(uri);

        if (document == null)
        {
            return Task.FromResult<SymbolInformationOrDocumentSymbolContainer?>(null);
        }

        // Snapshot unique pour une vue cohérente de l'AST.
        var snapshot = document.Current;

        if (snapshot.Ast == null)
        {
            return Task.FromResult<SymbolInformationOrDocumentSymbolContainer?>(null);
        }

        var symbols = new List<SymbolInformationOrDocumentSymbol>();

        foreach (var statement in snapshot.Ast.Statements)
        {
            // Les includes sont inlinés dans l'AST : ne garder que les symboles
            // propres à ce fichier, sinon l'outline afficherait le contenu des
            // fichiers inclus avec leurs numéros de ligne (ranges faux).
            if (!SettexDocument.IsFromSameFile(statement.Location, snapshot.FilePath))
            {
                continue;
            }

            var symbol = CreateSymbol(statement);
            if (symbol != null)
            {
                symbols.Add(symbol);
            }
        }

        if (symbols.Count == 0)
        {
            return Task.FromResult<SymbolInformationOrDocumentSymbolContainer?>(null);
        }

        return Task.FromResult<SymbolInformationOrDocumentSymbolContainer?>(
            new SymbolInformationOrDocumentSymbolContainer(symbols)
        );
    }

    protected override DocumentSymbolRegistrationOptions CreateRegistrationOptions(
        DocumentSymbolCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new DocumentSymbolRegistrationOptions
        {
            DocumentSelector = this.documentSelector
        };
    }

    private static SymbolInformationOrDocumentSymbol? CreateSymbol(Core.Parser.Ast.ITopLevelStatement statement)
    {
        return statement switch
        {
            Core.Parser.Ast.SettingsBlockNode settings => new SymbolInformationOrDocumentSymbol(
                new DocumentSymbol
                {
                    Name = "settings",
                    Kind = SymbolKind.Struct,
                    Range = SettexDocument.LocationToRange(settings.Location),
                    SelectionRange = SettexDocument.LocationToRange(settings.Location),
                    Children = CreateBlockChildren(settings.Block.Statements)
                }
            ),

            Core.Parser.Ast.EnvBlockNode env => new SymbolInformationOrDocumentSymbol(
                new DocumentSymbol
                {
                    Name = $"env {env.EnvironmentName}",
                    Kind = SymbolKind.Namespace,
                    Range = SettexDocument.LocationToRange(env.Location),
                    SelectionRange = SettexDocument.LocationToRange(env.Location),
                    Children = CreateBlockChildren(env.SettingsBlock.Block.Statements)
                }
            ),

            Core.Parser.Ast.LetNode let => new SymbolInformationOrDocumentSymbol(
                new DocumentSymbol
                {
                    Name = let.Name,
                    Kind = SymbolKind.Variable,
                    Range = SettexDocument.LocationToRange(let.Location),
                    SelectionRange = SettexDocument.LocationToRange(let.Location)
                }
            ),

            Core.Parser.Ast.IncludeNode include => new SymbolInformationOrDocumentSymbol(
                new DocumentSymbol
                {
                    Name = $"include \"{include.Path}\"",
                    Kind = SymbolKind.File,
                    Range = SettexDocument.LocationToRange(include.Location),
                    SelectionRange = SettexDocument.LocationToRange(include.Location)
                }
            ),

            _ => null
        };
    }

    private static Container<DocumentSymbol>? CreateBlockChildren(
        System.Collections.Generic.IReadOnlyList<Core.Parser.Ast.IStatement> statements)
    {
        var children = new List<DocumentSymbol>();

        foreach (var statement in statements)
        {
            if (statement is Core.Parser.Ast.AssignmentNode assignment)
            {
                var pathStr = assignment.Path.ToString() ?? "?";
                children.Add(new DocumentSymbol
                {
                    Name = pathStr,
                    Kind = SymbolKind.Property,
                    Range = SettexDocument.LocationToRange(assignment.Location),
                    SelectionRange = SettexDocument.LocationToRange(assignment.Location)
                });
            }
            else if (statement is Core.Parser.Ast.LetNode let)
            {
                children.Add(new DocumentSymbol
                {
                    Name = let.Name,
                    Kind = SymbolKind.Variable,
                    Range = SettexDocument.LocationToRange(let.Location),
                    SelectionRange = SettexDocument.LocationToRange(let.Location)
                });
            }
            else if (statement is Core.Parser.Ast.NestedBlockNode nested)
            {
                // Nested blocks get represented as well
                var nestedSymbol = new DocumentSymbol
                {
                    Name = nested.Name,
                    Kind = SymbolKind.Object,
                    Range = SettexDocument.LocationToRange(nested.Location),
                    SelectionRange = SettexDocument.LocationToRange(nested.Location),
                    Children = CreateBlockChildren(nested.Block.Statements)
                };
                children.Add(nestedSymbol);
            }
        }

        return children.Count > 0 ? new Container<DocumentSymbol>(children) : null;
    }
}
