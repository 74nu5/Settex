# Plan d'implémentation Settex V1

> **Settex** = Settings + Syntax + Extension  
> Extension fichier : `*.settex`  
> Namespace : `Settex` / `Settex.Build`

---

## 1. Vue d'ensemble

### Objectif
Implémenter un compilateur Settex qui transforme un fichier source `*.settex` en fichiers `appsettings*.json` pour projets .NET.

### Principes clés
- **Déterminisme** : à entrée identique → sortie identique (ordre des clés, formatage)
- **Diagnostics précis** : toutes les erreurs incluent ligne/colonne
- **Intégration MSBuild** : génération avant le build .NET

### Architecture cible

```
┌─────────────────────────────────────────────────────────────┐
│                         Settex.Core                         │
├─────────────────────────────────────────────────────────────┤
│  Source (.settex)                                           │
│       ↓                                                     │
│  [Lexer] ─────→ Tokens                                      │
│       ↓                                                     │
│  [Parser] ────→ AST                                         │
│       ↓                                                     │
│  [Evaluator] ─→ SettingsModel (base + overlays)             │
│       ↓                                                     │
│  [Merger] ────→ JSON Models par environnement               │
│       ↓                                                     │
│  [JsonWriter] → appsettings*.json                           │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                        Settex.Build                         │
├─────────────────────────────────────────────────────────────┤
│  MSBuild Task pour intégration dans le build .NET           │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. Structure de la solution

```
Settex/
├── src/
│   ├── Settex.Core/           # Bibliothèque principale
│   │   ├── Lexer/
│   │   │   ├── Token.cs
│   │   │   ├── TokenType.cs
│   │   │   ├── Lexer.cs
│   │   │   └── LexerException.cs
│   │   ├── Parser/
│   │   │   ├── Ast/
│   │   │   │   ├── IAstNode.cs
│   │   │   │   ├── FileNode.cs
│   │   │   │   ├── SettingsBlockNode.cs
│   │   │   │   ├── EnvBlockNode.cs
│   │   │   │   ├── BlockNode.cs
│   │   │   │   ├── AssignmentNode.cs
│   │   │   │   ├── NestedBlockNode.cs
│   │   │   │   ├── PathNode.cs
│   │   │   │   ├── ArrayNode.cs
│   │   │   │   ├── LiteralNode.cs
│   │   │   │   └── TaggedObjectNode.cs
│   │   │   ├── Parser.cs
│   │   │   └── ParserException.cs
│   │   ├── Evaluation/
│   │   │   ├── Evaluator.cs
│   │   │   └── SettingsModel.cs
│   │   ├── Merging/
│   │   │   ├── Merger.cs
│   │   │   └── MergeException.cs
│   │   ├── Output/
│   │   │   └── JsonWriter.cs
│   │   ├── Diagnostics/
│   │   │   ├── Diagnostic.cs
│   │   │   ├── DiagnosticCode.cs
│   │   │   ├── DiagnosticSeverity.cs
│   │   │   └── SourceLocation.cs
│   │   ├── SettexCompiler.cs      # Façade principale
│   │   └── CompilationResult.cs
│   │
│   ├── Settex.Build/              # MSBuild Task
│   │   ├── CompileSettexTask.cs
│   │   ├── Settex.Build.csproj
│   │   └── build/
│   │       ├── Settex.Build.props
│   │       └── Settex.Build.targets
│   │
│   └── Settex.Cli/                # CLI Tool (optionnel)
│       ├── Program.cs
│       └── Settex.Cli.csproj
│
├── tests/
│   ├── Settex.Core.Tests/
│   │   ├── Lexer/
│   │   │   └── LexerTests.cs
│   │   ├── Parser/
│   │   │   └── ParserTests.cs
│   │   ├── Evaluation/
│   │   │   └── EvaluatorTests.cs
│   │   ├── Merging/
│   │   │   └── MergerTests.cs
│   │   ├── Output/
│   │   │   └── JsonWriterTests.cs
│   │   ├── Integration/
│   │   │   ├── GoldenFileTests.cs
│   │   │   ├── ErrorTests.cs
│   │   │   └── TestData/
│   │   │       ├── basic/
│   │   │       │   ├── input.settex
│   │   │       │   └── expected/
│   │   │       │       └── appsettings.json
│   │   │       ├── with-environments/
│   │   │       │   ├── input.settex
│   │   │       │   └── expected/
│   │   │       │       ├── appsettings.json
│   │   │       │       ├── appsettings.Development.json
│   │   │       │       └── appsettings.Production.json
│   │   │       └── errors/
│   │   │           ├── no-settings.settex
│   │   │           └── no-settings.expected-error
│   │   └── Helpers/
│   │       └── GoldenFileTestHelper.cs
│   │
│   └── Settex.Build.Tests/
│       └── MSBuildIntegrationTests.cs
│
├── samples/
│   └── SampleWebApp/
│       ├── SampleWebApp.csproj
│       ├── appsettings.settex
│       └── Program.cs
│
├── Settex.sln
├── Directory.Build.props
├── Directory.Packages.props      # Central Package Management
└── .editorconfig
```

---

## 3. Plan de travail

### Phase 1 : Infrastructure projet ✅ COMPLÉTÉE
- [x] Créer la solution `Settex.sln`
- [x] Créer le projet `Settex.Core` (net10.0, netstandard2.0)
- [x] Créer le projet `Settex.Core.Tests` (TUnit)
- [x] Configurer `Directory.Build.props` (versions, conventions)
- [x] Configurer `Directory.Packages.props` (gestion centralisée des packages)
- [x] Créer `.editorconfig` (conventions de code)
- [x] Créer structure de dossiers complète
- [ ] Configurer CI/CD basique (optionnel - reporté)

### Phase 2 : Lexer ✅ COMPLÉTÉE
- [x] Définir `TokenType` enum :
  - [x] Mots-clés : `Settings`, `Env`, `True`, `False`, `Null`
  - [x] Symboles : `LeftBrace`, `RightBrace`, `LeftBracket`, `RightBracket`, `Equals`, `Dot`, `Comma`, `Semicolon`
  - [x] Littéraux : `String`, `Integer`, `Float`
  - [x] Autres : `Identifier`, `Newline`, `Comment`, `Eof`
- [x] Implémenter `Token` record avec position (ligne/colonne)
- [x] Implémenter `SourceLocation` pour les diagnostics
- [x] Implémenter `Lexer.cs` :
  - [x] Tokenisation des mots-clés : `settings`, `env`, `true`, `false`, `null`
  - [x] Tokenisation des identifiants : `[A-Za-z_][A-Za-z0-9_]*`
  - [x] Tokenisation des chaînes avec échappements : `\"`, `\\`, `\n`, `\r`, `\t`
  - [x] Tokenisation des nombres (entiers `-?\d+` et décimaux `-?\d+\.\d+`)
  - [x] Tokenisation des symboles : `{`, `}`, `[`, `]`, `=`, `.`, `,`, `;`
  - [x] Gestion des commentaires : `#` et `//` (jusqu'à fin de ligne)
  - [x] Gestion des espaces (ignorés) et sauts de ligne (`Newline` token pour tableaux)
- [x] Tests unitaires Lexer (17 tests - tous passent ✨)

### Phase 3 : Parser + AST
- [ ] Définir les nœuds AST (avec `SourceLocation` sur chaque nœud) :
  - [ ] `FileNode` (racine, contient topStmt*)
  - [ ] `SettingsBlockNode` (settings { ... })
  - [ ] `EnvBlockNode` (env "Name" { settings { ... } })
  - [ ] `BlockNode` (contenu d'un bloc { stmt* })
  - [ ] `AssignmentNode` (path = value)
  - [ ] `NestedBlockNode` (Ident { ... } comme statement)
  - [ ] `PathNode` (a.b.c - séquence d'identifiants)
  - [ ] `ValueNode` (interface/base pour toutes les valeurs)
  - [ ] `LiteralNode` (string, number, bool, null)
  - [ ] `ArrayNode` ([ items ])
  - [ ] `TaggedObjectNode` (ident { ... } comme valeur dans tableau)
- [ ] Implémenter `Parser.cs` (descente récursive) :
  - [ ] `ParseFile()` → topStmt* EOF
  - [ ] `ParseTopStmt()` → settingsBlock | envBlock | comment | ";"
  - [ ] `ParseSettingsBlock()` → "settings" block
  - [ ] `ParseEnvBlock()` → "env" string "{" settingsBlock "}"
  - [ ] `ParseBlock()` → "{" stmt* "}"
  - [ ] `ParseStmt()` → assignStmt | nestedBlockStmt | ";" | comment
  - [ ] `ParseAssignStmt()` → path "=" value
  - [ ] `ParseNestedBlockStmt()` → ident block
  - [ ] `ParsePath()` → ident ("." ident)*
  - [ ] `ParseValue()` → literal | array | taggedObjectValue
  - [ ] `ParseArray()` → "[" arrayItems? "]" (virgules/newlines optionnels)
  - [ ] `ParseTaggedObjectValue()` → ident block
  - [ ] Gestion des erreurs avec position ligne/colonne
- [ ] Tests unitaires Parser (couverture > 90%)

### Phase 4 : Evaluator
- [ ] Définir `SettingsModel` :
  - [ ] `BaseSettings` : JsonObject représentant le bloc settings principal
  - [ ] `EnvironmentOverlays` : Dictionary<string, JsonObject> pour chaque env
- [ ] Implémenter `Evaluator.cs` :
  - [ ] Validation structure :
    - [ ] Exactement un bloc `settings` au niveau global
    - [ ] Chaque bloc `env` contient exactement un bloc `settings` interne
    - [ ] Pas de blocs `env` dupliqués (même nom)
  - [ ] Conversion AST → modèle JSON intermédiaire :
    - [ ] `NestedBlockNode` → objet JSON
    - [ ] `AssignmentNode` → propriété (avec gestion dot-path)
    - [ ] `ArrayNode` → tableau JSON
    - [ ] `TaggedObjectNode` → objet dans tableau (le tag est ignoré)
    - [ ] `LiteralNode` → valeur primitive
  - [ ] Gestion des assignations par chemin (dot-path) :
    - [ ] `A.B.C = value` → création automatique d'objets intermédiaires
    - [ ] Erreur si chemin traverse un non-objet (ex: A est un array)
  - [ ] Collecte des erreurs sémantiques avec positions
- [ ] Tests unitaires Evaluator

### Phase 5 : Merger
- [ ] Implémenter `Merger.cs` :
  - [ ] Merge profond objet + objet (récursif)
  - [ ] Remplacement pour primitives
  - [ ] Remplacement complet pour tableaux
  - [ ] Détection et erreur pour type mismatch
- [ ] Fonction `Merge(base, overlay) → merged`
- [ ] Tests unitaires Merger (cas normaux + edge cases)

### Phase 6 : JsonWriter
- [ ] Implémenter `JsonWriter.cs` :
  - [ ] Sérialisation du modèle en JSON formaté (indentation cohérente)
  - [ ] **Déterminisme** : ordre des clés préservé (insertion order)
  - [ ] Écriture conditionnelle (si contenu identique → pas d'écriture)
  - [ ] Validation nom d'environnement pour filesystem (caractères invalides)
  - [ ] Gestion des erreurs d'écriture (permissions, path invalide)
- [ ] Tests unitaires JsonWriter

### Phase 7 : Compiler Façade + Diagnostics
- [ ] Implémenter `SettexCompiler.cs` :
  - [ ] API publique : `Compile(sourceFile, outputDir)`
  - [ ] Orchestration Lexer → Parser → Evaluator → Merger → JsonWriter
  - [ ] Collecte et formatage des diagnostics
- [ ] Implémenter système de diagnostics :
  - [ ] `Diagnostic` avec sévérité, message, location
  - [ ] Messages d'erreur lisibles avec ligne/colonne
  - [ ] Code de sortie approprié (0 = succès, != 0 = erreur)
- [ ] Tests d'intégration avec "golden files"

### Phase 8 : MSBuild Task (Settex.Build)
- [ ] Créer le projet `Settex.Build`
- [ ] Implémenter `CompileSettexTask.cs` :
  - [ ] Propriétés : `SettexFile`, `OutputDirectory`
  - [ ] Exécution du compilateur
  - [ ] Logging vers MSBuild (erreurs avec ligne/col)
- [ ] Créer `Settex.Build.targets` :
  - [ ] Target `CompileSettex` avant `Build`
  - [ ] Déclaration `Inputs/Outputs` pour incrémentalité MSBuild
  - [ ] Auto-détection des fichiers `*.settex`
- [ ] Créer le package NuGet `Settex.Build`
- [ ] Tests d'intégration MSBuild

### Phase 9 : CLI Tool (optionnel V1)
- [ ] Créer le projet `Settex.Cli` (dotnet tool)
- [ ] Commande `settex build <file> [-o <output>]`
- [ ] Affichage des diagnostics formatés
- [ ] Code de sortie : 0 = succès, 1 = erreur
- [ ] Package NuGet `Settex.Cli` (dotnet tool install)

### Phase 10 : Finalisation
- [ ] Créer un sample project fonctionnel
- [ ] Documentation README.md complète
- [ ] Validation de tous les critères de succès V1 :
  - [ ] Parse `settings` + `env`
  - [ ] Blocs taggés → objets JSON
  - [ ] Assignations `a.b.c = value`
  - [ ] Tableaux multi-lignes (virgules optionnelles)
  - [ ] Merge profond objets
  - [ ] Listes remplacées
  - [ ] Diagnostics ligne/col
  - [ ] Génération `appsettings*.json` sur disque
  - [ ] Écriture conditionnelle

---

## 4. Conventions de développement

### Nommage
- Namespace racine : `Settex`
- Extension fichier : `.settex`
- Package NuGet : `Settex.Build`

### Tests "Golden Files" (sans framework externe)

Implémentation manuelle des tests golden files :

```csharp
// GoldenFileTestHelper.cs
public static class GoldenFileTestHelper
{
    public static async Task AssertGoldenAsync(string testDataFolder)
    {
        var inputPath = Path.Combine(testDataFolder, "input.settex");
        var expectedDir = Path.Combine(testDataFolder, "expected");
        var actualDir = Path.Combine(testDataFolder, "actual");
        
        // Clean and create actual directory
        if (Directory.Exists(actualDir))
            Directory.Delete(actualDir, recursive: true);
        Directory.CreateDirectory(actualDir);
        
        // Compile
        var compiler = new SettexCompiler();
        var result = compiler.Compile(inputPath, actualDir);
        
        Assert.That(result.Success).IsTrue();
        
        // Compare each expected file
        foreach (var expectedFile in Directory.GetFiles(expectedDir, "*.json"))
        {
            var fileName = Path.GetFileName(expectedFile);
            var actualFile = Path.Combine(actualDir, fileName);
            
            Assert.That(File.Exists(actualFile))
                .IsTrue($"Missing: {fileName}");
            
            var expected = await File.ReadAllTextAsync(expectedFile);
            var actual = await File.ReadAllTextAsync(actualFile);
            
            Assert.That(actual).IsEqualTo(expected);
        }
    }
}
```

Structure d'un test golden :
```
tests/Settex.Core.Tests/Integration/TestData/<test-name>/
├── input.settex
└── expected/
    ├── appsettings.json
    ├── appsettings.Development.json (si applicable)
    └── appsettings.Production.json (si applicable)
```

### Gestion des erreurs
- Toutes les erreurs incluent ligne et colonne
- Format : `[<severity>] <file>(<line>,<col>): <message>`
- Exemple : `[Error] appsettings.settex(12,5): Unexpected token '}'`

---

## 5. Dépendances recommandées

| Package | Version | Usage |
|---------|---------|-------|
| `System.Text.Json` | (built-in) | Sérialisation JSON |
| `Microsoft.Build.Framework` | 17.x | MSBuild Task |
| `Microsoft.Build.Utilities.Core` | 17.x | MSBuild Task utilities |
| `TUnit` | latest | Framework de tests |

---

## 6. Grammaire de référence (EBNF)

```ebnf
file            := topStmt* EOF ;

topStmt         := settingsBlock
                 | envBlock
                 | comment
                 | ";" ;

settingsBlock   := "settings" block ;

envBlock        := "env" string "{" settingsBlock "}" ;

block           := "{" stmt* "}" ;

stmt            := assignStmt
                 | nestedBlockStmt
                 | ";" 
                 | comment ;

nestedBlockStmt := ident block ;

assignStmt      := path "=" value ;

path            := ident ("." ident)* ;

value           := literal
                 | array
                 | taggedObjectValue ;

taggedObjectValue := ident block ;

array           := "[" arrayItems? "]" ;

arrayItems      := arrayItem (arraySep arrayItem)* arraySep? ;
arraySep        := "," | NEWLINE ;

arrayItem       := literal
                 | taggedObjectValue ;

literal         := string | number | "true" | "false" | "null" ;

string          := QUOTED_STRING ;
number          := INT | FLOAT ;
ident           := IDENT ;
comment         := LINE_COMMENT ;
```

---

## 7. Estimation des efforts

| Phase | Complexité | Priorité |
|-------|------------|----------|
| Phase 1 : Infrastructure | Faible | Haute |
| Phase 2 : Lexer | Moyenne | Haute |
| Phase 3 : Parser + AST | Haute | Haute |
| Phase 4 : Evaluator | Haute | Haute |
| Phase 5 : Merger | Moyenne | Haute |
| Phase 6 : JsonWriter | Faible | Haute |
| Phase 7 : Compiler + Diagnostics | Moyenne | Haute |
| Phase 8 : MSBuild Task | Moyenne | Moyenne |
| Phase 9 : CLI Tool | Faible | Basse |
| Phase 10 : Finalisation | Faible | Haute |

---

## 8. Risques et mitigations

| Risque | Mitigation |
|--------|------------|
| Ambiguïté parsing tableaux multi-lignes | Tests exhaustifs avec différentes combinaisons virgules/newlines |
| Merge complexe objets imbriqués | Tests unitaires pour chaque règle de merge |
| Compatibilité MSBuild versions | Cibler `netstandard2.0` pour la task |
| Performance gros fichiers | Benchmark + optimisation si nécessaire (V1 = petits fichiers OK) |

---

## 9. Critères de validation V1

Tous les items suivants doivent passer pour considérer la V1 complète :

- [ ] Compilation réussie d'un fichier `*.settex` avec `settings` et `env`
- [ ] Génération correcte de `appsettings.json`
- [ ] Génération correcte de `appsettings.<Env>.json` pour chaque environnement
- [ ] Merge profond fonctionnel (objet + objet)
- [ ] Remplacement tableaux fonctionnel
- [ ] Assignations dot-path fonctionnelles
- [ ] Messages d'erreur avec ligne/colonne
- [ ] Écriture conditionnelle (pas de réécriture si identique)
- [ ] Intégration MSBuild fonctionnelle
- [ ] Tests passants (> 90% couverture)

---

## 10. Erreurs à implémenter (spec section 8)

### Erreurs de structure
- Absence de bloc `settings` au niveau global
- Plusieurs blocs `settings` au niveau global
- Bloc `env` sans bloc `settings` interne
- Blocs `env` dupliqués (même nom d'environnement)

### Erreurs de syntaxe
- Token inattendu
- Accolade/crochet non fermé
- Chaîne non terminée
- `env` sans chaîne de nom

### Erreurs sémantiques
- Assignation dot-path traversant un non-objet
- Merge impossible (type mismatch base/overlay)
- Élément de tableau invalide (doit être littéral ou objet taggé)

### Erreurs d'écriture
- Impossible d'écrire le fichier de sortie (permissions)
- Nom d'environnement invalide pour le filesystem

---

## 11. API publique (Settex.Core)

### SettexCompiler (façade principale)

```csharp
namespace Settex;

public class SettexCompiler
{
    /// <summary>
    /// Compile un fichier .settex et génère les fichiers JSON.
    /// </summary>
    /// <param name="sourceFile">Chemin vers le fichier .settex</param>
    /// <param name="outputDirectory">Répertoire de sortie (défaut: même dossier que source)</param>
    /// <returns>Résultat de compilation avec diagnostics</returns>
    public CompilationResult Compile(string sourceFile, string? outputDirectory = null);
    
    /// <summary>
    /// Compile du contenu .settex directement (pour tests/API).
    /// </summary>
    public CompilationResult CompileFromSource(string source, string? outputDirectory = null);
}

public record CompilationResult
{
    public bool Success { get; init; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; init; }
    public IReadOnlyList<string> GeneratedFiles { get; init; }
}

public record Diagnostic
{
    public DiagnosticSeverity Severity { get; init; }
    public string Message { get; init; }
    public SourceLocation? Location { get; init; }
    public string Code { get; init; } // Ex: "STX001"
}

public enum DiagnosticSeverity { Error, Warning, Info }

public record SourceLocation
{
    public string? FilePath { get; init; }
    public int Line { get; init; }      // 1-based
    public int Column { get; init; }    // 1-based
    public int Length { get; init; }    // Longueur du span
}
```

### Codes de diagnostic

| Code | Sévérité | Description |
|------|----------|-------------|
| STX001 | Error | Bloc `settings` manquant |
| STX002 | Error | Plusieurs blocs `settings` |
| STX003 | Error | Bloc `env` sans `settings` interne |
| STX004 | Error | Blocs `env` dupliqués |
| STX101 | Error | Token inattendu |
| STX102 | Error | Accolade/crochet non fermé |
| STX103 | Error | Chaîne non terminée |
| STX104 | Error | `env` sans nom |
| STX201 | Error | Assignation traverse un non-objet |
| STX202 | Error | Type mismatch lors du merge |
| STX203 | Error | Élément de tableau invalide |
| STX301 | Error | Erreur d'écriture fichier |
| STX302 | Error | Nom d'environnement invalide |

---

## 12. Cas de tests détaillés

### Tests Lexer

| Catégorie | Cas de test |
|-----------|-------------|
| Mots-clés | `settings`, `env`, `true`, `false`, `null` (casse exacte) |
| Identifiants | `foo`, `_bar`, `Baz123`, `__test__` |
| Chaînes | `"hello"`, `"with \"escape\""`, `"line\nbreak"`, `""` (vide) |
| Nombres | `42`, `-17`, `3.14`, `-0.5`, `0` |
| Symboles | Chaque symbole individuellement |
| Commentaires | `# comment`, `// comment`, commentaire en fin de ligne |
| Whitespace | Espaces, tabs, CR/LF, lignes vides |
| Erreurs | Chaîne non fermée, caractère invalide |

### Tests Parser

| Catégorie | Cas de test |
|-----------|-------------|
| Structure minimale | `settings { }` |
| Assignation simple | `Key = "value"` |
| Assignation dot-path | `A.B.C = 123` |
| Bloc taggé simple | `Server { Host = "x" }` |
| Bloc taggé imbriqué | `A { B { C { } } }` |
| Tableau vide | `Items = []` |
| Tableau littéraux | `[1, 2, 3]`, `["a" "b" "c"]` (sans virgules) |
| Tableau objets taggés | `[item { } item { }]` |
| Tableau mixte séparateurs | `[1, 2\n3\n4, 5]` |
| Env simple | `env "Dev" { settings { } }` |
| Plusieurs env | Dev + Prod + Staging |
| Commentaires partout | Dans blocs, entre statements |
| Erreurs syntaxe | Accolade manquante, token invalide |

### Tests Evaluator

| Catégorie | Cas de test |
|-----------|-------------|
| Conversion basique | Tous types primitifs |
| Dot-path création | `A.B = x` crée objets intermédiaires |
| Dot-path erreur | Traverse array ou primitive |
| Bloc taggé → objet | Tag ignoré, contenu converti |
| Plusieurs assignations même objet | Fusion correcte |
| Validation structure | 0, 1, 2+ blocs settings |
| Validation env | Env sans settings, env dupliqué |

### Tests Merger

| Catégorie | Cas de test |
|-----------|-------------|
| Objet + objet | Merge récursif profond |
| Primitive remplacée | String, number, bool, null |
| Array remplacé | Pas de merge, remplacement total |
| Objet vide overlay | Base préservée |
| Type mismatch | Objet vs array, primitive vs objet |
| Clés nouvelles | Ajout dans overlay |
| Clés supprimées | Non supporté V1 (overlay ne peut pas supprimer) |

### Tests Integration (Golden Files)

| Scénario | Description |
|----------|-------------|
| `basic` | Settings simple sans env |
| `with-environments` | Dev + Prod overlays |
| `nested-objects` | Objets imbriqués profonds |
| `arrays-literals` | Tableaux de primitifs |
| `arrays-objects` | Tableaux d'objets taggés |
| `dot-path-assignments` | Assignations par chemin |
| `comments-everywhere` | Commentaires # et // |
| `mixed-array-separators` | Virgules + newlines |
| `spec-example` | Exemple complet de la spec |

### Tests Erreurs

| Scénario | Erreur attendue |
|----------|-----------------|
| `no-settings` | STX001 |
| `multiple-settings` | STX002 |
| `env-no-settings` | STX003 |
| `duplicate-env` | STX004 |
| `unclosed-brace` | STX102 |
| `unclosed-string` | STX103 |
| `dot-path-through-array` | STX201 |
| `type-mismatch-merge` | STX202 |

---

## 13. Décisions techniques

### Représentation JSON intermédiaire

Utiliser `System.Text.Json.Nodes` (`JsonNode`, `JsonObject`, `JsonArray`, `JsonValue`) pour :
- Manipulation dynamique sans classes typées
- Sérialisation native avec `System.Text.Json`
- Préservation de l'ordre d'insertion (JsonObject utilise Dictionary avec ordre)

### Gestion des newlines dans les tableaux

Le Lexer produit des tokens `Newline` significatifs uniquement à l'intérieur des crochets `[...]`.
En dehors, les newlines sont ignorés comme whitespace.

**Implémentation suggérée** :
- Le Lexer maintient un compteur de profondeur `[`/`]`
- Si profondeur > 0 : émettre `Newline` tokens
- Si profondeur = 0 : ignorer comme whitespace

### Écriture conditionnelle

```csharp
// Pseudo-code
var newContent = SerializeToJson(model);
if (File.Exists(outputPath))
{
    var existingContent = File.ReadAllText(outputPath);
    if (existingContent == newContent)
        return; // Ne pas réécrire
}
File.WriteAllText(outputPath, newContent);
```

### Format de sortie JSON

- Indentation : 2 espaces
- Pas de trailing comma
- Encoding : UTF-8 sans BOM
- Newline : selon OS (Environment.NewLine) ou LF forcé (à décider)

---

## 14. Ordre d'implémentation recommandé

```
Phase 1 ──► Phase 2 ──► Phase 3 ──► Phase 4 ──► Phase 5 ──► Phase 6 ──► Phase 7
  │           │           │           │           │           │           │
  │           ▼           ▼           ▼           ▼           ▼           ▼
  │        Tests       Tests       Tests       Tests       Tests       Tests
  │        Lexer       Parser      Eval        Merger      Writer      Integ
  │
  └──────────────────────────────────────────────────────────────────────────►
                                                                              │
                                                              Phase 8 ──► Phase 9 ──► Phase 10
                                                              MSBuild      CLI        Final
```

**Points de validation intermédiaires :**

1. **Après Phase 3** : Parser produit un AST correct pour tous les cas syntaxiques
2. **Après Phase 5** : Le merge fonctionne isolément avec des JsonObject en entrée
3. **Après Phase 7** : Compilation end-to-end fonctionnelle (sans MSBuild)
4. **Après Phase 8** : Intégration complète dans un projet .NET
