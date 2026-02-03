namespace Settex.Core.Tests.Parser;

using Settex.Core.Lexer;
using Settex.Core.Parser;
using Settex.Core.Parser.Ast;

public sealed class ParserTests
{
    [Test]
    public async Task ParseFile_EmptySettingsBlock_ReturnsFileNode()
    {
        // Arrange
        var source = "settings { }";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        await Assert.That(fileNode.Statements).Count().IsEqualTo(1);
        await Assert.That(fileNode.Statements[0]).IsTypeOf<SettingsBlockNode>();

        var settingsBlock = (SettingsBlockNode)fileNode.Statements[0];
        await Assert.That(settingsBlock.Block.Statements).Count().IsEqualTo(0);
    }

    [Test]
    public async Task ParseFile_SettingsWithSimpleAssignment_ParsesCorrectly()
    {
        // Arrange
        var source = """
                     settings {
                       ApplicationName = "Shop"
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        var settingsBlock = (SettingsBlockNode)fileNode.Statements[0];
        await Assert.That(settingsBlock.Block.Statements).Count().IsEqualTo(1);

        var assignment = (AssignmentNode)settingsBlock.Block.Statements[0];
        await Assert.That(assignment.Path.Segments).Count().IsEqualTo(1);
        await Assert.That(assignment.Path.Segments[0]).IsEqualTo("ApplicationName");

        var literal = (LiteralNode)assignment.Value;
        await Assert.That(literal.Value).IsEqualTo("Shop");
    }

    [Test]
    public async Task ParseFile_SettingsWithDotPath_ParsesCorrectly()
    {
        // Arrange
        var source = """
                     settings {
                       Logging.LogLevel.Default = "Debug"
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        var settingsBlock = (SettingsBlockNode)fileNode.Statements[0];
        var assignment = (AssignmentNode)settingsBlock.Block.Statements[0];

        await Assert.That(assignment.Path.Segments).Count().IsEqualTo(3);
        await Assert.That(assignment.Path.Segments[0]).IsEqualTo("Logging");
        await Assert.That(assignment.Path.Segments[1]).IsEqualTo("LogLevel");
        await Assert.That(assignment.Path.Segments[2]).IsEqualTo("Default");
    }

    [Test]
    public async Task ParseFile_SettingsWithNestedBlock_ParsesCorrectly()
    {
        // Arrange
        var source = """
                     settings {
                       Server {
                         Host = "localhost"
                         Port = 8080
                       }
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        var settingsBlock = (SettingsBlockNode)fileNode.Statements[0];
        await Assert.That(settingsBlock.Block.Statements).Count().IsEqualTo(1);

        var nestedBlock = (NestedBlockNode)settingsBlock.Block.Statements[0];
        await Assert.That(nestedBlock.Name).IsEqualTo("Server");
        await Assert.That(nestedBlock.Block.Statements).Count().IsEqualTo(2);

        var assignment1 = (AssignmentNode)nestedBlock.Block.Statements[0];
        await Assert.That(assignment1.Path.Segments[0]).IsEqualTo("Host");
        await Assert.That(((LiteralNode)assignment1.Value).Value).IsEqualTo("localhost");

        var assignment2 = (AssignmentNode)nestedBlock.Block.Statements[1];
        await Assert.That(assignment2.Path.Segments[0]).IsEqualTo("Port");
        await Assert.That(((LiteralNode)assignment2.Value).Value).IsEqualTo(8080L);
    }

    [Test]
    public async Task ParseFile_ArrayWithLiterals_ParsesCorrectly()
    {
        // Arrange
        var source = """
                     settings {
                       AllowedHosts = ["localhost", "example.com"]
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        var settingsBlock = (SettingsBlockNode)fileNode.Statements[0];
        var assignment = (AssignmentNode)settingsBlock.Block.Statements[0];

        var array = (ArrayNode)assignment.Value;
        await Assert.That(array.Items).Count().IsEqualTo(2);
        await Assert.That(((LiteralNode)array.Items[0]).Value).IsEqualTo("localhost");
        await Assert.That(((LiteralNode)array.Items[1]).Value).IsEqualTo("example.com");
    }

    [Test]
    public async Task ParseFile_ArrayWithNewlineSeparator_ParsesCorrectly()
    {
        // Arrange
        var source = """
                     settings {
                       Ports = [
                         8080
                         8081
                         8082
                       ]
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        var settingsBlock = (SettingsBlockNode)fileNode.Statements[0];
        var assignment = (AssignmentNode)settingsBlock.Block.Statements[0];

        var array = (ArrayNode)assignment.Value;
        await Assert.That(array.Items).Count().IsEqualTo(3);
        await Assert.That(((LiteralNode)array.Items[0]).Value).IsEqualTo(8080L);
        await Assert.That(((LiteralNode)array.Items[1]).Value).IsEqualTo(8081L);
        await Assert.That(((LiteralNode)array.Items[2]).Value).IsEqualTo(8082L);
    }

    [Test]
    public async Task ParseFile_ArrayWithTaggedObjects_ParsesCorrectly()
    {
        // Arrange
        var source = """
                     settings {
                       Services = [
                         service {
                           Name = "API"
                           Port = 5000
                         }
                         service {
                           Name = "Web"
                           Port = 3000
                         }
                       ]
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        var settingsBlock = (SettingsBlockNode)fileNode.Statements[0];
        var assignment = (AssignmentNode)settingsBlock.Block.Statements[0];

        var array = (ArrayNode)assignment.Value;
        await Assert.That(array.Items).Count().IsEqualTo(2);

        var obj1 = (TaggedObjectNode)array.Items[0];
        await Assert.That(obj1.Tag).IsEqualTo("service");
        await Assert.That(obj1.Block.Statements).Count().IsEqualTo(2);

        var obj2 = (TaggedObjectNode)array.Items[1];
        await Assert.That(obj2.Tag).IsEqualTo("service");
        await Assert.That(obj2.Block.Statements).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ParseFile_TaggedObjectAsValue_ParsesCorrectly()
    {
        // Arrange
        var source = """
                     settings {
                       Database = connection {
                         Host = "localhost"
                         Port = 5432
                       }
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        var settingsBlock = (SettingsBlockNode)fileNode.Statements[0];
        var assignment = (AssignmentNode)settingsBlock.Block.Statements[0];

        var taggedObj = (TaggedObjectNode)assignment.Value;
        await Assert.That(taggedObj.Tag).IsEqualTo("connection");
        await Assert.That(taggedObj.Block.Statements).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ParseFile_EnvBlock_ParsesCorrectly()
    {
        // Arrange
        var source = """
                     settings {
                       Port = 8080
                     }

                     env "Development" {
                       settings {
                         Port = 5000
                       }
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        await Assert.That(fileNode.Statements).Count().IsEqualTo(2);

        var settingsBlock = (SettingsBlockNode)fileNode.Statements[0];
        await Assert.That(settingsBlock.Block.Statements).Count().IsEqualTo(1);

        var envBlock = (EnvBlockNode)fileNode.Statements[1];
        await Assert.That(envBlock.EnvironmentName).IsEqualTo("Development");
        await Assert.That(envBlock.SettingsBlock.Block.Statements).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ParseFile_MultipleEnvBlocks_ParsesCorrectly()
    {
        // Arrange
        var source = """
                     settings {
                       Port = 8080
                     }

                     env "Development" {
                       settings {
                         Port = 5000
                       }
                     }

                     env "Production" {
                       settings {
                         Port = 80
                       }
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        await Assert.That(fileNode.Statements).Count().IsEqualTo(3);

        var env1 = (EnvBlockNode)fileNode.Statements[1];
        await Assert.That(env1.EnvironmentName).IsEqualTo("Development");

        var env2 = (EnvBlockNode)fileNode.Statements[2];
        await Assert.That(env2.EnvironmentName).IsEqualTo("Production");
    }

    [Test]
    public async Task ParseFile_AllLiteralTypes_ParsesCorrectly()
    {
        // Arrange
        var source = """
                     settings {
                       StringValue = "test"
                       IntValue = 42
                       FloatValue = 3.14
                       TrueValue = true
                       FalseValue = false
                       NullValue = null
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        var settingsBlock = (SettingsBlockNode)fileNode.Statements[0];
        await Assert.That(settingsBlock.Block.Statements).Count().IsEqualTo(6);

        var stmt1 = (AssignmentNode)settingsBlock.Block.Statements[0];
        await Assert.That(((LiteralNode)stmt1.Value).Value).IsEqualTo("test");

        var stmt2 = (AssignmentNode)settingsBlock.Block.Statements[1];
        await Assert.That(((LiteralNode)stmt2.Value).Value).IsEqualTo(42L);

        var stmt3 = (AssignmentNode)settingsBlock.Block.Statements[2];
        await Assert.That(((LiteralNode)stmt3.Value).Value).IsEqualTo(3.14);

        var stmt4 = (AssignmentNode)settingsBlock.Block.Statements[3];
        await Assert.That(((LiteralNode)stmt4.Value).Value).IsEqualTo(true);

        var stmt5 = (AssignmentNode)settingsBlock.Block.Statements[4];
        await Assert.That(((LiteralNode)stmt5.Value).Value).IsEqualTo(false);

        var stmt6 = (AssignmentNode)settingsBlock.Block.Statements[5];
        await Assert.That(((LiteralNode)stmt6.Value).Value).IsNull();
    }

    [Test]
    public async Task ParseFile_ComplexExample_ParsesCorrectly()
    {
        // Arrange
        var source = """
                     settings {
                       ApplicationName = "MyApp"
                       
                       Server {
                         Host = "0.0.0.0"
                         Port = 8080
                         AllowedHosts = ["localhost", "*.example.com"]
                       }
                       
                       Logging {
                         LogLevel {
                           Default = "Information"
                           Microsoft = "Warning"
                         }
                       }
                     }

                     env "Development" {
                       settings {
                         Server.Port = 5000
                         Logging.LogLevel.Default = "Debug"
                       }
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        await Assert.That(fileNode.Statements).Count().IsEqualTo(2);

        var settingsBlock = (SettingsBlockNode)fileNode.Statements[0];
        await Assert.That(settingsBlock.Block.Statements).Count().IsEqualTo(3);

        var envBlock = (EnvBlockNode)fileNode.Statements[1];
        await Assert.That(envBlock.EnvironmentName).IsEqualTo("Development");
        await Assert.That(envBlock.SettingsBlock.Block.Statements).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ParseFile_MissingSettingsKeyword_ThrowsException()
    {
        // Arrange
        var source = "{ }";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act & Assert
        await Assert.ThrowsAsync<ParserException>(() => Task.FromResult(parser.ParseFile()));
    }

    [Test]
    public async Task ParseFile_UnclosedBlock_ThrowsException()
    {
        // Arrange
        var source = "settings {";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act & Assert
        await Assert.ThrowsAsync<ParserException>(() => Task.FromResult(parser.ParseFile()));
    }

    [Test]
    public async Task ParseFile_MissingEquals_ThrowsException()
    {
        // Arrange
        var source = """
                     settings {
                       Port 8080
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act & Assert
        await Assert.ThrowsAsync<ParserException>(() => Task.FromResult(parser.ParseFile()));
    }

    [Test]
    public async Task ParseFile_EmptyArray_ParsesCorrectly()
    {
        // Arrange
        var source = """
                     settings {
                       Items = []
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        var settingsBlock = (SettingsBlockNode)fileNode.Statements[0];
        var assignment = (AssignmentNode)settingsBlock.Block.Statements[0];

        var array = (ArrayNode)assignment.Value;
        await Assert.That(array.Items).Count().IsEqualTo(0);
    }

    [Test]
    public async Task ParseFile_ArrayWithTrailingComma_ParsesCorrectly()
    {
        // Arrange
        var source = """
                     settings {
                       Items = [1, 2, 3,]
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        var settingsBlock = (SettingsBlockNode)fileNode.Statements[0];
        var assignment = (AssignmentNode)settingsBlock.Block.Statements[0];

        var array = (ArrayNode)assignment.Value;
        await Assert.That(array.Items).Count().IsEqualTo(3);
    }

    [Test]
    public async Task ParseFile_IncludeStatement_ParsesCorrectly()
    {
        // Arrange
        var source = """
                     include "./common.settex"
                     
                     settings {
                       ApplicationName = "Shop"
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        await Assert.That(fileNode.Statements).Count().IsEqualTo(2);
        await Assert.That(fileNode.Statements[0]).IsTypeOf<IncludeNode>();
        await Assert.That(fileNode.Statements[1]).IsTypeOf<SettingsBlockNode>();

        var includeNode = (IncludeNode)fileNode.Statements[0];
        await Assert.That(includeNode.Path).IsEqualTo("./common.settex");
    }

    [Test]
    public async Task ParseFile_MultipleIncludesAndSettings_ParsesCorrectly()
    {
        // Arrange
        var source = """
                     include "./common.settex"
                     include "./logging.settex"
                     
                     settings {
                       ApplicationName = "Shop"
                     }
                     
                     env "Development" {
                       settings {
                         Debug = true
                       }
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        await Assert.That(fileNode.Statements).Count().IsEqualTo(4);
        await Assert.That(fileNode.Statements[0]).IsTypeOf<IncludeNode>();
        await Assert.That(fileNode.Statements[1]).IsTypeOf<IncludeNode>();
        await Assert.That(fileNode.Statements[2]).IsTypeOf<SettingsBlockNode>();
        await Assert.That(fileNode.Statements[3]).IsTypeOf<EnvBlockNode>();
    }

    [Test]
    public async Task ParseFile_LetStatement_ParsesCorrectly()
    {
        // Arrange
        var source = """
                     let port = 5000
                     
                     settings {
                       Server.Port = 8080
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        await Assert.That(fileNode.Statements).Count().IsEqualTo(2);
        await Assert.That(fileNode.Statements[0]).IsTypeOf<LetNode>();
        await Assert.That(fileNode.Statements[1]).IsTypeOf<SettingsBlockNode>();

        var letNode = (LetNode)fileNode.Statements[0];
        await Assert.That(letNode.Name).IsEqualTo("port");
        await Assert.That(letNode.Value).IsTypeOf<LiteralNode>();
    }

    [Test]
    public async Task ParseFile_LetWithVariableReference_ParsesCorrectly()
    {
        // Arrange
        var source = """
                     let basePort = 5000
                     let apiPort = basePort
                     
                     settings {
                       Server.Port = 8080
                     }
                     """;

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        // Act
        var fileNode = parser.ParseFile();

        // Assert
        await Assert.That(fileNode.Statements).Count().IsEqualTo(3);
        
        var let1 = (LetNode)fileNode.Statements[0];
        await Assert.That(let1.Name).IsEqualTo("basePort");
        await Assert.That(let1.Value).IsTypeOf<LiteralNode>();

        var let2 = (LetNode)fileNode.Statements[1];
        await Assert.That(let2.Name).IsEqualTo("apiPort");
        await Assert.That(let2.Value).IsTypeOf<VariableRefNode>();
        
        var varRef = (VariableRefNode)let2.Value;
        await Assert.That(varRef.Name).IsEqualTo("basePort");
    }
}
