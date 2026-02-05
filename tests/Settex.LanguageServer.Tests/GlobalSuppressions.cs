// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.

using System.Diagnostics.CodeAnalysis;

// Suppress VSTHRD200 for test methods - they return Task but don't need Async suffix
[assembly: SuppressMessage("Usage", "VSTHRD200:Use Async suffix for async methods", Justification = "Test methods with TUnit don't require Async suffix", Scope = "namespaceanddescendants", Target = "~N:Settex.LanguageServer.Tests")]
