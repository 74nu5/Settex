using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Settex.Core.Diagnostics;
using Settex.Core.Parser.Ast;

namespace Settex.LanguageServer;

/// <summary>
/// Résout les scopes lexicaux dans un fichier Settex.
/// Construit la hiérarchie de scopes (global → env → for) et permet de trouver
/// le scope actif à une position donnée ou de résoudre une variable.
/// </summary>
public class ScopeResolver
{
    /// <summary>
    /// Construit la hiérarchie complète des scopes à partir de l'AST.
    /// </summary>
    public ScopeInfo BuildScopeHierarchy(FileNode ast)
    {
        // Créer le scope global (racine)
        var globalScope = new ScopeInfo(
            ScopeType.Global,
            ast.Location,
            parent: null,
            name: null);

        // Le scope global couvre l'intégralité du fichier : toute position qui
        // n'appartient pas à un scope enfant (env/for) retombe sur le global.
        globalScope.ExtendTo(int.MaxValue);

        // Parcourir les statements top-level
        foreach (var statement in ast.Statements)
        {
            this.ProcessTopLevelStatement(statement, globalScope);
        }

        return globalScope;
    }

    /// <summary>
    /// Trouve le scope actif à une position donnée (ligne, colonne).
    /// Retourne le scope le plus spécifique (le plus profond dans la hiérarchie).
    /// </summary>
    public ScopeInfo? FindScopeAt(ScopeInfo rootScope, Position position)
    {
        return this.FindScopeAtRecursive(rootScope, position.Line + 1, position.Character + 1);
    }

    /// <summary>
    /// Résout une variable dans un scope donné (avec remontée aux scopes parents).
    /// </summary>
    public LetNode? FindVariableInScope(string name, ScopeInfo scope)
    {
        return scope.FindVariable(name);
    }

    private ScopeInfo? FindScopeAtRecursive(ScopeInfo scope, int line, int column)
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
            var childResult = this.FindScopeAtRecursive(child, line, column);
            if (childResult != null)
            {
                return childResult;
            }
        }

        // Aucun enfant ne contient la position, ce scope est le plus spécifique
        return scope;
    }

    private void ProcessTopLevelStatement(ITopLevelStatement statement, ScopeInfo parentScope)
    {
        switch (statement)
        {
            case LetNode letNode:
                // Ajouter la variable au scope courant
                parentScope.Variables.Add(letNode);
                break;

            case EnvBlockNode envNode:
                // Créer un scope pour l'environnement
                var envScope = new ScopeInfo(
                    ScopeType.Env,
                    envNode.Location,
                    parent: parentScope,
                    name: envNode.EnvironmentName);

                parentScope.Children.Add(envScope);

                // Parcourir le contenu de l'env block
                this.ProcessSettingsBlock(envNode.SettingsBlock, envScope);

                // Faire remonter l'étendue de l'env vers le scope parent.
                parentScope.ExtendTo(envScope.EndLine);
                break;

            case SettingsBlockNode settingsNode:
                // Le settings block au top-level partage le scope global
                this.ProcessBlock(settingsNode.Block, parentScope);
                break;

            // IncludeNode n'affecte pas les scopes (déjà résolu par le compilateur)
        }
    }

    private void ProcessSettingsBlock(SettingsBlockNode settingsBlock, ScopeInfo parentScope)
    {
        this.ProcessBlock(settingsBlock.Block, parentScope);
    }

    private void ProcessBlock(BlockNode block, ScopeInfo parentScope)
    {
        foreach (var statement in block.Statements)
        {
            this.ProcessStatement(statement, parentScope);
        }
    }

    private void ProcessStatement(IStatement statement, ScopeInfo parentScope)
    {
        // Ce statement appartient au scope courant : étendre sa couverture.
        parentScope.ExtendTo(statement.Location.Line);

        switch (statement)
        {
            case LetNode letNode:
                // Ajouter la variable au scope courant
                parentScope.Variables.Add(letNode);
                break;

            case AssignmentNode assignmentNode:
                // Parcourir la valeur pour trouver d'éventuels ForNode
                this.ProcessExpression(assignmentNode.Value, parentScope);
                // Parcourir la condition si présente
                if (assignmentNode.Condition != null)
                {
                    this.ProcessExpression(assignmentNode.Condition, parentScope);
                }
                break;

            case NestedBlockNode nestedBlockNode:
                // Parcourir le bloc imbriqué
                this.ProcessBlock(nestedBlockNode.Block, parentScope);
                break;
        }
    }

    private void ProcessExpression(IExpression expression, ScopeInfo parentScope)
    {
        // Les expressions (arrays multi-lignes, etc.) élargissent le scope courant.
        parentScope.ExtendTo(expression.Location.Line);

        switch (expression)
        {
            case ArrayNode arrayNode:
                // Parcourir les éléments du tableau
                foreach (var element in arrayNode.Elements)
                {
                    this.ProcessArrayElement(element, parentScope);
                }
                break;

            case BinaryOpNode binaryOp:
                this.ProcessExpression(binaryOp.Left, parentScope);
                this.ProcessExpression(binaryOp.Right, parentScope);
                break;

            case UnaryOpNode unaryOp:
                this.ProcessExpression(unaryOp.Operand, parentScope);
                break;

            case InterpolatedStringNode interpolatedString:
                foreach (var segment in interpolatedString.Segments)
                {
                    if (segment is ExpressionSegment exprSegment)
                    {
                        this.ProcessExpression(exprSegment.Expression, parentScope);
                    }
                }
                break;

            case MemberAccessNode memberAccess:
                this.ProcessExpression(memberAccess.Object, parentScope);
                break;

            case TaggedObjectNode taggedObject:
                this.ProcessBlock(taggedObject.Block, parentScope);
                break;

            // Les autres expressions (literals, variables, etc.) n'ont pas de scopes imbriqués
        }
    }

    private void ProcessArrayElement(IArrayElement element, ScopeInfo parentScope)
    {
        switch (element)
        {
            case ForNode forNode:
                // Créer un scope pour la boucle for
                var forScope = new ScopeInfo(
                    ScopeType.ForLoop,
                    forNode.Location,
                    parent: parentScope,
                    name: forNode.IteratorName);

                parentScope.Children.Add(forScope);

                // L'itérateur est une variable implicite : on l'enregistre comme un
                // LetNode synthétique pointant sur la boucle. Sans lui, résoudre
                // l'itérateur dans le corps remontait au scope parent et tombait sur
                // une variable homonyme externe (mauvais go-to-definition). Sa valeur
                // référence la collection parcourue — voir le hover, qui l'affiche
                // explicitement comme un itérateur.
                forScope.Variables.Add(new LetNode(forNode.IteratorName, forNode.Collection, forNode.Location));

                // Parcourir le corps de la boucle
                this.ProcessBlock(forNode.Body, forScope);

                // Faire remonter l'étendue de la boucle vers le scope parent.
                parentScope.ExtendTo(forScope.EndLine);

                // Parcourir la collection pour trouver d'éventuels for imbriqués
                this.ProcessExpression(forNode.Collection, parentScope);
                break;

            case IExpression expr:
                // Les autres éléments de tableau sont des expressions
                this.ProcessExpression(expr, parentScope);
                break;
        }
    }
}
