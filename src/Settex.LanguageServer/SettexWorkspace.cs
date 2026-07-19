using System.Collections.Concurrent;

namespace Settex.LanguageServer;

/// <summary>
/// Gère tous les documents Settex ouverts dans l'éditeur.
///
/// Les <c>include</c> sont résolus contre les buffers ouverts (y compris non
/// sauvegardés) plutôt que contre le disque, et modifier un fichier inclus
/// ré-analyse les documents qui en dépendent — l'éditeur reste donc cohérent
/// sans attendre une sauvegarde.
/// </summary>
public class SettexWorkspace
{
    private readonly ConcurrentDictionary<string, SettexDocument> documents = new();

    /// <summary>
    /// Ouvre un nouveau document. Retourne le document ouvert suivi des documents
    /// déjà ouverts qui l'incluent et ont été ré-analysés.
    /// </summary>
    public IReadOnlyList<SettexDocument> DidOpen(string uri, string text)
    {
        var document = new SettexDocument(uri, text, this.GetOpenBufferContent);
        this.documents[uri] = document;

        var affected = new List<SettexDocument> { document };
        affected.AddRange(this.RefreshDependents(document.FilePath, uri));
        return affected;
    }

    /// <summary>
    /// Modifie un document existant. Retourne le document modifié suivi de ceux qui
    /// l'incluent et ont été ré-analysés (vide si le document n'est pas ouvert).
    /// </summary>
    public IReadOnlyList<SettexDocument> DidChange(string uri, string newText)
    {
        if (!this.documents.TryGetValue(uri, out var document))
        {
            return Array.Empty<SettexDocument>();
        }

        document.Update(newText);

        var affected = new List<SettexDocument> { document };
        affected.AddRange(this.RefreshDependents(document.FilePath, uri));
        return affected;
    }

    /// <summary>
    /// Ferme un document. Retourne les documents qui l'incluaient et ont été
    /// ré-analysés — leur analyse repasse sur la copie disque.
    /// </summary>
    public IReadOnlyList<SettexDocument> DidClose(string uri)
    {
        if (!this.documents.TryRemove(uri, out var removed))
        {
            return Array.Empty<SettexDocument>();
        }

        return this.RefreshDependents(removed.FilePath, uri);
    }

    /// <summary>
    /// Récupère un document par son URI.
    /// </summary>
    public SettexDocument? GetDocument(string uri)
    {
        this.documents.TryGetValue(uri, out var document);
        return document;
    }

    /// <summary>
    /// Récupère tous les documents ouverts.
    /// </summary>
    public IEnumerable<SettexDocument> GetAllDocuments()
    {
        return this.documents.Values;
    }

    /// <summary>
    /// Ré-analyse les documents ouverts qui dépendent du fichier donné, sans passer par
    /// un changement de buffer. Utilisé quand un <c>.settex</c> change <strong>sur le
    /// disque</strong> (git checkout, autre outil) : le document éventuellement ouvert
    /// sur ce fichier est laissé de côté, son buffer faisant autorité.
    /// </summary>
    public IReadOnlyList<SettexDocument> RefreshDependentsOf(string filePath)
    {
        var openUri = this.documents.Values
            .FirstOrDefault(document => SettexDocument.SamePath(document.FilePath, filePath))?.Uri;

        return this.RefreshDependents(filePath, openUri ?? string.Empty);
    }

    /// <summary>
    /// Contenu d'un fichier s'il est ouvert dans l'éditeur, sinon <c>null</c> pour
    /// que le résolveur retombe sur le disque.
    /// </summary>
    private string? GetOpenBufferContent(string filePath)
    {
        foreach (var document in this.documents.Values)
        {
            if (SettexDocument.SamePath(document.FilePath, filePath))
            {
                return document.Text;
            }
        }

        return null;
    }

    /// <summary>
    /// Ré-analyse les documents ouverts dont l'analyse dépend du fichier donné.
    /// Le jeu d'includes étant transitif, une seule passe suffit.
    /// </summary>
    private List<SettexDocument> RefreshDependents(string? changedFilePath, string changedUri)
    {
        var refreshed = new List<SettexDocument>();

        if (string.IsNullOrEmpty(changedFilePath))
        {
            return refreshed;
        }

        foreach (var (documentUri, document) in this.documents)
        {
            if (documentUri == changedUri)
            {
                continue;
            }

            if (document.Includes.Any(include => SettexDocument.SamePath(include, changedFilePath)))
            {
                document.Refresh();
                refreshed.Add(document);
            }
        }

        return refreshed;
    }
}
