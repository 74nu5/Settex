namespace Settex.LanguageServer.Tests;

using Settex.Core.Lexer;
using Settex.Core.Parser;
using TUnit.Core;

/// <summary>
/// Tests for hover overlay tracking functionality.
/// </summary>
public class HoverOverlayTests
{
    [Test]
    public async Task FormatAssignmentWithOverlay_BaseBlock_ShowsBaseValue()
    {
        // Arrange
        const string source = @"settings {
    Server.Port = 443
}";

        var (ast, envName, path) = ParseAndGetContext(source, line: 2);

        // Act
        var result = SettexHoverTestHelper.FormatAssignmentWithOverlay(ast, path, envName);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!).Contains("**Setting:** `Server.Port`");
        await Assert.That(result!).Contains("443");
    }

    [Test]
    public async Task FormatAssignmentWithOverlay_EnvBlock_ShowsAllEnvironments()
    {
        // Arrange
        const string source = @"settings {
    Server.Port = 80
}

env ""Development"" {
    settings {
        Server.Port = 8080
    }
}

env ""Production"" {
    settings {
        Server.Port = 443
    }
}";

        var (ast, envName, path) = ParseAndGetContext(source, line: 7, envIndex: 0);

        // Act
        var result = SettexHoverTestHelper.FormatAssignmentWithOverlay(ast, path, envName);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!).Contains("**Setting:** `Server.Port`");
        // Should show base value
        await Assert.That(result!).Contains("80");
        // Should show development override (current env)
        await Assert.That(result!).Contains("8080");
        // Should highlight current environment
        await Assert.That(result!).Contains("Development");
    }

    [Test]
    public async Task FormatAssignmentWithOverlay_ValueInheritedFromBase_ShowsBaseValue()
    {
        // Arrange - Path only defined in base, env doesn't override it
        const string source = @"settings {
    App.Name = ""MyApp""
    Server.Port = 443
}

env ""Development"" {
    settings {
        App.Debug = true
    }
}";

        // Get the context for App.Name which is NOT overridden in dev
        var (ast, _, path) = ParseAndGetContext(source, line: 3);

        // Even in development context, we should see the base value for Server.Port
        var result = SettexHoverTestHelper.FormatAssignmentWithOverlay(ast, "Server.Port", "Development");

        // Assert - Should show the base value since it's not overridden
        await Assert.That(result).IsNotNull();
        await Assert.That(result!).Contains("443");
    }

    [Test]
    public async Task FormatAssignmentWithOverlay_NestedPath_ResolvesCorrectly()
    {
        // Arrange
        const string source = @"settings {
    Logging.LogLevel.Default = ""Information""
}

env ""Production"" {
    settings {
        Logging.LogLevel.Default = ""Warning""
    }
}";

        var (ast, envName, path) = ParseAndGetContext(source, line: 7, envIndex: 0);

        // Act
        var result = SettexHoverTestHelper.FormatAssignmentWithOverlay(ast, path, envName);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!).Contains("**Setting:** `Logging.LogLevel.Default`");
        await Assert.That(result!).Contains("Information");
        await Assert.That(result!).Contains("Warning");
    }

    [Test]
    public async Task FormatAssignmentWithOverlay_AllEnvironments_ListedWithCurrentHighlighted()
    {
        // Arrange - Multiple environments with different values
        const string source = @"settings {
    App.Port = 80
}

env ""Development"" {
    settings {
        App.Port = 3000
    }
}

env ""Staging"" {
    settings {
        App.Port = 8080
    }
}

env ""Production"" {
    settings {
        App.Port = 443
    }
}";

        var (ast, _, _) = ParseAndGetContext(source, line: 2);

        // Act - From Production context
        var result = SettexHoverTestHelper.FormatAssignmentWithOverlay(ast, "App.Port", "Production");

        // Assert - Should show all values
        await Assert.That(result).IsNotNull();
        await Assert.That(result!).Contains("80");   // Base
        await Assert.That(result!).Contains("3000"); // Development
        await Assert.That(result!).Contains("8080"); // Staging
        await Assert.That(result!).Contains("443");  // Production
        // Current environment should be highlighted
        await Assert.That(result!).Contains("**Production**");
    }

    private static (Core.Parser.Ast.FileNode Ast, string? EnvName, string Path) ParseAndGetContext(
        string source,
        int line,
        int envIndex = -1)
    {
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();

        // Find assignment at line
        string path = "";
        foreach (var stmt in ast.Statements)
        {
            if (stmt is Core.Parser.Ast.SettingsBlockNode settings)
            {
                foreach (var s in settings.Block.Statements)
                {
                    if (s is Core.Parser.Ast.AssignmentNode assignment && assignment.Location.Line == line)
                    {
                        path = string.Join(".", assignment.Path.Segments);
                    }
                }
            }
            else if (stmt is Core.Parser.Ast.EnvBlockNode env)
            {
                foreach (var s in env.SettingsBlock.Block.Statements)
                {
                    if (s is Core.Parser.Ast.AssignmentNode assignment && assignment.Location.Line == line)
                    {
                        path = string.Join(".", assignment.Path.Segments);
                    }
                }
            }
        }

        string? envName = null;
        if (envIndex >= 0)
        {
            var envBlocks = ast.Statements.OfType<Core.Parser.Ast.EnvBlockNode>().ToList();
            if (envIndex < envBlocks.Count)
            {
                envName = envBlocks[envIndex].EnvironmentName;
            }
        }

        return (ast, envName, path);
    }
}

/// <summary>
/// Helper class to expose internal hover formatting methods for testing.
/// </summary>
public static class SettexHoverTestHelper
{
    public static string? FormatAssignmentWithOverlay(
        Core.Parser.Ast.FileNode ast,
        string path,
        string? currentEnvName)
    {
        return HoverOverlayFormatter.FormatAssignmentWithOverlay(ast, path, currentEnvName, logger: null);
    }
}
