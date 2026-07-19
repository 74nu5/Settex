using Microsoft.JSInterop;

namespace Settex.Docs.Services;

/// <summary>
/// Holds the current UI language ("en" or "fr"), persists the choice to
/// localStorage, keeps <c>&lt;html lang&gt;</c> in sync, and notifies subscribers
/// when it changes.
/// </summary>
public class LocalizationState
{
    private const string StorageKey = "settex-lang";
    private readonly IJSRuntime js;
    private bool initialized;

    public LocalizationState(IJSRuntime js) => this.js = js;

    /// <summary>Current language code: "en" or "fr".</summary>
    public string Language { get; private set; } = "en";

    public bool IsFrench => this.Language == "fr";

    /// <summary>Raised after the language changes so components can re-render.</summary>
    public event Action? OnChange;

    /// <summary>
    /// Reads the persisted language <strong>synchronously</strong> before the first
    /// render, so the initial paint already uses the stored language instead of
    /// flashing English first. Uses in-process JS interop, which is available in
    /// Blazor WebAssembly; if it isn't (e.g. prerendering), the default is kept and
    /// <see cref="InitializeAsync"/> can reconcile later.
    /// </summary>
    public void Initialize()
    {
        if (this.initialized)
        {
            return;
        }

        // Only the in-process path is authoritative here; when it is unavailable we
        // leave initialization to InitializeAsync so the language is still read.
        if (this.js is not IJSInProcessRuntime sync)
        {
            return;
        }

        this.initialized = true;

        try
        {
            var stored = sync.Invoke<string?>("localStorage.getItem", StorageKey);
            if (stored is "fr" or "en")
            {
                this.Language = stored;
            }
        }
        catch
        {
            // localStorage unavailable — keep the default.
        }

        this.ApplyHtmlLang();
    }

    /// <summary>
    /// Async fallback for environments without in-process interop. Reconciles the
    /// language from localStorage and notifies subscribers if it changed.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (this.initialized)
        {
            return;
        }

        this.initialized = true;

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
            // localStorage may be unavailable — keep the default.
        }

        await this.ApplyHtmlLangAsync();
    }

    /// <summary>Sets the language, persists it, updates &lt;html lang&gt;, and notifies subscribers.</summary>
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

        await this.ApplyHtmlLangAsync();
        this.OnChange?.Invoke();
    }

    private void ApplyHtmlLang()
    {
        if (this.js is IJSInProcessRuntime sync)
        {
            try
            {
                sync.InvokeVoid("settexDocs.setHtmlLang", this.Language);
            }
            catch
            {
                // Best-effort; the visible content is already localized.
            }
        }
    }

    private async Task ApplyHtmlLangAsync()
    {
        try
        {
            await this.js.InvokeVoidAsync("settexDocs.setHtmlLang", this.Language);
        }
        catch
        {
            // Best-effort.
        }
    }
}
