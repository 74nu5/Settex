# Guide de débogage - Logs du Language Server

## Configuration des logs

Les logs sont maintenant activés dans le Language Server pour le hover overlay tracking.

### Tags de logs ajoutés :
- `[HOVER-OVERLAY]` : Logs dans SettexHoverHandler
- `[FORMATTER]` : Logs principaux dans HoverOverlayFormatter
- `[FORMATTER-EVAL]` : Logs d'évaluation de l'AST
- `[FORMATTER-PATH]` : Logs de navigation dans les paths JSON

## Voir les logs dans VS Code

### 1. Ouvrir la console de sortie
- Menu : **View** > **Output** (ou Ctrl+Shift+U)
- Dans le menu déroulant en haut à droite, sélectionner **Settex Language Server** (ou similaire)

### 2. Activer le niveau de log Debug
Créer/modifier le fichier `.vscode/settings.json` de votre projet :

```json
{
    "settex.trace.server": "verbose"
}
```

### 3. Redémarrer le Language Server
- Ouvrir la **Command Palette** (Ctrl+Shift+P)
- Taper : **Settex: Restart Language Server**

## Informations loggées pour chaque hover

Quand vous survolez un path d'assignation, vous verrez :

1. **[HOVER-OVERLAY]** : Path, environnement, mot sous le curseur
2. **[FORMATTER]** : Début du formatage
3. **[FORMATTER-EVAL]** : Évaluation de l'AST, settings de base et overlays
4. **[FORMATTER-PATH]** : Navigation dans les paths JSON avec détails
5. **[FORMATTER]** : Traitement des overlays et merge
6. **[HOVER-OVERLAY]** : Résultat final (NULL ou longueur)

## Exemple de logs attendus

```
[HOVER-OVERLAY] Formatting assignment for path='Server.Port', envName='Development', word='Port'
[FORMATTER] Starting format for path='Server.Port', currentEnv='Development'
[FORMATTER-EVAL] Starting evaluation of AST
[FORMATTER-EVAL] Evaluation successful. BaseSettings=present, Overlays=2
[FORMATTER] Evaluation successful. BaseSettings=present, Overlays count=2
[FORMATTER-PATH] Getting value at path 'Server.Port' from JsonObject
[FORMATTER-PATH] Path parts: Server, Port
[FORMATTER-PATH] Found property 'Server', type=JsonObject
[FORMATTER-PATH] Found property 'Port', type=JsonValue
[FORMATTER-PATH] Final value found: 80
[FORMATTER] Base value for 'Server.Port': FOUND
[FORMATTER] In environment block, showing all environments
[FORMATTER] Processing 2 environment overlays
[FORMATTER] Processing overlay for env='Development'
[FORMATTER] Merging base with overlay for env='Development'
[FORMATTER] Env 'Development' value for 'Server.Port': 8080
[FORMATTER] Highlighting current environment: Development
[FORMATTER] Processing overlay for env='Production'
[FORMATTER] Merging base with overlay for env='Production'
[FORMATTER] Env 'Production' value for 'Server.Port': 443
[HOVER-OVERLAY] Result: NOT NULL, length=234
```

## En cas de problème

Si vous voyez :
- **`[FORMATTER-EVAL] Evaluation failed`** : Erreur de parsing de l'AST
- **`[FORMATTER-PATH] Property 'X' not found`** : Le path n'existe pas dans les settings
- **`[HOVER-OVERLAY] Result: NULL`** : Le formatter n'a rien retourné

Partagez les logs complets pour diagnostic.
