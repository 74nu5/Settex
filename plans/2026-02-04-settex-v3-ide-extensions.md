# Settex V3 - Plan d'implémentation : Extensions IDE

## Visual Studio 2022+ et VS Code

**Date de création** : 2026-02-04  
**Statut** : 📋 Planification  
**Version** : 3.0  
**Prérequis** : Settex V2 complet ✅

---

## 0. Vue d'ensemble

### Objectif V3

Créer une expérience développeur de première classe pour Settex avec :
- **Coloration syntaxique** complète et précise
- **IntelliSense** : Autocomplétion, aide contextuelle, signatures
- **Diagnostics** : Erreurs, avertissements, conseils en temps réel
- **Navigation** : Go to definition, Find all references
- **Refactoring** : Renommer variables, extraire includes
- **Snippets** : Modèles de code courants
- **Support multi-IDE** : VS 2022+, VS Code (et potentiellement JetBrains via LSP)

### Architecture recommandée : Language Server Protocol (LSP)

```
┌─────────────────────────────────────────────────────────────────┐
│                    Settex Language Server                       │
│                    (.NET / Settex.LanguageServer)               │
│                                                                 │
│  ┌─────────┐  ┌──────────┐  ┌──────────┐  ┌─────────────────┐   │
│  │ Lexer   │→ │ Parser   │→ │Evaluator │→ │ Semantic Model  │   │
│  │ (V2)    │  │ (V2)     │  │ (V2)     │  │ (Symbols, etc.) │   │
│  └─────────┘  └──────────┘  └──────────┘  └─────────────────┘   │
│                           │                                     │
│  ┌────────────────────────┴──────────────────────────────────┐  │
│  │              LSP Protocol Handler (JSON-RPC 2.0)          │  │
│  │  - textDocument/completion                                │  │
│  │  - textDocument/hover                                     │  │
│  │  - textDocument/definition                                │  │
│  │  - textDocument/publishDiagnostics                        │  │
│  │  - textDocument/formatting                                │  │
│  │  - workspace/symbol                                       │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
         │ Standard I/O / Named Pipes / TCP
         ▼
┌────────────────────┐     ┌────────────────────┐
│   VS Code Client   │     │ Visual Studio 2022+│
│   (TypeScript)     │     │   (C# VSIX)        │
│                    │     │                    │
│ - TextMate Grammar │     │ - TextMate Grammar │
│ - Language Config  │     │ - Language Config  │
│ - LSP Client       │     │ - LSP Client       │
│ - Snippets         │     │ - Snippets         │
└────────────────────┘     └────────────────────┘
```

### Avantages de l'architecture LSP

| Aspect | Avantage |
|--------|----------|
| Code partagé | Un seul serveur de langage pour tous les IDE |
| Maintenance | Corrections et features bénéficient à tous les IDE |
| Réutilisation | Lexer/Parser/Evaluator V2 directement utilisables |
| Extensibilité | Nouveaux IDE supportables via clients LSP |
| Performance | Analyse lourde isolée du thread UI |

---

## 1. Structure des projets

```
Settex/
├── src/
│   ├── Settex.Core/              # V2 existant
│   ├── Settex.Build/             # MSBuild task existant
│   ├── Settex.Cli/               # CLI existant
│   └── Settex.LanguageServer/    # 🆕 Serveur LSP (.NET)
│
├── extensions/
│   ├── vscode-settex/            # 🆕 Extension VS Code
│   │   ├── package.json          # Manifest extension
│   │   ├── src/                  # TypeScript client
│   │   ├── syntaxes/             # TextMate grammar
│   │   ├── snippets/             # Snippets JSON
│   │   └── language-configuration.json
│   │
│   └── vs-settex/                # 🆕 Extension Visual Studio
│       ├── source.extension.vsixmanifest
│       ├── SettexLanguageClient.cs
│       ├── Grammars/             # TextMate grammar
│       └── Snippets/             # Snippets
│
├── shared/
│   └── textmate/                 # 🆕 Grammaire partagée
│       ├── settex.tmLanguage.json
│       └── settex.tmTheme.json
│
└── tests/
    └── Settex.LanguageServer.Tests/
```

---

## 2. Phases d'implémentation

### Phase 1 : TextMate Grammar (Coloration syntaxique) 🎨 - ✅ COMPLÉTÉE

**Objectif** : Coloration syntaxique immédiate dans les deux IDE.

**Durée réelle** : ~30 minutes

#### Tâches
- [x] Créer `shared/textmate/settex.tmLanguage.json` :
  - [x] Scope `source.settex`
  - [x] Keywords : `settings`, `env`, `include`, `let`, `for`, `in`, `if`, `and`, `or`, `not`, `true`, `false`, `null`
  - [x] Operators : `=`, `:=`, `+`, `-`, `*`, `/`, `==`, `!=`, `<`, `<=`, `>`, `>=`, `??`
  - [x] Strings : Double quotes avec interpolation `${...}`
  - [x] Numbers : Entiers et décimaux
  - [x] Comments : `#` et `//`
  - [x] Identifiers : Variables, noms de clés
  - [x] Blocks : `{ }`, `[ ]`
- [x] Définir les scopes standard :
  - [x] `keyword.control.settex` (for, if, in)
  - [x] `keyword.other.settex` (settings, env, include, let)
  - [x] `constant.language.settex` (true, false, null)
  - [x] `constant.numeric.settex` (42, 3.14)
  - [x] `string.quoted.double.settex`
  - [x] `string.interpolated.settex` (contenu de ${})
  - [x] `comment.line.settex`
  - [x] `variable.other.settex`
  - [x] `entity.name.tag.settex` (env name)
  - [x] `punctuation.definition.block.settex`
- [x] Créer `language-configuration.json` :
  - [x] Bracket matching : `{}`, `[]`, `()`
  - [x] Auto-closing brackets
  - [x] Comment toggling : `#`, `//`
  - [x] Indentation rules

**Critères de succès** :
- ✅ Coloration correcte de tous les exemples V2
- ✅ Bracket matching fonctionnel
- ✅ Auto-fermeture des accolades/brackets
- ✅ Toggle commentaire

---

### Phase 2 : Extension VS Code basique 📦 - ✅ COMPLÉTÉE

**Objectif** : Extension VS Code publiable avec coloration + snippets.

**Durée réelle** : ~40 minutes

#### Tâches
- [x] Scaffolder extension manuellement (structure complète créée)
- [x] Configurer `package.json` :
  - [x] `name`: `settex`
  - [x] `displayName`: `Settex - Configuration Language`
  - [x] `description`: Syntax highlighting, IntelliSense, diagnostics for Settex
  - [x] `categories`: `["Programming Languages", "Snippets"]`
  - [x] `activationEvents`: `["onLanguage:settex"]`
  - [x] File associations : `*.settex`
  - [x] Language configuration
  - [x] Grammar contribution
- [x] Intégrer TextMate grammar de Phase 1
- [x] Créer 12 snippets complets :
  - [x] settings, env, let, for, include
  - [x] if, setif (:=), block, tag, array
  - [x] interp (interpolation)
  - [x] !settex (template complet)
- [x] Créer README et CHANGELOG marketplace
- [x] Créer icônes SVG (extension + file type)
- [x] Configurer TypeScript (tsconfig.json)
- [x] Créer extension.ts (stub activation)
- [x] Configurer VS Code debug (.vscode/launch.json, tasks.json)

**Critères de succès** :
- ✅ Structure complète prête à tester
- ✅ Coloration syntaxique prête
- ✅ 12 snippets fonctionnels
- ✅ Metadata et documentation

**Note** : Prêt à tester avec `npm install && npm run compile` puis F5 !

---

### Phase 3 : Settex Language Server (Core) 🖥️

**Objectif** : Créer le serveur de langage .NET implémentant LSP.

**Durée estimée** : 5-7 jours

#### Sous-phase 3.1 : Infrastructure LSP
- [ ] Créer projet `Settex.LanguageServer` (.NET 8+)
- [ ] Dépendances NuGet :
  - [ ] `Microsoft.VisualStudio.LanguageServer.Protocol`
  - [ ] `Nerdbank.Streams` ou `StreamJsonRpc`
- [ ] Implémenter architecture de base :
  ```csharp
  public class SettexLanguageServer
  {
      private JsonRpc rpc;
      private SettexWorkspace workspace;
      
      // LSP Lifecycle
      Task<InitializeResult> Initialize(InitializeParams @params);
      Task Initialized(InitializedParams @params);
      Task Shutdown();
      Task Exit();
  }
  ```
- [ ] Gérer la communication stdio / named pipes
- [ ] Logging et tracing pour debug

#### Sous-phase 3.2 : Document Management
- [ ] `SettexWorkspace` : Gestion des documents ouverts
  ```csharp
  public class SettexWorkspace
  {
      Dictionary<DocumentUri, SettexDocument> documents;
      
      void DidOpen(TextDocumentItem item);
      void DidChange(VersionedTextDocumentIdentifier doc, TextDocumentContentChangeEvent[] changes);
      void DidClose(TextDocumentIdentifier doc);
  }
  ```
- [ ] `SettexDocument` : Analyse incrémentale
  ```csharp
  public class SettexDocument
  {
      string Text { get; }
      List<Token> Tokens { get; }        // Lexer output
      FileNode Ast { get; }               // Parser output
      List<Diagnostic> Diagnostics { get; }
      
      void Update(string newText);
      void Reparse();
  }
  ```
- [ ] Adapter le Lexer/Parser V2 pour l'analyse incrémentale
- [ ] Gestion des erreurs non-fatales (parser recovery)

#### Sous-phase 3.3 : Diagnostics (Errors/Warnings)
- [ ] Implémenter `textDocument/publishDiagnostics` :
  - [ ] Erreurs lexer (caractères invalides)
  - [ ] Erreurs parser (syntaxe incorrecte)
  - [ ] Warnings sémantiques :
    - [ ] Variable non utilisée
    - [ ] Variable non définie (avant évaluation)
    - [ ] Include non trouvé
    - [ ] Cycle d'includes détecté
- [ ] Mapper `SourceLocation` vers LSP `Range`
- [ ] Codes d'erreur Settex (STX001-STX302)

**Critères de succès** :
- ✅ Serveur démarre et répond à Initialize
- ✅ Diagnostics envoyés à l'ouverture de fichier
- ✅ Diagnostics mis à jour à chaque modification
- ✅ Erreurs correctement positionnées (ligne/colonne)

---

### Phase 4 : IntelliSense (Completion) 💡

**Objectif** : Autocomplétion contextuelle.

**Durée estimée** : 4-5 jours

#### Tâches
- [ ] Implémenter `textDocument/completion` :
  - [ ] Keywords selon contexte :
    - [ ] Top-level : `settings`, `env`, `include`, `let`
    - [ ] Dans array : `for`, values
    - [ ] Dans expression : `and`, `or`, `not`, `if`, variables
  - [ ] Variables en scope (global, env, for iterator)
  - [ ] Noms d'environnement existants (pour `env`)
  - [ ] Fichiers pour `include` (complétion de chemin)
- [ ] `CompletionItem` avec :
  - [ ] `label` : Texte affiché
  - [ ] `kind` : Keyword, Variable, File, Snippet
  - [ ] `detail` : Type ou description
  - [ ] `insertText` : Texte inséré (avec snippets)
  - [ ] `documentation` : Documentation markdown
- [ ] Trigger characters : `.`, `"`, `$`, `{`
- [ ] Implémenter `completionItem/resolve` pour lazy loading

**Exemples de completion** :
```
let ba|  →  [basePort, baseHost, ...]
settings { Lo| → [Logging, ...]
include "|  →  [common.settex, logging.settex, ...]
${|  →  [host, port, baseUrl, ...]
```

**Critères de succès** :
- ✅ Completion de mots-clés contextuelle
- ✅ Completion de variables en scope
- ✅ Completion de chemins pour include
- ✅ Documentation visible

---

### Phase 5 : Hover & Signature Help 📖

**Objectif** : Informations contextuelles au survol.

**Durée estimée** : 2-3 jours

#### Tâches Hover
- [ ] Implémenter `textDocument/hover` :
  - [ ] Variables : Afficher valeur et scope
    ```
    let basePort = 8000
    ---
    Variable (global scope)
    Value: 8000
    ```
  - [ ] Keywords : Documentation rapide
    ```
    settings
    ---
    Defines the base configuration block.
    All environments inherit from this block.
    ```
  - [ ] Operators : Explication
    ```
    :=
    ---
    Set-if-missing operator.
    Sets the value only if the path doesn't exist.
    ```
  - [ ] Environnements : Statistiques
    ```
    env "Development"
    ---
    Environment overlay
    Overrides: 5 settings
    Variables: 2 (devPort, devHost)
    ```

#### Tâches Signature Help
- [ ] Implémenter `textDocument/signatureHelp` :
  - [ ] Pour `include` : Chemin relatif attendu
  - [ ] Pour `for` : `for iterator in collection { body }`
  - [ ] Pour expressions : Opérateurs disponibles

**Critères de succès** :
- ✅ Hover informatif sur tous les éléments
- ✅ Documentation markdown rendue correctement
- ✅ Signature help contextuelle

---

### Phase 6 : Navigation (Go to Definition, References) 🔍

**Objectif** : Navigation rapide dans le code.

**Durée estimée** : 3-4 jours

#### Tâches
- [ ] Construire table de symboles :
  ```csharp
  public class SymbolTable
  {
      Dictionary<string, VariableSymbol> Variables;
      Dictionary<string, EnvironmentSymbol> Environments;
      List<IncludeReference> Includes;
      
      SymbolInfo? FindSymbolAt(Position pos);
      List<Location> FindReferences(string name);
  }
  ```
- [ ] Implémenter `textDocument/definition` :
  - [ ] Variable → déclaration `let`
  - [ ] Include → fichier cible
  - [ ] Environnement → bloc `env`
- [ ] Implémenter `textDocument/references` :
  - [ ] Toutes les utilisations d'une variable
  - [ ] Tous les `include` vers un fichier
- [ ] Implémenter `textDocument/documentSymbol` :
  - [ ] Outline : settings, envs, variables
  - [ ] Structure arborescente

**Critères de succès** :
- ✅ Ctrl+Click sur variable → déclaration
- ✅ Ctrl+Click sur include → ouvre fichier
- ✅ Find All References fonctionnel
- ✅ Outline visible dans l'explorateur

---

### Phase 7 : Formatting & Code Actions 🔧

**Objectif** : Formatage automatique et suggestions.

**Durée estimée** : 3-4 jours

#### Tâches Formatting
- [ ] Implémenter `textDocument/formatting` :
  - [ ] Indentation cohérente (2 ou 4 espaces)
  - [ ] Espacement autour des opérateurs
  - [ ] Alignement des valeurs (optionnel)
- [ ] Implémenter `textDocument/rangeFormatting`
- [ ] Options configurables via settings

#### Tâches Code Actions (Quick Fixes)
- [ ] Implémenter `textDocument/codeAction` :
  - [ ] Variable non définie → "Create let variable"
  - [ ] Include non trouvé → "Create file"
  - [ ] Valeur dupliquée → "Extract to variable"
  - [ ] Env vide → "Remove empty environment"
- [ ] Light bulb / Quick fixes dans l'éditeur

**Critères de succès** :
- ✅ Format Document fonctionne
- ✅ Au moins 3 code actions
- ✅ Light bulb visible sur les warnings

---

### Phase 8 : Extension Visual Studio 2022+ 🎯

**Objectif** : Extension VSIX complète pour VS 2022+.

**Durée estimée** : 3-4 jours

#### Tâches
- [ ] Créer projet VSIX :
  ```xml
  <PackageReference Include="Microsoft.VisualStudio.LanguageServer.Client" />
  ```
- [ ] Implémenter `ILanguageClient` :
  ```csharp
  [ContentType("settex")]
  [Export(typeof(ILanguageClient))]
  public class SettexLanguageClient : ILanguageClient
  {
      public string Name => "Settex Language Server";
      
      public async Task<Connection> ActivateAsync(...);
      public async Task OnLoadedAsync();
  }
  ```
- [ ] Définir Content Type :
  ```csharp
  [Export]
  [Name("settex")]
  [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
  internal static ContentTypeDefinition SettexContentType;

  [Export]
  [FileExtension(".settex")]
  [ContentType("settex")]
  internal static FileExtensionToContentTypeDefinition SettexFileExtension;
  ```
- [ ] Intégrer TextMate grammar (`.pkgdef`)
- [ ] Configurer distribution du Language Server :
  - [ ] Bundled dans VSIX
  - [ ] Ou téléchargement à la demande
- [ ] Tester dans VS 2022 Experimental Instance
- [ ] Préparer manifest VSIX pour marketplace

**Critères de succès** :
- ✅ Installation dans VS 2022+
- ✅ Coloration syntaxique
- ✅ Toutes les features LSP fonctionnelles
- ✅ Pas d'erreurs dans Activity Log

---

### Phase 9 : Intégration LSP dans VS Code 🔌

**Objectif** : Connecter VS Code extension au Language Server.

**Durée estimée** : 2-3 jours

#### Tâches
- [ ] Ajouter dépendances client LSP :
  ```json
  "dependencies": {
    "vscode-languageclient": "^9.0.0"
  }
  ```
- [ ] Créer client LSP TypeScript :
  ```typescript
  import { LanguageClient, LanguageClientOptions, ServerOptions } from 'vscode-languageclient/node';

  export function activate(context: vscode.ExtensionContext) {
    const serverModule = context.asAbsolutePath('server/Settex.LanguageServer.exe');
    
    const serverOptions: ServerOptions = {
      run: { command: serverModule, transport: TransportKind.stdio },
      debug: { command: serverModule, transport: TransportKind.stdio }
    };
    
    const clientOptions: LanguageClientOptions = {
      documentSelector: [{ scheme: 'file', language: 'settex' }],
      synchronize: {
        fileEvents: workspace.createFileSystemWatcher('**/*.settex')
      }
    };
    
    const client = new LanguageClient('settex', 'Settex Language Server', serverOptions, clientOptions);
    client.start();
  }
  ```
- [ ] Bundler le serveur .NET avec l'extension :
  - [ ] Self-contained executable
  - [ ] Ou installer comme dotnet tool
- [ ] Configuration settings VS Code :
  ```json
  "settex.maxNumberOfProblems": 100,
  "settex.trace.server": "verbose",
  "settex.formatting.indentSize": 2
  ```
- [ ] Tester toutes les features LSP

**Critères de succès** :
- ✅ Démarrage automatique du serveur
- ✅ Toutes les features fonctionnelles
- ✅ Performance acceptable (<100ms pour completion)
- ✅ Logs accessibles pour debug

---

### Phase 10 : Tests & Qualité 🧪

**Objectif** : Tests automatisés pour la stabilité.

**Durée estimée** : 3-4 jours

#### Tâches
- [ ] Tests unitaires Language Server :
  - [ ] Document parsing
  - [ ] Symbol resolution
  - [ ] Completion generation
  - [ ] Diagnostic production
- [ ] Tests d'intégration LSP :
  - [ ] Client/Server protocol tests
  - [ ] Workspace synchronization
- [ ] Tests E2E VS Code :
  - [ ] Using `@vscode/test-electron`
  - [ ] Scenarios utilisateur réels
- [ ] Tests manuels Visual Studio
- [ ] Benchmarks performance :
  - [ ] Temps d'ouverture fichier large
  - [ ] Latence completion
  - [ ] Mémoire utilisée

**Critères de succès** :
- ✅ >80% couverture Language Server
- ✅ Tests E2E passent en CI
- ✅ Latence completion <200ms
- ✅ Mémoire <100MB pour projet moyen

---

### Phase 11 : Documentation & Publication 📚

**Objectif** : Documentation utilisateur et publication.

**Durée estimée** : 2-3 jours

#### Tâches Documentation
- [ ] README VS Code marketplace :
  - [ ] GIF démonstration
  - [ ] Features list
  - [ ] Configuration options
  - [ ] Troubleshooting
- [ ] README VS marketplace :
  - [ ] Screenshots Visual Studio
  - [ ] Installation guide
  - [ ] Configuration
- [ ] Contributing guide pour extensions

#### Tâches Publication
- [ ] VS Code Marketplace :
  - [ ] `vsce package`
  - [ ] `vsce publish`
- [ ] Visual Studio Marketplace :
  - [ ] Publisher account
  - [ ] VSIX upload
- [ ] GitHub Releases :
  - [ ] Language Server standalone
  - [ ] Extensions packagées

**Critères de succès** :
- ✅ Extensions publiées et installables
- ✅ Documentation complète
- ✅ Au moins 10 installations test
- ✅ Pas de crash reports

---

## 3. Récapitulatif des phases

| Phase | Description | Durée | Priorité |
|-------|-------------|-------|----------|
| 1 | TextMate Grammar | 1-2j | 🔴 Haute |
| 2 | VS Code Extension basique | 2-3j | 🔴 Haute |
| 3 | Language Server Core | 5-7j | 🔴 Haute |
| 4 | IntelliSense (Completion) | 4-5j | 🔴 Haute |
| 5 | Hover & Signature Help | 2-3j | 🟡 Medium |
| 6 | Navigation | 3-4j | 🟡 Medium |
| 7 | Formatting & Code Actions | 3-4j | 🟡 Medium |
| 8 | Visual Studio Extension | 3-4j | 🟡 Medium |
| 9 | VS Code LSP Integration | 2-3j | 🔴 Haute |
| 10 | Tests & Qualité | 3-4j | 🟡 Medium |
| 11 | Documentation & Publication | 2-3j | 🔴 Haute |

**Total estimé** : 30-42 jours de développement

---

## 4. Dépendances techniques

### Language Server (.NET)
```xml
<PackageReference Include="Microsoft.VisualStudio.LanguageServer.Protocol" Version="17.*" />
<PackageReference Include="StreamJsonRpc" Version="2.*" />
<PackageReference Include="Nerdbank.Streams" Version="2.*" />
```

### VS Code Extension (TypeScript/Node.js)
```json
{
  "dependencies": {
    "vscode-languageclient": "^9.0.0",
    "vscode-languageserver-protocol": "^3.17.0"
  },
  "devDependencies": {
    "@types/vscode": "^1.85.0",
    "@vscode/test-electron": "^2.3.0",
    "typescript": "^5.3.0",
    "esbuild": "^0.19.0"
  }
}
```

### Visual Studio Extension (C#)
```xml
<PackageReference Include="Microsoft.VisualStudio.LanguageServer.Client" Version="17.*" />
<PackageReference Include="Microsoft.VisualStudio.SDK" Version="17.*" />
```

---

## 5. Ressources et références

### Documentation officielle
- [VS Code Language Server Extension Guide](https://code.visualstudio.com/api/language-extensions/language-server-extension-guide)
- [Visual Studio LSP Extension](https://learn.microsoft.com/en-us/visualstudio/extensibility/adding-an-lsp-extension)
- [TextMate Grammars](https://macromates.com/manual/en/language_grammars)
- [Language Configuration](https://learn.microsoft.com/en-us/visualstudio/extensibility/language-configuration)

### Exemples de référence
- [vscode-languageserver-node](https://github.com/microsoft/vscode-languageserver-node)
- [VSSDK LSP Sample](https://github.com/Microsoft/VSSDK-Extensibility-Samples/tree/master/LanguageServerProtocol)
- [OmniSharp LSP](https://github.com/OmniSharp/omnisharp-roslyn)

### Outils
- [Yeoman VS Code Generator](https://github.com/Microsoft/vscode-generator-code)
- [vsce (VS Code Extension Manager)](https://github.com/microsoft/vscode-vsce)
- [VSIX Tools](https://learn.microsoft.com/en-us/visualstudio/extensibility/vsix-project-template)

---

## 6. Checklist Definition of Done V3

### Extensions
- [ ] VS Code extension publiée sur marketplace
- [ ] Visual Studio extension publiée sur marketplace
- [ ] Language Server distribué standalone

### Fonctionnalités Core
- [ ] Coloration syntaxique complète
- [ ] IntelliSense (completion) fonctionnel
- [ ] Diagnostics temps réel
- [ ] Hover informatif
- [ ] Go to Definition
- [ ] Find All References

### Fonctionnalités Avancées
- [ ] Formatting document
- [ ] Code Actions (quick fixes)
- [ ] Snippets complets
- [ ] Configuration utilisateur

### Qualité
- [ ] Tests automatisés
- [ ] Documentation utilisateur
- [ ] Performance acceptable
- [ ] Pas de memory leaks

---

## 7. Risques et mitigations

| Risque | Impact | Mitigation |
|--------|--------|------------|
| Complexité LSP | Fort | Commencer par features minimales |
| Performance parsing | Medium | Cache AST, parsing incrémental |
| Compatibilité VS versions | Medium | Cibler VS 2022 17.8+ minimum |
| Distribution serveur | Medium | Self-contained exe ou dotnet tool |
| Maintenance 2 extensions | Medium | Maximum de code partagé (grammar, server) |

---

**Note** : Ce plan peut être ajusté selon les retours utilisateurs après les premières phases. Les phases 1-4 et 9 constituent le MVP (Minimum Viable Product).
