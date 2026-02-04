# Settex V2 - Plan d'implémentation
## Fonctionnalités avancées : include, let, expressions, if inline, :=, for

**Date de création** : 2026-02-03  
**Statut** : Planification  
**Version** : 2.0

---

## 0. Vue d'ensemble

### Objectif V2

Transformer Settex en un véritable "SASS des appsettings" avec :
- **Variables** (`let`) avec scopes lexicaux
- **Expressions** arithmétiques, logiques, comparaisons, coalesce
- **Interpolation** de chaînes `"http://${host}:${port}"`
- **Conditions inline** : `Path = Value if Condition`
- **Set-if-missing** : `Path := DefaultValue`
- **Boucles** : `for` dans les tableaux
- **Modularité** : `include` pour fichiers séparés

### Nouvelles capacités

```settex
include "./common.settex"

let basePort = 5000
let host = "localhost"

let services = [
  service { name = "auth" port = basePort + 1 }
  service { name = "api"  port = basePort + 2 }
]

settings {
  ApplicationName = "MyApp"
  
  Server {
    Host := "0.0.0.0"        // set-if-missing
    Port := 8080
  }
  
  Logging.LogLevel.Default = "Debug" if env == "Development"
  Logging.LogLevel.Default = "Warning" if env == "Production"
  
  AllowedHosts = [
    "localhost"
    "*.local" if env != "Production"
    "myapp.com" if env == "Production"
  ]
  
  Services = [
    for s in services {
      item {
        Name = s.name
        Url = "http://${host}:${s.port}"
      }
    }
  ]
}

env "Development" {
  let basePort = 4000
  
  settings {
    Server.Port = basePort
  }
}
```

### Différences avec V1

| Fonctionnalité | V1 | V2 |
|----------------|----|----|
| Include | ❌ | ✅ |
| Variables (let) | ❌ | ✅ |
| Expressions | ❌ | ✅ (`+`, `-`, `*`, `/`, `==`, `and`, `??`, etc.) |
| Interpolation | ❌ | ✅ (`"${expr}"`) |
| If inline | ❌ | ✅ (`Path = Value if Condition`) |
| Set-if-missing | ❌ | ✅ (`:=`) |
| For loops | ❌ | ✅ (dans tableaux) |
| Blocs if/else | ❌ | ❌ (V3+) |
| Append liste (+=) | ❌ | ❌ (V3+) |

---

## 1. Architecture & stratégie

### 1.1 Approche incrémentale

**Phase par phase** :
1. Include system + cycle detection
2. Variables `let` + scopes
3. Expressions (Pratt parser) + types runtime
4. Interpolation de chaînes
5. If inline sur assignations
6. Opérateur `:=` (set-if-missing)
7. For loops dans tableaux
8. Tests golden files + validation complète

### 1.2 Modifications des composants existants

| Composant | Changements V2 |
|-----------|----------------|
| **Lexer** | Nouveaux tokens : `include`, `let`, `if`, `for`, `in`, `:=`, opérateurs (`+`, `-`, `*`, `/`, `==`, `!=`, `and`, `or`, `not`, `??`, etc.) |
| **Parser** | Nouvelles constructions AST : `IncludeNode`, `LetNode`, `ExpressionNode`, `IfInlineNode`, `ForNode`, `InterpolatedStringNode` |
| **Evaluator** | Évaluation d'expressions, gestion des scopes, résolution de variables, exécution de boucles |
| **Merger** | Pas de changement (merge rules identiques) |
| **JsonWriter** | Pas de changement |
| **Compiler** | Orchestration include, multi-passes pour variables, génération avec contexte `env` |

### 1.3 Nouveaux composants

- **IncludeResolver** : Résolution de chemins, détection de cycles
- **ExpressionEvaluator** : Évaluation d'expressions avec Pratt parser
- **VariableScope** : Gestion des scopes lexicaux (global, env, for)
- **RuntimeValue** : Représentation des valeurs typées (string, number, bool, null, array, object)
- **StringInterpolator** : Interpolation `${expr}` dans les chaînes

---

## 2. Phases d'implémentation

### Phase 1 : Include System 📁

**Objectif** : Permettre `include "./file.settex"` avec détection de cycles.

#### Tâches
- [x] Créer `IncludeResolver` avec méthodes :
  - [x] `ResolveIncludePath(string relativePath, string currentFilePath)` → chemin absolu
  - [x] `LoadAndParseFile(string filePath)` → FileNode AST
  - [x] `DetectCycle(Stack<string> includeStack, string filePath)` → bool
- [x] Ajouter token `INCLUDE` dans le Lexer
- [x] Ajouter `IncludeNode` dans l'AST :
  ```csharp
  public sealed record IncludeNode(string Path, SourceLocation Location) : ITopLevelStatement;
  ```
- [x] Parser : reconnaître `include "<path>"`
- [x] Compiler :
  - [x] Résoudre tous les includes récursivement
  - [x] Concaténer les AST (comme copié/collé)
  - [x] Erreur si cycle détecté
- [x] Tests :
  - [x] Include simple (1 fichier)
  - [x] Include imbriqué (A includes B, B includes C)
  - [x] Détection de cycle (A → B → A)
  - [x] Fichier introuvable
  - [x] Chemins relatifs corrects

**Critères de succès** :
- ✅ `include` fonctionne avec chemins relatifs
- ✅ Cycles détectés et bloqués avec erreur
- ✅ AST correctement concaténé
- ✅ Diagnostics avec ligne/colonne précis

**Notes d'implémentation** :
- IncludeResolver créé dans `src/Settex.Core/Resolution/`
- IncludeException pour gérer les erreurs d'include
- Parser.Parse() avec ParseFile() comme alias pour compatibilité
- Tous les tests passent (94/94)

---

### Phase 2 : Variables `let` + Scopes 🔤

**Objectif** : Déclarer et utiliser des variables avec scopes lexicaux.

#### Tâches
- [x] Créer `RuntimeValue` pour typage runtime :
  ```csharp
  public abstract record RuntimeValue;
  public sealed record StringValue(string Value) : RuntimeValue;
  public sealed record NumberValue(decimal Value) : RuntimeValue;
  public sealed record BoolValue(bool Value) : RuntimeValue;
  public sealed record NullValue : RuntimeValue;
  public sealed record ArrayValue(List<RuntimeValue> Items) : RuntimeValue;
  public sealed record ObjectValue(Dictionary<string, RuntimeValue> Properties) : RuntimeValue;
  ```
- [x] Créer `VariableScope` :
  ```csharp
  public sealed class VariableScope
  {
      private Dictionary<string, RuntimeValue> variables;
      private VariableScope? parent;
      
      public void Define(string name, RuntimeValue value);
      public RuntimeValue? Lookup(string name);
      public VariableScope CreateChild();
  }
  ```
- [x] Ajouter tokens `LET`
- [x] Ajouter `LetNode` dans l'AST :
  ```csharp
  public sealed record LetNode(string Name, IExpression Value, SourceLocation Location) : ITopLevelStatement;
  ```
- [x] Ajouter `VariableRefNode` pour références :
  ```csharp
  public sealed record VariableRefNode(string Name, SourceLocation Location) : IExpression;
  ```
- [x] Parser : `let ident = expr`
- [x] Evaluator :
  - [x] Créer scope global
  - [x] Évaluer `let` globaux avant `settings`
  - [x] Pour chaque env : créer scope enfant (copie + masquage)
  - [x] Résoudre variables dans expressions
- [x] Tests :
  - [x] Variable globale accessible partout
  - [x] Variable env masque global
  - [x] Variable utilisée avant définition → erreur
  - [x] Variable inconnue → erreur

**Critères de succès** :
- ✅ `let name = value` parse correctement
- ✅ Scopes global / env / for respectés
- ✅ Erreur si variable inconnue
- ✅ Masquage env sur global fonctionne

**Notes d'implémentation** :
- RuntimeValue créé dans `src/Settex.Core/Runtime/`
- VariableScope implémenté avec lookup récursif dans parent
- IExpression interface créée, IValue en hérite
- Parser.ParseExpression() supporte valeurs et références de variables
- ExpressionEvaluator créé pour évaluer IExpression → RuntimeValue
- Evaluator modifié pour gérer scopes et variables avec conversion RuntimeValue → JsonNode
- Parser.ParseEnvBlock() supporte let statements avant settings block
- AssignmentNode.Value et ArrayNode.Items changés en IExpression (au lieu de IValue)
- 8 tests d'évaluation de variables ajoutés dans VariableEvaluationTests.cs
- 104/104 tests passent ✅
- **COMPLÉTÉ** : Commit effectué (feat(v2): Phase 2 - Variables let + Scopes - complete)

---

### Phase 3 : Expressions (Pratt Parser) ➗

**Objectif** : Évaluer expressions arithmétiques, logiques, comparaisons, coalesce.

#### Tâches
- [x] Créer `ExpressionEvaluator` avec Pratt parser
- [x] Ajouter tous les tokens d'opérateurs :
  - [x] Arithmétique : `+`, `-`, `*`, `/`
  - [x] Comparaison : `==`, `!=`, `<`, `<=`, `>`, `>=`
  - [x] Logique : `and`, `or`, `not`
  - [x] Coalesce : `??`
- [x] Créer nœuds d'expressions :
  ```csharp
  public sealed record BinaryOpNode(IExpression Left, string Op, IExpression Right, SourceLocation Location) : IExpression;
  public sealed record UnaryOpNode(string Op, IExpression Operand, SourceLocation Location) : IExpression;
  public sealed record LiteralNode(...) : IExpression; // déjà existe
  ```
- [x] Parser expressions avec précédence :
  - [x] Pratt parser : `logicalOr` → `logicalAnd` → `coalesce` → `equality` → `comparison` → `term` → `factor` → `unary` → `primary`
- [x] Evaluator :
  - [x] Évaluer expressions récursivement
  - [x] Vérifier types compatibles pour chaque opérateur
  - [x] Erreur si types incompatibles
- [x] Tests :
  - [x] Arithmétique : `5 + 3 * 2` = 11
  - [x] Comparaison : `10 > 5` = true
  - [x] Logique : `true and false` = false
  - [x] Coalesce : `null ?? "default"` = "default"
  - [x] Précédence : `2 + 3 * 4` = 14
  - [x] Erreurs : `"hello" + 5` → erreur
  - [x] Bool requis pour `and`/`or`/`not`

**Critères de succès** :
- ✅ Toutes les opérations supportées
- ✅ Précédence correcte
- ✅ Erreurs de type détectées
- ✅ Tests avec combinaisons complexes

**Notes d'implémentation** :
- Lexer étendu avec tokens pour tous les opérateurs (Plus, Minus, Star, Slash, EqualEqual, etc.)
- BinaryOpNode et UnaryOpNode créés pour AST
- Pratt parser implémenté avec 9 niveaux de précédence
- ExpressionEvaluator étendu avec évaluation d'opérateurs (short-circuit pour and/or)
- BoolValue.True/False ajoutés comme constantes
- 26 tests d'expressions ajoutés dans ExpressionEvaluationTests.cs
- 130/130 tests passent ✅
- **COMPLÉTÉ** : Commit effectué (feat(v2): Phase 3 - Expressions (Pratt Parser) - complete)

---

### Phase 4 : Interpolation `"${expr}"` 💬

**Objectif** : Remplacer `${expr}` dans les chaînes par la valeur évaluée.

#### Tâches
- [x] Créer structures pour interpolation
- [x] Détection des `${...}` dans les chaînes lors du parsing
  - [x] Parser les segments littéraux et expressions
- [x] Ajouter `InterpolatedStringNode` :
  ```csharp
  public sealed record InterpolatedStringNode(
      List<StringSegment> Segments,
      SourceLocation Location
  ) : IExpression;
  
  public abstract record StringSegment;
  public sealed record LiteralSegment(string Text) : StringSegment;
  public sealed record ExpressionSegment(IExpression Expr) : StringSegment;
  ```
- [x] Evaluator :
  - [x] Évaluer chaque expression dans `${}`
  - [x] Concaténer les résultats
  - [x] Erreur si expression renvoie `null`
- [x] Tests :
  - [x] Simple : `"Hello ${name}"` avec `name = "World"` → "Hello World"
  - [x] Multiple : `"http://${host}:${port}"` → "http://localhost:8080"
  - [x] Expression : `"Result: ${5 + 3}"` → "Result: 8"
  - [x] Null → erreur

**Critères de succès** :
- ✅ Interpolation fonctionne avec variables
- ✅ Expressions évaluées correctement
- ✅ Null déclenche erreur
- ✅ Plusieurs `${}` dans une chaîne

**Notes d'implémentation** :
- InterpolatedStringNode créé avec LiteralSegment et ExpressionSegment
- Parser.ParseLiteral détecte `${` dans les chaînes et parse les interpolations
- ParseInterpolatedString gère le comptage des accolades imbriquées
- ExpressionEvaluator.EvaluateInterpolatedString évalue et concatène les segments
- Conversion automatique : numbers, bools, strings vers string
- 10 tests d'interpolation ajoutés dans InterpolationTests.cs
- 140/140 tests passent ✅
- **COMPLÉTÉ** : Commit effectué (feat(v2): Phase 4 - String Interpolation - complete)

---

### Phase 5 : If Inline sur Assignations 🔀

**Objectif** : `Path = Value if Condition` (assignation conditionnelle).

#### Tâches
- [x] Ajouter token `IF`
- [x] Modifier `AssignmentNode` pour inclure condition optionnelle :
  ```csharp
  public sealed record AssignmentNode(
      PathNode Path,
      IExpression Value,
      IExpression? Condition, // nouveau
      SourceLocation Location
  ) : IStatement;
  ```
- [x] Parser : `path assignOp expr ( "if" expr )?`
- [x] Evaluator :
  - [x] Si pas de condition → appliquer assignation
  - [x] Si condition présente :
    - [x] Évaluer condition → doit être bool, sinon erreur
    - [x] Si true → appliquer assignation
    - [x] Si false → ne rien faire
- [x] Variable implicite `env` :
  - [x] Lors génération base : `env = "Base"`
  - [x] Lors génération env `E` : `env = E`
- [x] Tests :
  - [x] `LogLevel = "Debug" if env == "Development"` → appliqué seulement en Dev
  - [x] Condition false → clé non créée
  - [x] Condition non-bool → erreur

**Critères de succès** :
- ✅ If inline fonctionne sur assignations
- ✅ Variable `env` disponible
- ✅ Condition false = pas d'assignation
- ✅ Erreur si condition non-bool

**Notes d'implémentation** :
- IF ajouté comme token (TokenType.If)
- AssignmentNode.Condition ajouté (nullable IExpression)
- Parser.ParseAssignmentStatement détecte `if` après la valeur
- Evaluator.Evaluate définit variable `env` dans chaque scope
- EvaluateAssignment vérifie la condition avant d'appliquer l'assignation
- Parser permet au mot-clé `env` d'être utilisé comme identifiant
- 11 tests de conditions ajoutés dans ConditionalAssignmentTests.cs
- 150/150 tests passent ✅
- **COMPLÉTÉ** : Commit effectué (feat(v2): Phase 5 - If Inline on Assignments - complete)

---

### Phase 6 : Opérateur `:=` (Set-If-Missing) 🔒

**Objectif** : `Path := DefaultValue` (ne set que si clé absente).

#### Tâches
- [x] Ajouter token `:=` (COLON_EQUALS)
- [x] Modifier `AssignmentNode` pour distinguer `=` et `:=` :
  ```csharp
  public enum AssignmentOp { Set, SetIfMissing }
  
  public sealed record AssignmentNode(
      PathNode Path,
      AssignmentOp Op, // nouveau
      IExpression Value,
      IExpression? Condition,
      SourceLocation Location
  ) : IStatement;
  ```
- [x] Parser : distinguer `=` et `:=`
- [x] Evaluator :
  - [x] Pour `:=` dans settings base :
    - [x] Vérifier si clé existe dans objet en construction
    - [x] Si absente → créer
    - [x] Si présente → ne rien faire
  - [x] Pour `:=` dans env :
    - [x] Vérifier si clé existe dans overlay **OU** dans base
    - [x] Si absente des deux → créer dans overlay
    - [x] Si présente → ne rien faire
- [x] Tests :
  - [x] Base : `Port := 8080` après `Port = 5000` → reste 5000
  - [x] Base : `Port := 8080` sans `Port` défini → devient 8080
  - [x] Env : `Port := 9000` avec `Port = 8080` dans base → reste 8080
  - [x] Null est considéré comme présent
  - [x] Combinaison avec `if inline`

**Critères de succès** :
- ✅ `:=` ne remplace pas valeur existante
- ✅ Règle spéciale env (consulte base + overlay)
- ✅ Combinaison avec `if inline` fonctionne
- ✅ `null` traité comme présent

**Notes d'implémentation** :
- Token ColonEquals ajouté au lexer
- AssignmentOp enum créé avec Set et SetIfMissing
- AssignmentNode modifié pour inclure Op: AssignmentOp
- Parser.ParseStatement étendu pour reconnaître := en lookahead
- Parser.ParseAssignmentStatement détecte = ou := et définit l'opérateur
- PathExists helper créé pour vérifier existence de clé dans JsonObject
- EvaluateBlock, EvaluateAssignment, EvaluateNestedBlock prennent baseSettings en paramètre
- EvaluateAssignment vérifie existence dans overlay + base avant d'assigner
- 11 tests créés dans SetIfMissingTests.cs
- 160/160 tests passent ✅
- **COMPLÉTÉ** : Commit effectué (feat(v2): Phase 6 - Set-If-Missing Operator (:=) - complete)

---

### Phase 7 : For Loops dans Tableaux 🔁

**Objectif** : `for ident in expr { ... }` pour générer éléments de tableau.

#### Tâches
- [ ] Ajouter tokens `FOR`, `IN`
- [ ] Créer `ForNode` :
  ```csharp
  public sealed record ForNode(
      string IteratorName,
      IExpression Collection,
      BlockNode Body,
      SourceLocation Location
  ) : IArrayElement;
  ```
- [ ] Modifier `ArrayNode` pour accepter `ForNode` :
  ```csharp
  public sealed record ArrayNode(
      List<IArrayElement> Elements,
      SourceLocation Location
  ) : IValue;
  
  public interface IArrayElement; // peut être IValue ou ForNode
  ```
- [ ] Parser : `for ident in expr block`
- [ ] Evaluator :
  - [ ] Évaluer `expr` → doit être array, sinon erreur
  - [ ] Pour chaque élément :
    - [ ] Créer scope enfant avec iterator variable
    - [ ] Évaluer le corps du for
    - [ ] Collecter les valeurs produites (items)
  - [ ] Aplatir les résultats dans le tableau final
- [ ] Tests :
  - [ ] For simple : générer 3 items
  - [ ] For avec expression : `for s in services { item { Url = "http://${s.name}" } }`
  - [ ] For imbriqué
  - [ ] For sur non-array → erreur
  - [ ] Scope iterator local au for

**Critères de succès** :
- ✅ For génère items dans tableau
- ✅ Iterator variable accessible dans corps
- ✅ Scope créé pour chaque itération
- ✅ Erreur si collection n'est pas array
- ✅ Combinaison avec interpolation

---

### Phase 8 : If Inline sur Éléments de Tableau (Optionnel) 🎛️

**Objectif** : `"value" if condition` dans les tableaux.

#### Tâches
- [ ] Modifier `ArrayElement` pour accepter condition :
  ```csharp
  public sealed record ConditionalArrayElement(
      IValue Value,
      IExpression? Condition,
      SourceLocation Location
  ) : IArrayElement;
  ```
- [ ] Parser : `arrayValue ( "if" expr )?`
- [ ] Evaluator :
  - [ ] Si condition absente → inclure élément
  - [ ] Si condition présente :
    - [ ] Évaluer → doit être bool
    - [ ] Si true → inclure
    - [ ] Si false → exclure
- [ ] Tests :
  - [ ] `["localhost", "shop.com" if env == "Production"]` → 1 ou 2 éléments selon env
  - [ ] Condition false → élément non ajouté
  - [ ] Combinaison avec for

**Critères de succès** :
- ✅ If inline fonctionne sur éléments tableau
- ✅ Condition false = élément exclu
- ✅ Combinaison for + if inline

---

### Phase 9 : Tests & Validation Complète ✅

**Objectif** : Tests golden files couvrant toutes les fonctionnalités V2.

#### Tâches
- [ ] Tests Include :
  - [ ] Include simple
  - [ ] Include imbriqué
  - [ ] Cycle détecté
  - [ ] Fichier manquant
- [ ] Tests Let & Scopes :
  - [ ] Variable globale
  - [ ] Variable env masque global
  - [ ] Variable for scope
  - [ ] Variable inconnue
- [ ] Tests Expressions :
  - [ ] Arithmétique (`+`, `-`, `*`, `/`)
  - [ ] Comparaison (`==`, `!=`, `<`, etc.)
  - [ ] Logique (`and`, `or`, `not`)
  - [ ] Coalesce (`??`)
  - [ ] Précédence
  - [ ] Erreurs de type
- [ ] Tests Interpolation :
  - [ ] Simple
  - [ ] Multiple `${}`
  - [ ] Expression dans `${}`
  - [ ] Null → erreur
- [ ] Tests If Inline :
  - [ ] Assignation conditionnelle
  - [ ] Variable `env`
  - [ ] Condition false
  - [ ] Condition non-bool → erreur
- [ ] Tests `:=` :
  - [ ] Set-if-missing base
  - [ ] Set-if-missing env (consulte base)
  - [ ] Avec `if inline`
- [ ] Tests For :
  - [ ] For simple
  - [ ] For avec interpolation
  - [ ] For imbriqué
  - [ ] For sur non-array → erreur
- [ ] Tests If inline tableau :
  - [ ] Élément conditionnel
  - [ ] Combinaison for + if
- [ ] Tests Intégration :
  - [ ] Exemple complet de la spec V2
  - [ ] Multi-fichiers avec includes
  - [ ] Variables + expressions + for + if
  - [ ] Génération 3+ environnements

**Critères de succès** :
- ✅ 50+ tests golden files
- ✅ Tous les cas d'erreur testés
- ✅ Exemples réalistes (config app complète)
- ✅ Couverture de code > 80%

---

### Phase 10 : Documentation & Samples V2 📚

**Objectif** : Documenter les nouvelles fonctionnalités avec exemples.

#### Tâches
- [ ] Mettre à jour README.md :
  - [ ] Section V2 features
  - [ ] Exemples include, let, expressions, if, :=, for
  - [ ] Guide de migration V1 → V2
- [ ] Créer samples/AdvancedWebApi :
  - [ ] Configuration multi-fichiers (common.settex, logging.settex, etc.)
  - [ ] Variables pour réutilisation
  - [ ] For loops pour services
  - [ ] If inline pour features flags
  - [ ] := pour valeurs par défaut
- [ ] Créer documentation technique :
  - [ ] Architecture V2
  - [ ] Grammaire EBNF complète
  - [ ] Guide d'extension (V3+)
- [ ] Tests de performance :
  - [ ] Benchmark include (10, 100, 1000 fichiers)
  - [ ] Benchmark expressions complexes
  - [ ] Benchmark for loops (1, 10, 100, 1000 iterations)

**Critères de succès** :
- ✅ README V2 complet
- ✅ Sample avancé fonctionnel
- ✅ Documentation technique
- ✅ Benchmarks performants

---

## 3. Checklist Definition of Done V2

### Fonctionnalités
- [ ] Include + détection de cycles
- [ ] Variables `let` (global, env, for scopes)
- [ ] Expressions (arithmétique, logique, comparaison, coalesce)
- [ ] Interpolation `"${expr}"` (null → erreur)
- [ ] If inline sur assignations
- [ ] Opérateur `:=` set-if-missing (avec règle env/base)
- [ ] For loops dans tableaux
- [ ] If inline sur éléments de tableau (optionnel)

### Qualité
- [ ] 50+ tests golden files
- [ ] Diagnostics ligne/col pour toutes les erreurs
- [ ] Codes d'erreur documentés
- [ ] Couverture de tests > 80%
- [ ] Benchmarks de performance
- [ ] Pas de régression V1

### Documentation
- [ ] README V2 avec exemples
- [ ] Guide de migration V1 → V2
- [ ] Sample projet avancé
- [ ] Documentation technique (architecture, grammaire)

### Livraison
- [ ] NuGet packages mis à jour (Settex.Core, Settex.Build, Settex.Cli)
- [ ] Versioning sémantique (2.0.0)
- [ ] Release notes détaillées
- [ ] Changelog V1 → V2

---

## 4. Ordre d'implémentation recommandé

1. **Phase 1** : Include System (fondation pour modularité)
2. **Phase 2** : Variables let + Scopes (requis pour tout le reste)
3. **Phase 3** : Expressions (Pratt parser, base pour if/for/interpolation)
4. **Phase 4** : Interpolation (utilise expressions)
5. **Phase 5** : If inline assignations (utilise expressions)
6. **Phase 6** : Opérateur `:=` (extension de l'assignation)
7. **Phase 7** : For loops (utilise expressions + scopes)
8. **Phase 8** : If inline tableaux (extension mineure)
9. **Phase 9** : Tests complets
10. **Phase 10** : Documentation

---

## 5. Risques & Mitigations

### Risques identifiés

| Risque | Impact | Probabilité | Mitigation |
|--------|--------|-------------|------------|
| Complexité Pratt parser | Élevé | Moyen | Étudier exemples existants, tests incrémentaux |
| Performance for loops | Moyen | Faible | Benchmarks early, optimisation si nécessaire |
| Cycles include complexes | Moyen | Moyen | Stack-based detection, tests exhaustifs |
| Scopes variables buggés | Élevé | Moyen | Tests unitaires exhaustifs, exemples clairs |
| Rupture compatibilité V1 | Élevé | Faible | Tests de régression V1, versioning strict |

### Stratégie de test

- **Tests unitaires** : Chaque composant isolé (parser, evaluator, etc.)
- **Tests golden files** : Cas d'usage réels bout-en-bout
- **Tests de régression V1** : Tous les tests V1 passent avec V2
- **Tests d'erreurs** : Tous les cas d'erreur documentés testés
- **Tests de performance** : Benchmarks pour éviter régressions

---

## 6. Évolutions futures (V3+)

### Fonctionnalités candidates V3

- `if/else` en bloc (avec scope)
- `+=` append pour listes
- Macros/mixins réutilisables
- Validation avec schémas
- Import depuis JSON/YAML
- Watch mode (recompilation automatique)
- LSP (Language Server Protocol) pour IDE
- Formatage automatique (formatter)

### Rétro-compatibilité

- V2 doit rester compatible V1 (syntaxe V1 valide en V2)
- V3 doit rester compatible V2
- Versioning sémantique strict

---

## 7. Statut des phases

| Phase | Statut | Complétée | Notes |
|-------|--------|-----------|-------|
| Phase 1: Include | ⏳ Planifié | - | - |
| Phase 2: Let + Scopes | ⏳ Planifié | - | - |
| Phase 3: Expressions | ⏳ Planifié | - | Pratt parser requis |
| Phase 4: Interpolation | ⏳ Planifié | - | Dépend de Phase 3 |
| Phase 5: If Inline Assign | ⏳ Planifié | - | Dépend de Phase 3 |
| Phase 6: Set-If-Missing | ⏳ Planifié | - | Extension assignation |
| Phase 7: For Loops | ⏳ Planifié | - | Dépend de Phase 2 & 3 |
| Phase 8: If Inline Array | ⏳ Planifié | - | Optionnel |
| Phase 9: Tests | ⏳ Planifié | - | - |
| Phase 10: Documentation | ⏳ Planifié | - | - |

---

## 8. Exemple complet de référence V2

```settex
// common.settex
let defaultLogLevel = "Information"

// main.settex
include "./common.settex"

let basePort = 5000
let host = "localhost"

let services = [
  service { name = "auth" port = basePort + 1 }
  service { name = "api"  port = basePort + 2 }
  service { name = "admin" port = basePort + 3 }
]

settings {
  ApplicationName = "ModernShop"
  Version = "2.0.0"
  
  Server {
    Host := "0.0.0.0"
    Port := 8080
  }
  
  Logging {
    LogLevel {
      Default := defaultLogLevel
      Microsoft := "Warning"
    }
  }
  
  // Conditional assignments
  Logging.LogLevel.Default = "Debug" if env == "Development"
  Logging.LogLevel.Default = "Warning" if env == "Production"
  
  AllowedHosts = [
    "localhost"
    "${host}" if env != "Production"
    "*.local" if env == "Development"
    "shop.com" if env == "Production"
  ]
  
  ConnectionStrings {
    DefaultConnection := "Server=localhost;Database=ShopDb"
  }
  
  Services = [
    for s in services {
      item {
        Name = s.name
        Url = "http://${host}:${s.port}"
        Enabled = true
      }
    }
  ]
  
  FeatureFlags {
    NewCheckout := false
    BetaFeatures := false
  }
}

env "Development" {
  let basePort = 4000
  let dbServer = "localhost"
  
  settings {
    Server.Port = basePort
    
    ConnectionStrings.DefaultConnection = "Server=${dbServer};Database=ShopDb_Dev"
    
    FeatureFlags.NewCheckout := true
    FeatureFlags.BetaFeatures := true
    
    Services = [
      for s in services {
        item {
          Name = s.name
          Url = "http://localhost:${s.port}"
          Enabled = true
          Debug = true
        }
      }
    ]
  }
}

env "Staging" {
  settings {
    Server.Port = 80
    
    ConnectionStrings.DefaultConnection = "Server=staging-db.example.com;Database=ShopDb"
    
    FeatureFlags.NewCheckout := true
    
    Services = [
      for s in services {
        item {
          Name = s.name
          Url = "https://${s.name}-staging.example.com"
          Enabled = s.name != "admin" if env == "Staging"
        }
      }
    ]
  }
}

env "Production" {
  settings {
    Server.Port = 443
    
    Logging.LogLevel.Default = "Error"
    
    ConnectionStrings.DefaultConnection = "${DB_CONNECTION_STRING}"
    
    Services = [
      for s in services {
        item {
          Name = s.name
          Url = "https://${s.name}.example.com"
          Enabled = true
        }
      }
    ]
  }
}
```

**Sorties attendues** :
- `appsettings.json` : base avec valeurs par défaut
- `appsettings.Development.json` : base + dev overlays (port 4000, debug, etc.)
- `appsettings.Staging.json` : base + staging overlays
- `appsettings.Production.json` : base + prod overlays (HTTPS, erreur logging, etc.)

---

**Date de dernière mise à jour** : 2026-02-03  
**Prochaine révision** : Après Phase 1
