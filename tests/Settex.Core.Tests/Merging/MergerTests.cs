namespace Settex.Core.Tests.Merging;

using System.Text.Json.Nodes;

using Settex.Core.Merging;

public sealed class MergerTests
{
    [Test]
    public async Task Merge_EmptyObjects_ReturnsEmptyObject()
    {
        // Arrange
        var merger = new Merger();
        var baseObj = new JsonObject();
        var overlay = new JsonObject();

        // Act
        var result = merger.Merge(baseObj, overlay);

        // Assert
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Merge_BaseOnly_ReturnsBaseClone()
    {
        // Arrange
        var merger = new Merger();
        var baseObj = new JsonObject
        {
            ["Name"] = "Test",
        };

        var overlay = new JsonObject();

        // Act
        var result = merger.Merge(baseObj, overlay);

        // Assert
        await Assert.That(result["Name"]?.GetValue<string>()).IsEqualTo("Test");
    }

    [Test]
    public async Task Merge_OverlayOnly_ReturnsOverlayClone()
    {
        // Arrange
        var merger = new Merger();
        var baseObj = new JsonObject();
        var overlay = new JsonObject
        {
            ["Name"] = "Test",
        };

        // Act
        var result = merger.Merge(baseObj, overlay);

        // Assert
        await Assert.That(result["Name"]?.GetValue<string>()).IsEqualTo("Test");
    }

    [Test]
    public async Task Merge_PrimitiveReplacement_OverlayWins()
    {
        // Arrange
        var merger = new Merger();
        var baseObj = new JsonObject
        {
            ["Name"] = "Base",
        };

        var overlay = new JsonObject
        {
            ["Name"] = "Overlay",
        };

        // Act
        var result = merger.Merge(baseObj, overlay);

        // Assert
        await Assert.That(result["Name"]?.GetValue<string>()).IsEqualTo("Overlay");
    }

    [Test]
    public async Task Merge_DeepObjectMerge_MergesRecursively()
    {
        // Arrange
        var merger = new Merger();
        var baseObj = new JsonObject
        {
            ["Logging"] = new JsonObject
            {
                ["LogLevel"] = new JsonObject
                {
                    ["Default"] = "Information",
                },
            },
        };

        var overlay = new JsonObject
        {
            ["Logging"] = new JsonObject
            {
                ["LogLevel"] = new JsonObject
                {
                    ["System"] = "Warning",
                },
            },
        };

        // Act
        var result = merger.Merge(baseObj, overlay);

        // Assert
        var logging = result["Logging"] as JsonObject;
        await Assert.That(logging).IsNotNull();
        var logLevel = logging!["LogLevel"] as JsonObject;
        await Assert.That(logLevel).IsNotNull();
        await Assert.That(logLevel!["Default"]?.GetValue<string>()).IsEqualTo("Information");
        await Assert.That(logLevel["System"]?.GetValue<string>()).IsEqualTo("Warning");
    }

    [Test]
    public async Task Merge_ArrayReplacement_OverlayReplacesEntireArray()
    {
        // Arrange
        var merger = new Merger();
        var baseObj = new JsonObject
        {
            ["Items"] = new JsonArray(1, 2, 3),
        };

        var overlay = new JsonObject
        {
            ["Items"] = new JsonArray(4, 5),
        };

        // Act
        var result = merger.Merge(baseObj, overlay);

        // Assert
        var items = result["Items"] as JsonArray;
        await Assert.That(items).IsNotNull();
        await Assert.That(items!.Count).IsEqualTo(2);
        await Assert.That(items[0]?.GetValue<int>()).IsEqualTo(4);
        await Assert.That(items[1]?.GetValue<int>()).IsEqualTo(5);
    }

    [Test]
    public async Task Merge_NullReplacement_OverlayWins()
    {
        // Arrange
        var merger = new Merger();
        var baseObj = new JsonObject
        {
            ["Value"] = "Something",
        };

        var overlay = new JsonObject
        {
            ["Value"] = null,
        };

        // Act
        var result = merger.Merge(baseObj, overlay);

        // Assert
        await Assert.That(result["Value"]).IsNull();
    }

    [Test]
    public async Task Merge_TypeMismatch_ObjectToArray_ThrowsException()
    {
        // Arrange
        var merger = new Merger();
        var baseObj = new JsonObject
        {
            ["Data"] = new JsonObject { ["Key"] = "Value" },
        };

        var overlay = new JsonObject
        {
            ["Data"] = new JsonArray(1, 2, 3),
        };

        // Act & Assert
        await Assert.ThrowsAsync<MergerException>(async () =>
        {
            merger.Merge(baseObj, overlay);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task Merge_TypeMismatch_ArrayToObject_ThrowsException()
    {
        // Arrange
        var merger = new Merger();
        var baseObj = new JsonObject
        {
            ["Data"] = new JsonArray(1, 2, 3),
        };

        var overlay = new JsonObject
        {
            ["Data"] = new JsonObject { ["Key"] = "Value" },
        };

        // Act & Assert
        await Assert.ThrowsAsync<MergerException>(async () =>
        {
            merger.Merge(baseObj, overlay);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task Merge_TypeMismatch_PrimitiveToObject_ThrowsException()
    {
        // Arrange
        var merger = new Merger();
        var baseObj = new JsonObject
        {
            ["Value"] = "String",
        };

        var overlay = new JsonObject
        {
            ["Value"] = new JsonObject { ["Key"] = "Value" },
        };

        // Act & Assert
        await Assert.ThrowsAsync<MergerException>(async () =>
        {
            merger.Merge(baseObj, overlay);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task Merge_ComplexMerge_PreservesStructure()
    {
        // Arrange
        var merger = new Merger();
        var baseObj = new JsonObject
        {
            ["ConnectionStrings"] = new JsonObject
            {
                ["DefaultConnection"] = "Server=localhost",
            },
            ["Logging"] = new JsonObject
            {
                ["LogLevel"] = new JsonObject
                {
                    ["Default"] = "Information",
                },
            },
        };

        var overlay = new JsonObject
        {
            ["ConnectionStrings"] = new JsonObject
            {
                ["DefaultConnection"] = "Server=production",
            },
            ["Logging"] = new JsonObject
            {
                ["LogLevel"] = new JsonObject
                {
                    ["System"] = "Warning",
                },
            },
        };

        // Act
        var result = merger.Merge(baseObj, overlay);

        // Assert
        var connStrings = result["ConnectionStrings"] as JsonObject;
        await Assert.That(connStrings).IsNotNull();
        await Assert.That(connStrings!["DefaultConnection"]?.GetValue<string>())
                    .IsEqualTo("Server=production");

        var logging = result["Logging"] as JsonObject;
        await Assert.That(logging).IsNotNull();
        var logLevel = logging!["LogLevel"] as JsonObject;
        await Assert.That(logLevel).IsNotNull();
        await Assert.That(logLevel!["Default"]?.GetValue<string>()).IsEqualTo("Information");
        await Assert.That(logLevel["System"]?.GetValue<string>()).IsEqualTo("Warning");
    }

    [Test]
    public async Task Merge_NumberTypes_MergesCorrectly()
    {
        // Arrange
        var merger = new Merger();
        var baseObj = new JsonObject
        {
            ["Port"] = 8080,
            ["Timeout"] = 30.5,
        };

        var overlay = new JsonObject
        {
            ["Port"] = 9090,
            ["MaxConnections"] = 100,
        };

        // Act
        var result = merger.Merge(baseObj, overlay);

        // Assert
        await Assert.That(result["Port"]?.GetValue<int>()).IsEqualTo(9090);
        await Assert.That(result["Timeout"]?.GetValue<double>()).IsEqualTo(30.5);
        await Assert.That(result["MaxConnections"]?.GetValue<int>()).IsEqualTo(100);
    }

    [Test]
    public async Task Merge_BooleanValues_MergesCorrectly()
    {
        // Arrange
        var merger = new Merger();
        var baseObj = new JsonObject
        {
            ["Enabled"] = true,
            ["Debug"] = false,
        };

        var overlay = new JsonObject
        {
            ["Enabled"] = false,
        };

        // Act
        var result = merger.Merge(baseObj, overlay);

        // Assert
        await Assert.That(result["Enabled"]?.GetValue<bool>()).IsEqualTo(false);
        await Assert.That(result["Debug"]?.GetValue<bool>()).IsEqualTo(false);
    }

    [Test]
    public async Task Merge_NestedArrays_ReplacesCompletely()
    {
        // Arrange
        var merger = new Merger();
        var baseObj = new JsonObject
        {
            ["Config"] = new JsonObject
            {
                ["AllowedHosts"] = new JsonArray("localhost", "127.0.0.1"),
            },
        };

        var overlay = new JsonObject
        {
            ["Config"] = new JsonObject
            {
                ["AllowedHosts"] = new JsonArray("*"),
            },
        };

        // Act
        var result = merger.Merge(baseObj, overlay);

        // Assert
        var config = result["Config"] as JsonObject;
        await Assert.That(config).IsNotNull();
        var hosts = config!["AllowedHosts"] as JsonArray;
        await Assert.That(hosts).IsNotNull();
        await Assert.That(hosts!.Count).IsEqualTo(1);
        await Assert.That(hosts[0]?.GetValue<string>()).IsEqualTo("*");
    }
}
