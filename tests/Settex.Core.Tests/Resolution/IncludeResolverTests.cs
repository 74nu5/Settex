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
    /// <summary>
    ///     An absolute directory to anchor path resolution on. Built from the platform's
    ///     own separators rather than hard-coded, so these tests assert on resolution
    ///     behaviour instead of on Windows drive-letter syntax. Nothing is created on
    ///     disk: <see cref="IncludeResolver.ResolveIncludePath" /> is a pure path operation.
    /// </summary>
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "settex-project"));

    [Test]
    public async Task ResolveIncludePath_WithRelativePath_ReturnsAbsolutePath()
    {
        var resolver = new IncludeResolver();
        var currentFile = Path.Combine(ProjectRoot, "main.settex");
        var includePath = "./common.settex";

        var result = resolver.ResolveIncludePath(includePath, currentFile);

        await Assert.That(result).IsEqualTo(Path.Combine(ProjectRoot, "common.settex"));
    }

    [Test]
    public async Task ResolveIncludePath_WithNestedRelativePath_ReturnsAbsolutePath()
    {
        var resolver = new IncludeResolver();
        var currentFile = Path.Combine(ProjectRoot, "src", "main.settex");
        var includePath = "../config/common.settex";

        var result = resolver.ResolveIncludePath(includePath, currentFile);

        await Assert.That(result).IsEqualTo(Path.Combine(ProjectRoot, "config", "common.settex"));
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

    [Test]
    public async Task ResolveIncludes_WithDiamondInclude_AppliesSharedFileOnce()
    {
        // Diamond: main -> {b, c}, and both b and c include the shared file d.
        // d must be merged exactly once, so the flattened list is d, b, c, main
        // (4 statements) rather than d, b, d, c, main (5).
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "d.settex"), """settings { D.Value = "d" }""");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "b.settex"), "include \"./d.settex\"\nsettings { B.Value = \"b\" }");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "c.settex"), "include \"./d.settex\"\nsettings { C.Value = \"c\" }");

            var mainFile = Path.Combine(tempDir, "main.settex");
            await File.WriteAllTextAsync(mainFile, "include \"./b.settex\"\ninclude \"./c.settex\"\nsettings { Main.Value = \"m\" }");

            var lexer = new Lexer(await File.ReadAllTextAsync(mainFile), mainFile);
            var parser = new Parser(lexer.Tokenize(), mainFile);
            var ast = parser.Parse();

            var resolver = new IncludeResolver();
            var resolved = resolver.ResolveIncludes(ast, mainFile);

            await Assert.That(resolved.Count).IsEqualTo(4);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ResolveIncludes_ExceedingDepthLimit_ThrowsIncludeException()
    {
        // A long, non-circular linear chain (deeper than the resolver's bound)
        // must fail with a diagnostic rather than a StackOverflowException.
        const int chainLength = 70;
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            for (var i = 0; i < chainLength; i++)
            {
                var body = i < chainLength - 1
                    ? $"include \"./f{i + 1}.settex\""
                    : "settings { Leaf.Value = \"end\" }";
                await File.WriteAllTextAsync(Path.Combine(tempDir, $"f{i}.settex"), body);
            }

            var rootFile = Path.Combine(tempDir, "f0.settex");
            var lexer = new Lexer(await File.ReadAllTextAsync(rootFile), rootFile);
            var parser = new Parser(lexer.Tokenize(), rootFile);
            var ast = parser.Parse();

            var resolver = new IncludeResolver();

            var exception = await Assert.That(() => resolver.ResolveIncludes(ast, rootFile)).ThrowsException();
            await Assert.That(exception).IsTypeOf<IncludeException>();
            await Assert.That(exception!.Message).Contains("depth limit");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ResolveIncludes_DiamondWithDifferentCasing_DedupsOnWindows()
    {
        // On a case-insensitive filesystem, ./d.settex and ./D.settex are the same
        // file and must still be deduped. This behaviour is host-specific, so the
        // assertion only runs on Windows (elsewhere the casing refers to a distinct,
        // non-existent file).
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "d.settex"), """settings { D.Value = "d" }""");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "b.settex"), "include \"./d.settex\"\nsettings { B.Value = \"b\" }");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "c.settex"), "include \"./D.settex\"\nsettings { C.Value = \"c\" }");

            var mainFile = Path.Combine(tempDir, "main.settex");
            await File.WriteAllTextAsync(mainFile, "include \"./b.settex\"\ninclude \"./c.settex\"\nsettings { Main.Value = \"m\" }");

            var lexer = new Lexer(await File.ReadAllTextAsync(mainFile), mainFile);
            var parser = new Parser(lexer.Tokenize(), mainFile);
            var ast = parser.Parse();

            var resolver = new IncludeResolver();
            var resolved = resolver.ResolveIncludes(ast, mainFile);

            // d.settex deduped despite the different casing in c.settex.
            await Assert.That(resolved.Count).IsEqualTo(4);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
