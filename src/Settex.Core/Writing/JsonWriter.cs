namespace Settex.Core.Writing;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using Settex.Core.Evaluation;
using Settex.Core.Merging;

/// <summary>
///     Writes SettingsModel to appsettings*.json files.
/// </summary>
public sealed class JsonWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    private readonly Merger merger = new();

    /// <summary>
    ///     Writes all appsettings files to the output directory.
    /// </summary>
    /// <param name="model">Settings model to write.</param>
    /// <param name="outputDirectory">Directory where files will be written.</param>
    /// <param name="mergeEnvironments">
    ///     When <c>true</c>, each environment file contains the full merged config
    ///     (base + overlay). When <c>false</c> (the default), it contains only the
    ///     environment's overrides, layered on top of <c>appsettings.json</c> by .NET
    ///     configuration at runtime.
    /// </param>
    /// <returns>List of generated file paths.</returns>
    /// <exception cref="JsonWriterException">Thrown when writing fails.</exception>
    public List<string> WriteSettings(SettingsModel model, string outputDirectory, bool mergeEnvironments = false)
    {
        var generatedFiles = new List<string>();

        // Ensure output directory exists
        try
        {
            Directory.CreateDirectory(outputDirectory);
        }
        catch (Exception ex)
        {
            throw new JsonWriterException(
                $"Failed to create output directory: {outputDirectory}",
                ex);
        }

        // Write base appsettings.json
        var basePath = WriteJsonFile(model.BaseSettings, outputDirectory, "appsettings.json");
        generatedFiles.Add(basePath);

        // Write environment-specific files
        foreach (var (envName, overlay) in model.EnvironmentOverlays)
        {
            ValidateEnvironmentName(envName);

            // Delta (default): write only the environment's overrides, which .NET
            // layers on top of appsettings.json. Merged (opt-in): the full config.
            var content = mergeEnvironments
                ? this.merger.Merge(model.BaseSettings, overlay)
                : overlay;

            var fileName = $"appsettings.{envName}.json";
            var path = WriteJsonFile(content, outputDirectory, fileName);
            generatedFiles.Add(path);
        }

        return generatedFiles;
    }

    /// <summary>
    ///     Validates that an environment name is safe for file system use.
    /// </summary>
    private static void ValidateEnvironmentName(string envName)
    {
        if (string.IsNullOrWhiteSpace(envName))
        {
            throw new JsonWriterException("Environment name cannot be empty or whitespace");
        }

        foreach (var c in envName)
        {
            if (Array.IndexOf(InvalidFileNameChars, c) >= 0)
            {
                throw new JsonWriterException(
                    $"Environment name '{envName}' contains invalid character '{c}'");
            }
        }
    }

    /// <summary>
    ///     Writes a JSON object to a file, only if content differs.
    /// </summary>
    /// <returns>The full path to the written file.</returns>
    private static string WriteJsonFile(JsonObject jsonObject, string directory, string fileName)
    {
        var filePath = Path.Combine(directory, fileName);
        var jsonContent = SerializeJson(jsonObject);

        // Check if file exists and has same content (conditional write)
        if (File.Exists(filePath))
        {
            try
            {
                var existingContent = File.ReadAllText(filePath, Encoding.UTF8);

                if (existingContent == jsonContent)
                {
                    // Content unchanged - skip write
                    return filePath;
                }
            }
            catch
            {
                // If we can't read existing file, proceed with write
            }
        }

        // Write the file
        try
        {
            File.WriteAllText(filePath, jsonContent, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            throw new JsonWriterException(
                $"Failed to write file: {filePath}",
                ex);
        }

        return filePath;
    }

    /// <summary>
    ///     Serializes JsonObject to JSON string with deterministic formatting.
    /// </summary>
    private static string SerializeJson(JsonObject jsonObject)
    {
        // System.Text.Json preserves insertion order by default
        var json = jsonObject.ToJsonString(SerializerOptions);

        // Ensure consistent line endings (LF)
        json = json.Replace("\r\n", "\n");

        // Add trailing newline
        if (!json.EndsWith("\n"))
        {
            json += "\n";
        }

        return json;
    }
}
