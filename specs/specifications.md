Voici une **V1 de spec** au format **Markdown** (prête à coller dans un repo GitHub pour “piloter” GitHub Copilot).  
Elle décrit un langage “SASS des appsettings” qui **génère des fichiers JSON sur disque** (pas d’injection runtime), avec :

*   **blocs + clé/valeur**
*   **overlays par environnement**
*   **listes multi-lignes** (virgules optionnelles)
*   **objets “taggés”** via blocs (pas d’objets inline `{ ... }`)

> **Note** : V1 = volontairement “petit mais utile”. Les features “SASS avancées” (boucles, `if`, include, interpolation, etc.) sont listées en “hors scope / V2”.

***

# Settex — Spécification V1 (MVP)

## 1. Objectif

**Settex** (Settings + Syntax + Extension) est un langage de configuration déclaratif destiné à remplacer la duplication de fichiers `appsettings*.json` dans les projets .NET.

À partir d’un seul fichier source, le compilateur génère **sur disque** :

*   `appsettings.json` (base)
*   `appsettings.<Environment>.json` pour chaque environnement défini (`Development`, `Production`, etc.)

Le résultat doit être **déterministe** : à entrée identique → sortie identique.

***

## 2. Portée V1 (ce qui est supporté)

### 2.1 Constructions supportées

*   `settings { ... }` : bloc de configuration **base**
*   `env "<Name>" { settings { ... } }` : bloc d’override **par environnement**
*   Assignations `Path = Value`
*   Objets via **blocs taggés** (ex. `Server { ... }`, `Logging { ... }`)
*   Tableaux via `[...]` avec **éléments multi-lignes** (virgules optionnelles)
*   Valeurs primitives : `string`, `number`, `bool`, `null`
*   Références de chemin **dans les assignations uniquement** (ex. `Logging.LogLevel.Default = "Debug"`)

### 2.2 Hors scope V1 (prévu V2+)

*   `include`
*   variables `let`
*   expressions (`+`, `??`, comparaisons…)
*   interpolation `"http://${...}"`
*   conditions `if/else`
*   boucles `for`
*   opérateurs `+=`, `:=`
*   validation par schéma / types

***

## 3. Fichiers & conventions

### 3.1 Nom du fichier source

Par convention : `appsettings.settex` ou `*.settex` (extension recommandée).

### 3.2 Emplacement de sortie

La génération produit les fichiers JSON dans un répertoire cible (par défaut le dossier projet) :

*   `appsettings.json`
*   `appsettings.<Env>.json` (ex: `appsettings.Development.json`)

### 3.3 Détection des environnements

Les environnements sont la liste des blocs `env "<Name>" { ... }` présents dans le fichier (après résolution des includes si V2).

***

## 4. Syntaxe (vue d’ensemble)

### 4.1 Base : `settings`

```cfg
settings {
  ApplicationName = "Shop"

  Server {
    Host = "0.0.0.0"
    Port = 8080
  }
}
```

### 4.2 Environnement : `env "<Name>"`

```cfg
env "Development" {
  settings {
    Server.Port = 5000
    Logging.LogLevel.Default = "Debug"
  }
}
```

### 4.3 Blocs “taggés” (objets)

Les objets JSON sont construits **uniquement via des blocs**, pas d’objets inline `{ ... }`.

```cfg
settings {
  Logging {
    LogLevel {
      Default = "Information"
      Microsoft = "Warning"
    }
  }
}
```

### 4.4 Tableaux multi-lignes (virgules optionnelles)

```cfg
settings {
  AllowedHosts = [
    "example.com"
    "api.example.com"
    "localhost"
  ]
}
```

La forme avec virgules est aussi acceptée :

```cfg
settings {
  AllowedHosts = [
    "example.com",
    "api.example.com",
    "localhost"
  ]
}
```

### 4.5 Tableaux d’objets “taggés”

Comme V1 n’a pas d’objets inline, on utilise des blocs taggés comme **valeurs** dans un tableau :

```cfg
settings {
  Services = [
    service {
      Name = "auth"
      Url = "http://localhost:5001"
    }
    service {
      Name = "api"
      Url = "http://localhost:5002"
    }
  ]
}
```

> Les tags (`service`, `item`, etc.) n’ont pas de sémantique particulière en V1 : ils servent à **construire un objet**.

***

## 5. Sémantique (règles d’évaluation & merge)

### 5.1 Construction du JSON de base

Le fichier doit contenir **exactement un** bloc `settings { ... }` au niveau global.

Ce bloc produit `appsettings.json`.

### 5.2 Construction d’un JSON par environnement

Pour chaque `env "<E>"`, on produit `appsettings.<E>.json` comme :

    Final(E) = Merge(BaseSettings, EnvOverlay(E))

où `EnvOverlay(E)` est le contenu du `settings { ... }` à l’intérieur du bloc `env "<E>"`.

### 5.3 Règles de merge (critiques)

Le merge doit être **prévisible** :

*   **Objet + Objet** → merge profond (clé par clé, récursif)
*   **Primitive** (string/number/bool/null) → l’overlay **remplace** la base
*   **Array + Array** → l’overlay **remplace entièrement** la liste (remplacement par défaut)
*   **Type mismatch** (ex. base = objet, overlay = liste) → **erreur** (échec de génération)

### 5.4 Assignation par chemin (dot-path)

`A.B.C = value` est une assignation “profonde” :

*   si `A` n’existe pas, créer un objet `{}` pour `A`
*   si `A` existe mais n’est pas un objet → **erreur**
*   idem pour `B`
*   assigner `C`

Exemple :

```cfg
settings {
  Logging.LogLevel.Default = "Information"
}
```

équivaut à :

```json
{
  "Logging": {
    "LogLevel": { "Default": "Information" }
  }
}
```

***

## 6. Lexique

### 6.1 Mots-clés réservés

*   `settings`
*   `env`
*   `true`, `false`, `null`

### 6.2 Identifiants

*   Regex recommandée : `[A-Za-z_][A-Za-z0-9_]*`

### 6.3 Chaînes

*   Délimitées par `"`
*   Échappements minimaux : `\"`, `\\`, `\n`, `\r`, `\t`

### 6.4 Nombres

*   Entiers : `-?\d+`
*   Décimaux : `-?\d+\.\d+`

### 6.5 Commentaires (option V1, recommandé)

*   `# ...` jusqu’à fin de ligne
*   `// ...` jusqu’à fin de ligne

***

## 7. Grammaire (EBNF V1)

> Objectif : simple à parser (descente récursive / Pratt minimal).

```ebnf
file            := topStmt* EOF ;

topStmt         := settingsBlock
                 | envBlock
                 | comment
                 | ";" ;

settingsBlock    := "settings" block ;

envBlock         := "env" string block ;

block            := "{" stmt* "}" ;

stmt             := assignStmt
                 | nestedBlockStmt
                 | ";" 
                 | comment ;

nestedBlockStmt  := ident block ;              // construit un objet JSON

assignStmt       := path "=" value ;

path             := ident ("." ident)* ;

value            := literal
                 | array
                 | taggedObjectValue ;

taggedObjectValue := ident block ;             // ex: service { ... } est une valeur objet

array            := "[" arrayItems? "]" ;

arrayItems       := arrayItem (arraySep arrayItem)* arraySep? ;
arraySep         := "," | NEWLINE ;            // virgules optionnelles (multi-lignes)

arrayItem        := literal
                 | taggedObjectValue ;

literal          := string | number | "true" | "false" | "null" ;

string           := QUOTED_STRING ;
number           := INT | FLOAT ;
ident            := IDENT ;
comment          := LINE_COMMENT ;
```

### Notes de parsing

*   Les tableaux acceptent :
    *   soit `,` comme séparateur
    *   soit un saut de ligne (`NEWLINE`)
    *   soit un mix (à normaliser)
*   Pour éviter les ambiguïtés, le lexer doit produire des tokens `NEWLINE` (ou le parser doit pouvoir détecter fin de ligne).

***

## 8. Diagnostics (erreurs) — exigences V1

La génération doit échouer (exit code != 0 / MSBuild error) avec des messages utiles, incluant **ligne/colonne**.

### 8.1 Erreurs de structure

*   Absence de bloc `settings`
*   Plusieurs blocs `settings` au niveau global (interdit en V1)
*   Bloc `env "<E>"` sans `settings` interne

### 8.2 Erreurs de syntaxe

*   Token inattendu
*   Accolade/crochet manquant
*   Chaîne non terminée
*   `env` sans string

### 8.3 Erreurs sémantiques

*   Assignation sur un chemin traversant un non-objet (ex. `A` est une liste mais on fait `A.B = ...`)
*   Merge impossible (type mismatch base/overlay)
*   Élément de tableau invalide (en V1 : uniquement littéraux ou objets taggés)

### 8.4 Erreurs d’écriture

*   Impossible d’écrire le fichier de sortie (permissions / path)
*   Nom d’environnement invalide pour le filesystem (à valider ou à normaliser)

***

## 9. Exemples complets (référence V1)

### 9.1 Source unique

```cfg
settings {
  ApplicationName = "Shop"

  Server {
    Host = "0.0.0.0"
    Port = 8080
  }

  AllowedHosts = [
    "localhost"
    "shop.local"
  ]

  Services = [
    service {
      Name = "auth"
      Url = "http://localhost:5001"
    }
    service {
      Name = "api"
      Url = "http://localhost:5002"
    }
  ]
}

env "Development" {
  settings {
    Server.Port = 5000
  }
}

env "Production" {
  settings {
    Server.Port = 80
    AllowedHosts = [
      "shop.com"
      "api.shop.com"
    ]
  }
}
```

### 9.2 Sorties attendues

#### `appsettings.json`

```json
{
  "ApplicationName": "Shop",
  "Server": { "Host": "0.0.0.0", "Port": 8080 },
  "AllowedHosts": ["localhost", "shop.local"],
  "Services": [
    { "Name": "auth", "Url": "http://localhost:5001" },
    { "Name": "api", "Url": "http://localhost:5002" }
  ]
}
```

#### `appsettings.Development.json`

```json
{
  "ApplicationName": "Shop",
  "Server": { "Host": "0.0.0.0", "Port": 5000 },
  "AllowedHosts": ["localhost", "shop.local"],
  "Services": [
    { "Name": "auth", "Url": "http://localhost:5001" },
    { "Name": "api", "Url": "http://localhost:5002" }
  ]
}
```

#### `appsettings.Production.json`

```json
{
  "ApplicationName": "Shop",
  "Server": { "Host": "0.0.0.0", "Port": 80 },
  "AllowedHosts": ["shop.com", "api.shop.com"],
  "Services": [
    { "Name": "auth", "Url": "http://localhost:5001" },
    { "Name": "api", "Url": "http://localhost:5002" }
  ]
}
```

***

## 10. Intégration build (recommandation V1)

V1 cible une génération **sur disque** via :

*   une **MSBuild Task** (NuGet `*.Build`) exécutée avant `Build`
*   (optionnel plus tard) un `dotnet tool` pour usage standalone

### Exigences

*   Ne réécrire les fichiers de sortie **que si le contenu a changé** (évite rebuild infini)
*   Déclarer correctement `Inputs/Outputs` pour l’incrémentalité MSBuild
*   Logs clairs + diagnostics ligne/col

***

## 11. Roadmap V2 (non normative)

*   `let` global/local + scope
*   expressions (arithmétique, `??`)
*   interpolation `${expr}`
*   `include`
*   `if/else`
*   `for` bloc pour générer des listes/objets
*   opérateurs `+=`, `:=`
*   commandes CLI : `build`, `diff`, `fmt`, `lint`

***

## 12. Critères de succès V1 (checklist)

*   [ ] Parse `settings` + `env`
*   [ ] Blocs taggés → objets JSON
*   [ ] Assignations `a.b.c = value`
*   [ ] Tableaux multi-lignes (virgules optionnelles)
*   [ ] Merge profond objets
*   [ ] Listes remplacées
*   [ ] Diagnostics ligne/col
*   [ ] Génération `appsettings*.json` sur disque
*   [ ] Écriture conditionnelle (si contenu identique → pas d’écriture)

***

Si tu veux, je peux aussi te générer (toujours en Markdown) un **README de repo** “prêt à coder” avec :

*   architecture de solution (`Compiler`, `MSBuildTask`, `Tests`)
*   conventions de tests “golden files”
*   et une proposition d’API interne (`Lexer`, `Parser`, `Ast`, `Evaluator`, `Merger`, `JsonWriter`).
