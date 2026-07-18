# Settex documentation site

A static documentation website for Settex, built with **Blazor WebAssembly** and hosted on
**Azure Static Web Apps**.

## Run locally

```bash
cd docs-site
dotnet run
```

Then open the URL printed by the dev server (typically `https://localhost:5001`).

## Build / publish

```bash
dotnet publish docs-site/Settex.Docs.csproj -c Release -o publish
```

The deployable static files are written to `publish/wwwroot`.

## Deploy to Azure Static Web Apps

Deployment is automated by [`.github/workflows/azure-static-web-apps.yml`](../.github/workflows/azure-static-web-apps.yml),
which builds the site and uploads `publish/wwwroot` on every push to `main` that touches `docs-site/`.

One-time setup:

1. Create an **Azure Static Web Apps** resource (Free plan is enough). When asked for a deployment
   source, choose **Other** so Azure does not generate its own workflow.
2. Copy the resource's **deployment token** and add it as a repository secret named
   `AZURE_STATIC_WEB_APPS_API_TOKEN` (Settings → Secrets and variables → Actions).

The app is pre-built in CI (rather than by Azure's Oryx builder) because it targets a preview .NET
SDK. Client-side routing is handled by [`wwwroot/staticwebapp.config.json`](wwwroot/staticwebapp.config.json),
which falls back to `index.html` for SPA routes.

## Structure

- `Pages/` — one `.razor` page per route (Overview, Getting started, Language guide, CLI, Extensions, Cheat sheet).
- `Layout/` — the shell (top bar + sidebar navigation).
- `Components/CodeBlock.razor` — renders code samples (content is HTML-encoded automatically).
- `wwwroot/css/app.css` — the theme (light/dark aware).

This project is intentionally **excluded from the main solution** (`Settex.slnx`) and from the
repository's Central Package Management / strict build settings — it has its own `Directory.Build.props`
and `Directory.Packages.props`.
