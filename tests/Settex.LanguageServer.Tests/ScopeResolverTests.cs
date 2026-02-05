using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Settex.Core.Lexer;
using Settex.Core.Parser;
using Settex.Core.Parser.Ast;

namespace Settex.LanguageServer.Tests;

public sealed class ScopeResolverTests
{
    [Test]
    public async Task BuildScopeHierarchy_GlobalLetVariables_AddsToGlobalScopeAsync()
    {
        // Arrange
        var source = """
            let basePort = 5000
            let host = "localhost"

            settings {
                Port = basePort
            }
            """;
        var ast = this.Parse(source);
        var resolver = new ScopeResolver();

        // Act
        var globalScope = resolver.BuildScopeHierarchy(ast);

        // Assert
        await Assert.That(globalScope.Type).IsEqualTo(ScopeType.Global);
        await Assert.That(globalScope.Variables).Count().IsEqualTo(2);
        await Assert.That(globalScope.Variables[0].Name).IsEqualTo("basePort");
        await Assert.That(globalScope.Variables[1].Name).IsEqualTo("host");
    }

    [Test]
    public async Task BuildScopeHierarchy_EnvBlock_CreatesEnvScopeAsync()
    {
        // Arrange
        var source = """
            settings {
                Port = 8080
            }

            env "Development" {
                let devPort = 4000

                settings {
                    Port = devPort
                }
            }
            """;
        var ast = this.Parse(source);
        var resolver = new ScopeResolver();

        // Act
        var globalScope = resolver.BuildScopeHierarchy(ast);

        // Assert
        await Assert.That(globalScope.Children).Count().IsEqualTo(1);

        var envScope = globalScope.Children[0];
        await Assert.That(envScope.Type).IsEqualTo(ScopeType.Env);
        await Assert.That(envScope.Name).IsEqualTo("Development");
        await Assert.That(envScope.Variables).Count().IsEqualTo(1);
        await Assert.That(envScope.Variables[0].Name).IsEqualTo("devPort");
    }

    [Test]
    public async Task BuildScopeHierarchy_ForLoop_CreatesForScopeAsync()
    {
        // Arrange
        var source = """
            let services = ["auth", "api"]

            settings {
                Services = [
                    for svc in services {
                        service { Name = svc }
                    }
                ]
            }
            """;
        var ast = this.Parse(source);
        var resolver = new ScopeResolver();

        // Act
        var globalScope = resolver.BuildScopeHierarchy(ast);

        // Assert - Le for scope devrait être créé sous le global scope
        // car il apparaît dans une expression (array dans settings)
        await Assert.That(globalScope.Children).Count().IsGreaterThanOrEqualTo(1);

        var forScope = this.FindFirstForScope(globalScope);
        await Assert.That(forScope).IsNotNull();
        await Assert.That(forScope!.Type).IsEqualTo(ScopeType.ForLoop);
        await Assert.That(forScope.Name).IsEqualTo("svc");
    }

    [Test]
    public async Task BuildScopeHierarchy_NestedScopes_BuildsCorrectHierarchyAsync()
    {
        // Arrange
        var source = """
            let globalVar = 100

            env "Production" {
                let envVar = 200

                settings {
                    Items = [
                        for item in [1, 2, 3] {
                            value { Number = item }
                        }
                    ]
                }
            }
            """;
        var ast = this.Parse(source);
        var resolver = new ScopeResolver();

        // Act
        var globalScope = resolver.BuildScopeHierarchy(ast);

        // Assert
        await Assert.That(globalScope.Variables).Count().IsEqualTo(1);
        await Assert.That(globalScope.Variables[0].Name).IsEqualTo("globalVar");

        var envScope = globalScope.Children[0];
        await Assert.That(envScope.Type).IsEqualTo(ScopeType.Env);
        await Assert.That(envScope.Variables).Count().IsEqualTo(1);
        await Assert.That(envScope.Variables[0].Name).IsEqualTo("envVar");

        var forScope = this.FindFirstForScope(envScope);
        await Assert.That(forScope).IsNotNull();
        await Assert.That(forScope!.Type).IsEqualTo(ScopeType.ForLoop);
        await Assert.That(forScope.Name).IsEqualTo("item");
    }

    [Test]
    public async Task FindVariableInScope_GlobalVariable_FoundAsync()
    {
        // Arrange
        var source = """
            let basePort = 5000

            settings { }
            """;
        var ast = this.Parse(source);
        var resolver = new ScopeResolver();
        var globalScope = resolver.BuildScopeHierarchy(ast);

        // Act
        var variable = resolver.FindVariableInScope("basePort", globalScope);

        // Assert
        await Assert.That(variable).IsNotNull();
        await Assert.That(variable!.Name).IsEqualTo("basePort");
    }

    [Test]
    public async Task FindVariableInScope_EnvVariableFromEnvScope_FoundAsync()
    {
        // Arrange
        var source = """
            let globalVar = 100

            env "Development" {
                let devVar = 200

                settings { }
            }
            """;
        var ast = this.Parse(source);
        var resolver = new ScopeResolver();
        var globalScope = resolver.BuildScopeHierarchy(ast);
        var envScope = globalScope.Children[0];

        // Act - depuis le scope env, on doit trouver devVar
        var variable = resolver.FindVariableInScope("devVar", envScope);

        // Assert
        await Assert.That(variable).IsNotNull();
        await Assert.That(variable!.Name).IsEqualTo("devVar");
    }

     [Test]
    public async Task FindVariableInScope_GlobalVariableFromEnvScope_FoundAsync()
    {
        // Arrange
        var source = """
            let globalVar = 100

            env "Development" {
                let devVar = 200

                settings { }
            }
            """;
        var ast = this.Parse(source);
        var resolver = new ScopeResolver();
        var globalScope = resolver.BuildScopeHierarchy(ast);
        var envScope = globalScope.Children[0];

        // Act - depuis le scope env, on doit aussi trouver globalVar (remontée)
        var variable = resolver.FindVariableInScope("globalVar", envScope);

        // Assert
        await Assert.That(variable).IsNotNull();
        await Assert.That(variable!.Name).IsEqualTo("globalVar");
    }

    [Test]
    public async Task FindVariableInScope_NonExistentVariable_ReturnsNullAsync()
    {
        // Arrange
        var source = """
            let basePort = 5000

            settings { }
            """;
        var ast = this.Parse(source);
        var resolver = new ScopeResolver();
        var globalScope = resolver.BuildScopeHierarchy(ast);

        // Act
        var variable = resolver.FindVariableInScope("nonExistent", globalScope);

        // Assert
        await Assert.That(variable).IsNull();
    }

    [Test]
    public async Task FindScopeAt_GlobalPosition_ReturnsGlobalScopeAsync()
    {
        // Arrange
        var source = """
            let basePort = 5000

            settings { }
            """;
        var ast = this.Parse(source);
        var resolver = new ScopeResolver();
        var globalScope = resolver.BuildScopeHierarchy(ast);

        // Act - Position au début du fichier (ligne 0, colonne 0)
        var scope = resolver.FindScopeAt(globalScope, new Position(0, 0));

        // Assert
        await Assert.That(scope).IsNotNull();
        await Assert.That(scope!.Type).IsEqualTo(ScopeType.Global);
    }

    [Test]
    public async Task FindScopeAt_EnvBlockPosition_ReturnsEnvScopeAsync()
    {
        // Arrange
        var source = """
            settings { }

            env "Development" {
                let devVar = 200

                settings { }
            }
            """;
        var ast = this.Parse(source);
        var resolver = new ScopeResolver();
        var globalScope = resolver.BuildScopeHierarchy(ast);

        // Act - Position dans le env block (ligne 3, après "let devVar")
        var scope = resolver.FindScopeAt(globalScope, new Position(3, 10));

        // Assert
        await Assert.That(scope).IsNotNull();
        await Assert.That(scope!.Type).IsEqualTo(ScopeType.Env);
        await Assert.That(scope.Name).IsEqualTo("Development");
    }

    // Helpers

    private FileNode Parse(string source)
    {
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        return parser.Parse();
    }

    private ScopeInfo? FindFirstForScope(ScopeInfo scope)
    {
        if (scope.Type == ScopeType.ForLoop)
        {
            return scope;
        }

        foreach (var child in scope.Children)
        {
            var result = this.FindFirstForScope(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
