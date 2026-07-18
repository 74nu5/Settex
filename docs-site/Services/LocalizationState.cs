using Microsoft.JSInterop;

namespace Settex.Docs.Services;

/// <summary>
/// Holds the current UI language ("en" or "fr"), persists the choice to
/// localStorage, and notifies subscribers when it changes.
/// </summary>
public class LocalizationState
{
    private const string StorageKey = "settex-lang";
    private readonly IJSRuntime js;

    public LocalizationState(IJSRuntime js) => this.js = js;

    /// <summary>Current language code: "en" or "fr".</summary>
    public string Language { get; private set; } = "en";

    public bool IsFrench => this.Language == "fr";

    /// <summary>Raised after the language changes so components can re-render.</summary>
    public event Action? OnChange;

    /// <summary>Reads the persisted language from localStorage (call once at startup).</summary>
    public async Task InitializeAsync()
    {
        try
        {
            var stored = await this.js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (stored is "fr" or "en" && stored != this.Language)
            {
                this.Language = stored;
                this.OnChange?.Invoke();
            }
        }
        catch
        {
            // localStorage may be unavailable (e.g. prerendering) — keep the default.
        }
    }

    /// <summary>Sets the language, persists it, and notifies subscribers.</summary>
    public async Task SetLanguageAsync(string lang)
    {
        if (lang is not ("fr" or "en") || lang == this.Language)
        {
            return;
        }

        this.Language = lang;

        try
        {
            await this.js.InvokeVoidAsync("localStorage.setItem", StorageKey, lang);
        }
        catch
        {
            // Ignore persistence failures; the in-memory choice still applies.
        }

        this.OnChange?.Invoke();
    }
}
