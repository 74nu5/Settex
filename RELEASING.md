# Releasing

Every command below was run and verified on this repository. What is **not** here is
the publishing step itself for each target — those take credentials, and they are
irreversible: a NuGet version can be unlisted but never replaced, and a marketplace
extension version can never be re-uploaded.

## 1. Set the version

One place drives all three packages:

```xml
<!-- Directory.Build.props -->
<VersionPrefix>0.1.0</VersionPrefix>
<VersionSuffix>alpha</VersionSuffix>   <!-- delete this line for a stable release -->
```

The VS Code extension version lives in `extensions/vscode-settex/package.json` and is
kept in step by hand.

## 2. Verify

```bash
dotnet build Settex.slnx -c Release      # must be 0 warnings; TreatWarningsAsErrors is on
dotnet test Settex.slnx -c Release       # note: --nologo breaks the test runner
```

## 3. Build the packages

```bash
dotnet pack Settex.slnx -c Release -o ./artifacts
```

Produces `Settex.Core`, `Settex.Cli` and `Settex.Build`. `Settex.LanguageServer` is
deliberately not packable — it ships inside the editor extensions, not as a library.

### Verify the packages actually work before publishing

Packing successfully proves nothing: the MSBuild package can pack cleanly and still
fail to do anything in a consumer project, which is exactly what happened here. Test
against a local feed.

`nuget.config` in a throwaway project — the `packageSourceMapping` section is required
on machines where mapping is enabled, otherwise the local feed is silently ignored:

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="/absolute/path/to/artifacts" />
    <add key="nuget" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="local"><package pattern="Settex.*" /></packageSource>
    <packageSource key="nuget"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
```

Then, with an `appsettings.settex` in the project and a `PackageReference` to
`Settex.Build`, `dotnet build` must produce `appsettings.json` and one file per
environment. And the tool:

```bash
dotnet tool install Settex.Cli --version <version> --tool-path ./bin
./bin/settex build t.settex -o out
./bin/settex import out/appsettings.json -o round.settex   # must say "verified exact"
```

## 4. Publish to NuGet

Needs an API key from <https://www.nuget.org/account/apikeys> scoped to these package
IDs. **Push `Settex.Core` first** — the other two depend on it, and a package whose
dependency is not yet indexed fails to restore for anyone who is quick.

```bash
dotnet nuget push ./artifacts/Settex.Core.<version>.nupkg    -k <KEY> -s https://api.nuget.org/v3/index.json
dotnet nuget push ./artifacts/Settex.Build.<version>.nupkg   -k <KEY> -s https://api.nuget.org/v3/index.json
dotnet nuget push ./artifacts/Settex.Cli.<version>.nupkg     -k <KEY> -s https://api.nuget.org/v3/index.json
```

Indexing takes a few minutes. Verify with `dotnet tool install --global Settex.Cli`
from a machine that never saw the local feed.

## 5. Publish the VS Code extension

```bash
cd extensions/vscode-settex
npm ci
npx vsce package            # verified: produces settex-<version>.vsix, ~2.5 MB
npx vsce publish -p <PAT>
```

The publisher id in `package.json` is `settex`; the marketplace publisher must exist
and the PAT must belong to the Azure DevOps organisation that owns it. `vsce package`
bundles the language server via the `bundle-server` script, so the VSIX is
self-contained apart from the .NET runtime, which the extension acquires at first use.

## 6. Publish the Visual Studio extension

`extensions/vs-settex` targets `net472` and needs the Visual Studio SDK workload to
build, so it cannot be produced from a plain `dotnet` SDK install. Build the VSIX from
Visual Studio, then upload it at
<https://marketplace.visualstudio.com/manage/publishers>.

Before that upload, decide the `<Prerequisites>` entry. The runtime component id is
`Microsoft.NetCore.Component.Runtime.10.0`. It is deliberately **not** declared today:
a wrong version range blocks installation for everyone on an older VS, and that cannot
be validated without a real VS instance to install into.

## After publishing

- Tag the commit: `git tag v<version> && git push origin v<version>`
- The docs site deploys itself from `main` on any change under `docs-site/`.
