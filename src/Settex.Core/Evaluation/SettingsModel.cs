namespace Settex.Core.Evaluation;

using System.Text.Json.Nodes;

/// <summary>
///     Represents the evaluated settings model.
///     Contains base settings and environment-specific overlays.
/// </summary>
public sealed class SettingsModel(JsonObject baseSettings, Dictionary<string, JsonObject> environmentOverlays)
{
    /// <summary>
    ///     Base settings from the main settings block.
    /// </summary>
    public JsonObject BaseSettings { get; } = baseSettings;

    /// <summary>
    ///     Environment-specific overlays.
    ///     Key: Environment name, Value: Settings overlay for that environment.
    /// </summary>
    public Dictionary<string, JsonObject> EnvironmentOverlays { get; } = environmentOverlays;
}
