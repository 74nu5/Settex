namespace Settex.Core.Tests.Evaluation;

using System.Threading.Tasks;
using Settex.Core.Evaluation;
using Settex.Core.Lexer;
using Settex.Core.Merging;
using Settex.Core.Parser;
using Settex.Core.Parser.Ast;
using Settex.Core.Resolution;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

public class VariableEvaluationTests
{
    private static SettingsModel CompileSource(string source)
    {
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens, "test.settex");
        var ast = parser.Parse();
        
        // Phase 2.5: Resolve includes (even though we don't have any in these tests)
        var includeResolver = new IncludeResolver();
        var resolvedStatements = includeResolver.ResolveIncludes(ast, "test.settex");
        
        // Rebuild FileNode with resolved statements
        var resolvedAst = new FileNode(resolvedStatements, ast.Location);
        
        var evaluator = new Evaluator();
        return evaluator.Evaluate(resolvedAst);
    }

    [Test]
    public async Task Evaluate_GlobalVariable_UsedInSettings()
    {
        var source = """
            let port = 8080

            settings {
                Server.Port = port
            }
            """;

        var model = CompileSource(source);

        var settingsJson = model.BaseSettings;
        await Assert.That(settingsJson).IsNotNull();
        await Assert.That(settingsJson!["Server"]).IsNotNull();
        await Assert.That(settingsJson["Server"]!["Port"]!.GetValue<long>()).IsEqualTo(8080L);
    }

    [Test]
    public async Task Evaluate_StringVariable_UsedInSettings()
    {
        var source = """
            let appName = "MyApp"

            settings {
                Application.Name = appName
            }
            """;

        var model = CompileSource(source);

        var settingsJson = model.BaseSettings;
        await Assert.That(settingsJson).IsNotNull();
        await Assert.That(settingsJson!["Application"]).IsNotNull();
        await Assert.That(settingsJson["Application"]!["Name"]!.GetValue<string>()).IsEqualTo("MyApp");
    }

    [Test]
    public async Task Evaluate_BoolVariable_UsedInSettings()
    {
        var source = """
            let debugMode = true

            settings {
                Application.Debug = debugMode
            }
            """;

        var model = CompileSource(source);

        var settingsJson = model.BaseSettings;
        await Assert.That(settingsJson).IsNotNull();
        await Assert.That(settingsJson!["Application"]).IsNotNull();
        await Assert.That(settingsJson["Application"]!["Debug"]!.GetValue<bool>()).IsEqualTo(true);
    }

    [Test]
    public async Task Evaluate_EnvVariable_ShadowsGlobal()
    {
        var source = """
            let port = 8080

            settings {
                Server.Port = port
            }

            env "Production" {
                let port = 443
                
                settings {
                    Server.Port = port
                }
            }
            """;

        var model = CompileSource(source);

        // Base settings should use global port
        var baseJson = model.BaseSettings;
        await Assert.That(baseJson).IsNotNull();
        await Assert.That(baseJson!["Server"]!["Port"]!.GetValue<long>()).IsEqualTo(8080L);

        // Production env should use env port
        var prodOverlay = model.EnvironmentOverlays["Production"];
        await Assert.That(prodOverlay).IsNotNull();
        await Assert.That(prodOverlay!["Server"]!["Port"]!.GetValue<long>()).IsEqualTo(443L);
    }

    [Test]
    public async Task Evaluate_VariableInArray()
    {
        var source = """
            let host = "localhost"
            let port = 8080

            settings {
                AllowedHosts = [host]
                Ports = [port, 9090]
            }
            """;

        var model = CompileSource(source);

        var settingsJson = model.BaseSettings;
        await Assert.That(settingsJson).IsNotNull();
        
        var hosts = settingsJson!["AllowedHosts"]!.AsArray();
        await Assert.That(hosts.Count).IsEqualTo(1);
        await Assert.That(hosts[0]!.GetValue<string>()).IsEqualTo("localhost");

        var ports = settingsJson["Ports"]!.AsArray();
        await Assert.That(ports.Count).IsEqualTo(2);
        await Assert.That(ports[0]!.GetValue<long>()).IsEqualTo(8080L);
        await Assert.That(ports[1]!.GetValue<long>()).IsEqualTo(9090L);
    }

    [Test]
    public async Task Evaluate_UndefinedVariable_ThrowsException()
    {
        var source = """
            settings {
                Server.Port = unknownVariable
            }
            """;

        await Assert.ThrowsAsync<EvaluatorException>(() => Task.FromResult(CompileSource(source)));
    }

    [Test]
    public async Task Evaluate_MultipleGlobalVariables()
    {
        var source = """
            let host = "localhost"
            let port = 8080
            let enableSsl = false

            settings {
                Server.Host = host
                Server.Port = port
                Server.EnableSsl = enableSsl
            }
            """;

        var model = CompileSource(source);

        var settingsJson = model.BaseSettings;
        await Assert.That(settingsJson).IsNotNull();
        await Assert.That(settingsJson!["Server"]).IsNotNull();
        
        var server = settingsJson["Server"]!.AsObject();
        await Assert.That(server["Host"]!.GetValue<string>()).IsEqualTo("localhost");
        await Assert.That(server["Port"]!.GetValue<long>()).IsEqualTo(8080L);
        await Assert.That(server["EnableSsl"]!.GetValue<bool>()).IsEqualTo(false);
    }

    [Test]
    public async Task Evaluate_VariableInNestedObject()
    {
        var source = """
            let level = "Information"

            settings {
                Logging {
                    LogLevel {
                        Default = level
                    }
                }
            }
            """;

        var model = CompileSource(source);

        var settingsJson = model.BaseSettings;
        await Assert.That(settingsJson).IsNotNull();
        
        var logging = settingsJson!["Logging"]!.AsObject();
        var logLevel = logging["LogLevel"]!.AsObject();
        await Assert.That(logLevel["Default"]!.GetValue<string>()).IsEqualTo("Information");
    }
}
