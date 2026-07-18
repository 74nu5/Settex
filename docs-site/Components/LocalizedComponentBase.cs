using Microsoft.AspNetCore.Components;

using Settex.Docs.Services;

namespace Settex.Docs.Components;

/// <summary>
/// Base component that re-renders whenever the UI language changes.
/// Pages inherit from this and branch their content on <see cref="Fr"/>.
/// </summary>
public abstract class LocalizedComponentBase : ComponentBase, IDisposable
{
    [Inject] protected LocalizationState Loc { get; set; } = default!;

    /// <summary>True when the current language is French.</summary>
    protected bool Fr => this.Loc.IsFrench;

    protected override void OnInitialized() => this.Loc.OnChange += this.StateHasChanged;

    public void Dispose() => this.Loc.OnChange -= this.StateHasChanged;
}
