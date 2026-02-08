namespace Settex.VisualStudio;

using System.ComponentModel;

using Microsoft.VisualStudio.Shell;

/// <summary>
/// Options page for Settex extension settings.
/// </summary>
public class SettexOptionsPage : DialogPage
{
    /// <summary>
    /// Gets or sets a value indicating whether to automatically compile .settex files on save.
    /// </summary>
    [Category("Build")]
    [DisplayName("Compile on Save")]
    [Description("Automatically compile .settex files when they are saved.")]
    public bool CompileOnSave { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to show success notifications.
    /// </summary>
    [Category("Notifications")]
    [DisplayName("Show Success Notifications")]
    [Description("Show a notification when compilation succeeds.")]
    public bool ShowSuccessNotifications { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to show error notifications.
    /// </summary>
    [Category("Notifications")]
    [DisplayName("Show Error Notifications")]
    [Description("Show a notification when compilation fails.")]
    public bool ShowErrorNotifications { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to log compilation output to the Output window.
    /// </summary>
    [Category("Output")]
    [DisplayName("Log to Output Window")]
    [Description("Log compilation messages to the Visual Studio Output window.")]
    public bool LogToOutputWindow { get; set; } = true;
}
