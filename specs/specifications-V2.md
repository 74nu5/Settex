# Settex — Spécification V2 (inline if + set-if-missing)

## 1) Objectif & principes

### Objectif

Créer un langage de config “SASS des appsettings” :

*   **1 fichier source** expressif
*   génération **sur disque** de :
    *   `appsettings.json`
    *   `appsettings.<Env>.json` (pour chaque environnement)

### Principes

*   **Déterministe** : pas de `now()`, random, accès réseau, I/O implicite.
*   **Build-time** : compilation via **MSBuild Task** / tool ; pas d’injection runtime.
*   **Prévisible** :
    *   objets : merge profond
    *   primitives : override
    *   listes : **remplacement** (par défaut)
*   **SASS-like** :
    *   `include`, `let`, expressions, interpolation, `for`, `if inline`

***

## 2) Portée V2

### 2.1 Supporté

V2 inclut tout V1 + :

*   `include "<path>"`
*   variables `let` (globales + locales `env` + scopes locaux)
*   expressions : `+ - * /`, comparaisons, logique, `??`
*   interpolation string `"http://${host}:${port}"`
*   `for` **en bloc** (dans les tableaux)
*   `if` **inline** (sur assignations ; optionnel sur éléments de tableau)
*   opérateur **set-if-missing** : `:=`

### 2.2 Hors scope V2 (V3+)

*   `if/else` en bloc
*   `+=` append liste
*   macros/mixins
*   validation avancée / schémas

***

## 3) Structure globale & sorties

### 3.1 Structure attendue

*   **au moins un** `settings { ... }` global. Plusieurs blocs sont autorisés et fusionnent en profondeur dans l'ordre du document, ce qui est ce qui permet à un fichier inclus d'en contribuer un.
*   0..N `env "<Name>" { ... }`
*   `include` et `let` autorisés au global et dans `env`

### 3.2 Sorties

*   base : `appsettings.json`
*   par env : `appsettings.<Env>.json` pour chaque `env "<Env>"`

***

## 4) Syntaxe & sémantique — V2

## 4.1 `include`

```cfg
include "./common.settex"
include "./logging.settex"
```

**Règles**

*   chemin relatif au fichier courant (recommandé)
*   inclusion “comme si copié/collé” à cet endroit (AST concat)
*   cycle d’include = **erreur**

***

## 4.2 Variables `let` & scopes

### 4.2.1 Déclaration

```cfg
let basePort = 5000
let host = "localhost"
```

### 4.2.2 Scopes (lexical)

*   global : visible partout
*   `env` : masque le global pour cet env
*   scopes locaux :
    *   dans un `for` (corps du for)
    *   (V2 n’a pas de `if {}` bloc, donc pas de scope if-bloc)

### 4.2.3 Règle recommandée (simplicité)

*   un `let` doit être défini **avant usage** dans le même scope (pas de hoisting)

***

## 4.3 Expressions

### 4.3.1 Opérateurs

*   arithmétique : `+ - * /`
*   comparaison : `== != < <= > >=`
*   logique : `and or not`
*   coalesce : `a ?? b` (retourne `b` si `a` est `null`)

### 4.3.2 Types runtime

*   `string`, `number` (recommandé : `decimal` interne), `bool`, `null`, `array`, `object`

### 4.3.3 Typage & erreurs

*   opérateur utilisé sur types incompatibles => **erreur**
*   condition d’un `if inline` doit être bool => **erreur**

***

## 4.4 Interpolation `"${expr}"`

```cfg
let host = "localhost"
let port = 5001

settings {
  Service.Url = "http://${host}:${port}"
}
```

**Règles**

*   `${expr}` peut apparaître plusieurs fois
*   si `expr` vaut `null` → recommandé V2 : **erreur** (plus sûr)

***

## 4.5 Assignations & `if` inline (V2)

### 4.5.1 Assignation simple

```cfg
settings {
  Server.Port = 8080
}
```

### 4.5.2 Assignation conditionnelle (inline)

Syntaxe :

```cfg
Path = Expr if Condition
```

Exemple :

```cfg
settings {
  Logging.LogLevel.Default = "Debug" if env == "Development"
  Logging.LogLevel.Default = "Information" if env != "Development"
}
```

**Sémantique**

*   Si `Condition` est `true` → appliquer l’assignation
*   Si `false` → ne rien faire (pas de clé créée/modifiée)
*   Si `Condition` n’est pas bool → **erreur**

> **Note** : comme on génère plusieurs sorties, `env` est une variable implicite (voir §6).

***

## 4.6 `set-if-missing` (opérateur `:=`) — V2

### 4.6.1 Intention

Définir une valeur **seulement si** la clé (chemin) n’existe pas déjà.

Syntaxe :

```cfg
Path := Expr
Path := Expr if Condition
```

### 4.6.2 Exemple (valeur par défaut)

```cfg
settings {
  Logging.LogLevel.Default := "Information"
  Logging.LogLevel.Microsoft := "Warning"
}
```

Si plus haut (ou via include) tu as déjà :

```cfg
Logging.LogLevel.Default = "Debug"
```

alors `:=` ne remplace pas.

### 4.6.3 Définition de “missing”

Une clé est **missing** si :

*   elle n’existe pas dans l’objet courant (pas présente)

Recommandation V2 :

*   `null` est considéré **présent** (donc `:=` ne remplace pas un `null`).  
    *(Tu peux choisir l’inverse, mais il faut figer et tester.)*

### 4.6.4 Règle spéciale en environnement (important)

Pour que `:=` soit utile avec le merge base/env :

*   Dans `settings` **base** : `:=` teste uniquement “déjà présent dans base en construction”.
*   Dans `settings` d’un `env` : `:=` teste :
    1.  “déjà présent dans l’overlay en construction”
    2.  **et** “déjà présent dans la base”  
        → donc `:=` signifie “set uniquement si la config finale n’a pas déjà cette clé”.

***

## 4.7 Blocs taggés (objets) — toujours

### 4.7.1 Dans un objet JSON

```cfg
settings {
  Server {
    Host = "0.0.0.0"
    Port = 8080
  }
}
```

### 4.7.2 Comme valeur (dans un tableau ou `let`)

```cfg
let svc = service {
  name = "auth"
  port = 5001
}
```

> Les tags (`service`, `item`, `Server`, etc.) servent à construire un objet ; le tag n’est pas sérialisé en JSON.

***

## 4.8 Tableaux multi-lignes (virgules optionnelles)

### 4.8.1 Primitives

```cfg
settings {
  AllowedHosts = [
    "localhost"
    "shop.local"
  ]
}
```

### 4.8.2 Objets (taggés)

```cfg
settings {
  Services = [
    service { Name = "auth" Url = "http://localhost:5001" }
    service { Name = "api"  Url = "http://localhost:5002" }
  ]
}
```

> Rappel : “blocs uniquement” ⇒ `service { ... }` est une **valeur objet**.

### 4.8.3 `if inline` sur éléments de tableau (option V2, recommandé)

Pour éviter V3 trop tôt, V2 peut autoriser :

```cfg
AllowedHosts = [
  "localhost"
  "shop.local" if env != "Production"
  "shop.com"   if env == "Production"
]
```

**Sémantique**

*   si condition false → élément non ajouté
*   condition doit être bool, sinon erreur

***

## 4.9 `for` en bloc (dans tableaux) — V2

### 4.9.1 Exemple canonique

```cfg
let services = [
  service { name = "auth" port = 5001 }
  service { name = "api"  port = 5002 }
]

settings {
  Services = [
    for s in services {
      item {
        Name = s.name
        Url  = "http://localhost:${s.port}"
      }
    }
  ]
}
```

### 4.9.2 Règles

*   l’expression après `in` doit être un `array`, sinon erreur
*   le corps du `for` peut produire :
    *   `item { ... }` (valeur objet)
    *   et/ou des littéraux (si tu l’autorises)
*   les assignations `A.B = ...` directement dans un `for` sont **interdites** (ambiguës pour une liste)

***

## 5) Chemins & création implicite d’objets

### 5.1 Assignation `A.B.C = v`

*   crée `A` et `B` en tant qu’objets si absents
*   si `A` existe mais n’est pas un objet → erreur
*   si `B` existe mais n’est pas un objet → erreur

***

## 6) Environnements & merge

### 6.1 Variable implicite `env`

Lors de la génération :

*   pour `appsettings.json` : `env = "Base"` (valeur fixe)
*   pour `env "Development"` : `env = "Development"`

### 6.2 Évaluation

1.  parser fichier + includes → AST
2.  évaluer `let` globaux → contexte global
3.  évaluer `settings` global → `BaseSettings`
4.  pour chaque env `E` :
    *   créer contexte env (copie du global)
    *   définir `env = E`
    *   évaluer `let` locaux de `env`
    *   évaluer `settings` interne → `Overlay(E)`
    *   appliquer `Final(E) = Merge(BaseSettings, Overlay(E))`
5.  écrire les JSON sur disque (only-if-changed)

### 6.3 Règles de merge (inchangées)

*   objet+objet : deep merge
*   primitive : override
*   liste : override complet (remplacement)
*   mismatch : erreur

***

## 7) Diagnostics (erreurs) V2

### 7.1 Structure

*   pas de `settings` global
*   plusieurs `settings` globaux
*   `env "<E>"` sans `settings` interne

### 7.2 Include

*   fichier introuvable
*   cycle d’include

### 7.3 Évaluation

*   variable inconnue
*   opérateur incompatible (types)
*   `if inline` condition non-bool
*   `for` sur non-array
*   accès `s.name` alors que `s` n’est pas un objet
*   interpolation `${expr}` renvoie `null` (si choisi comme erreur)

### 7.4 Merge / chemins

*   type mismatch base/overlay
*   assignation traverse un non-objet (ex: `A` est liste et on fait `A.B = 1`)

### 7.5 Format conseillé

`file(line,col): error <PREFIX>###: message`

***

## 8) Grammaire EBNF V2 (inline if + := + for/let/include)

> Parser recommandé : descente récursive + Pratt pour les expressions.

```ebnf
file             := topStmt* EOF ;

topStmt          := includeStmt
                  | letStmt
                  | settingsBlock
                  | envBlock
                  | comment
                  | ";" ;

includeStmt       := "include" string ;

letStmt           := "let" ident "=" expr ;

settingsBlock     := "settings" block ;

envBlock          := "env" string block ;

block             := "{" blockStmt* "}" ;

blockStmt         := includeStmt
                  | letStmt
                  | assignStmt
                  | nestedObjectBlock
                  | ";" 
                  | comment ;

nestedObjectBlock := ident block ;                (* objet JSON dans un objet *)

assignStmt        := path assignOp expr inlineIf? ;

assignOp          := "=" | ":=" ;                 (* set-if-missing *)

inlineIf          := "if" expr ;                  (* condition bool *)

path              := ident ("." ident)* ;

(* Expressions *)
expr              := logicalOr ;

logicalOr         := logicalAnd ( "or" logicalAnd )* ;
logicalAnd        := coalesce ( "and" coalesce )* ;
coalesce          := equality ( "??" equality )* ;
equality          := comparison ( ( "==" | "!=" ) comparison )* ;
comparison        := term ( ( "<" | "<=" | ">" | ">=" ) term )* ;
term              := factor ( ( "+" | "-" ) factor )* ;
factor            := unary ( ( "*" | "/" ) unary )* ;
unary             := ( "not" | "-" ) unary | primary ;

primary           := literal
                  | interpolatedString
                  | array
                  | taggedObjectValue
                  | pathRef
                  | "(" expr ")" ;

pathRef           := ident ("." ident)* ;

taggedObjectValue := ident block ;                (* valeur objet *)

array             := "[" arrayElem* "]" ;

arrayElem         := ( arrayValue inlineIf? | forStmt ) arraySep? ;

arrayValue        := literal
                  | interpolatedString
                  | taggedObjectValue
                  | pathRef ;

forStmt           := "for" ident "in" expr block ;

arraySep          := "," | NEWLINE ;

literal           := string | number | "true" | "false" | "null" ;
```

***

## 9) Exemple complet V2 (référence)

```cfg
include "./common.settex"

let basePort = 5000
let host = "localhost"

let services = [
  service { name = "auth" port = basePort + 1 }
  service { name = "api"  port = basePort + 2 }
]

settings {
  ApplicationName = "Shop"

  Server {
    Host = "0.0.0.0"
    Port := 8080
  }

  Logging {
    LogLevel {
      Default := "Information"
      Microsoft := "Warning"
    }
  }

  Logging.LogLevel.Default = "Debug" if env == "Development"
  Logging.LogLevel.Default = "Warning" if env == "Production"

  AllowedHosts = [
    "localhost"
    "${host}" if env != "Production"
    "shop.com" if env == "Production"
  ]

  Services = [
    for s in services {
      item {
        Name = s.name
        Url  = "http://${host}:${s.port}"
      }
    }
  ]
}

env "Development" {
  let basePort = 4000

  settings {
    Server.Port = basePort
    FeatureFlags.NewCheckout := true
  }
}

env "Production" {
  settings {
    Server.Port = 80
    FeatureFlags.NewCheckout := false

    Services = [
      for s in services {
        item {
          Name = s.name
          Url  = "https://${s.name}.mycompany.com"
        }
      }
    ]
  }
}
```

***

## 10) Checklist DoD V2

*   [ ] include + cycle detection
*   [ ] let global + env + scope for
*   [ ] Pratt parser expressions + bool logic + ??
*   [ ] interpolation `${expr}` (null => erreur)
*   [ ] `if` inline sur assignations (+ optionnel sur array elements)
*   [ ] `:=` set-if-missing (avec règle spéciale env: consulte base)
*   [ ] `for` bloc dans arrays (produit items)
*   [ ] diagnostics ligne/col + codes
*   [ ] golden tests : include + let + if inline + := + for + interpolation
*   [ ] MSBuild task : write-only-if-changed

