namespace Settex.Core.Tests.Resolution;

using Settex.Core.Lexer;
using Settex.Core.Parser;
using Settex.Core.Parser.Ast;
using Settex.Core.Resolution;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
///     Tests for the IncludeResolver.
/// </summary>
public class IncludeResolverTests
{
    [Test]
    public async Task ResolveIncludePath_WithRelativePath_ReturnsAbsolutePath()
    {
        var resolver = new IncludeResolver();
        var currentFile = @"D:\project\main.settex";
        var includePath = "./common.settex";

        var result = resolver.ResolveIncludePath(includePath, currentFile);

        await Assert.That(result).IsEqualTo(@"D:\project\common.settex");
    }

    [Test]
    public async Task ResolveIncludePath_WithNestedRelativePath_ReturnsAbsolutePath()
    {
        var resolver = new IncludeResolver();
        var currentFile = @"D:\project\src\main.settex";
        var includePath = "../config/common.settex";

        var result = resolver.ResolveIncludePath(includePath, currentFile);

        await Assert.That(result).IsEqualTo(@"D:\project\config\common.settex");
    }

    [Test]
    public async Task LoadAndParseFile_WithMissingFile_ThrowsIncludeException()
    {
        var resolver = new IncludeResolver();
        var filePath = @"D:\nonexistent\file.settex";

        var exception = await Assert.That(() => resolver.LoadAndParseFile(filePath)).ThrowsException();
        await Assert.That(exception).IsTypeOf<IncludeException>();
    }

    [Test]
    public async Task DetectCycle_WithoutCycle_ReturnsFalse()
    {
        var resolver = new IncludeResolver();

        var result = resolver.DetectCycle(@"D:\project\main.settex");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ResolveIncludes_WithSimpleInclude_ConcatenatesStatements()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var commonFile = Path.Combine(tempDir, "common.settex");
            var mainFile = Path.Combine(tempDir, "main.settex");

            await File.WriteAllTextAsync(
                commonFile,
                """
                settings {
                    Common.Value = "FromCommon"
                }
                """
            );

            await File.WriteAllTextAsync(
                mainFile,
                """
                include "./common.settex"
                
                settings {
                    Main.Value = "FromMain"
                }
                """
            );

            var lexer = new Lexer(await File.ReadAllTextAsync(mainFile), mainFile);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens, mainFile);
            var ast = parser.Parse();

            var resolver = new IncludeResolver();
            var resolved = resolver.ResolveIncludes(ast, mainFile);

            await Assert.That(resolved.Count).IsEqualTo(2);
            await Assert.That(resolved[0]).IsTypeOf<SettingsBlockNode>();
            await Assert.That(resolved[1]).IsTypeOf<SettingsBlockNode>();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ResolveIncludes_WithCircularInclude_ThrowsIncludeException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var fileA = Path.Combine(tempDir, "a.settex");
            var fileB = Path.Combine(tempDir, "b.settex");

            await File.WriteAllTextAsync(fileA, """include "./b.settex" """);
            await File.WriteAllTextAsync(fileB, """include "./a.settex" """);

            var lexer = new Lexer(await File.ReadAllTextAsync(fileA), fileA);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens, fileA);
            var ast = parser.Parse();

            var resolver = new IncludeResolver();

            var exception = await Assert.That(() => resolver.ResolveIncludes(ast, fileA)).ThrowsException();
            await Assert.That(exception).IsTypeOf<IncludeException>();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ResolveIncludes_WithNestedIncludes_ResolvesProperly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var baseFile = Path.Combine(tempDir, "base.settex");
            var middleFile = Path.Combine(tempDir, "middle.settex");
            var mainFile = Path.Combine(tempDir, "main.settex");

            await File.WriteAllTextAsync(
                baseFile,
                """
                settings {
                    Base.Value = "FromBase"
                }
                """
            );

            await File.WriteAllTextAsync(
                middleFile,
                """
                include "./base.settex"
                
                settings {
                    Middle.Value = "FromMiddle"
                }
                """
            );

            await File.WriteAllTextAsync(
                mainFile,
                """
                include "./middle.settex"
                
                settings {
                    Main.Value = "FromMain"
                }
                """
            );

            var lexer = new Lexer(await File.ReadAllTextAsync(mainFile), mainFile);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens, mainFile);
            var ast = parser.Parse();

            var resolver = new IncludeResolver();
            var resolved = resolver.ResolveIncludes(ast, mainFile);

            await Assert.That(resolved.Count).IsEqualTo(3);
            await Assert.That(resolved[0]).IsTypeOf<SettingsBlockNode>();
            await Assert.That(resolved[1]).IsTypeOf<SettingsBlockNode>();
            await Assert.That(resolved[2]).IsTypeOf<SettingsBlockNode>();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
