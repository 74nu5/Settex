using System.Collections.Concurrent;

namespace Settex.LanguageServer;

/// <summary>
/// Gère tous les documents Settex ouverts dans l'éditeur.
/// </summary>
public class SettexWorkspace
{
    private readonly ConcurrentDictionary<string, SettexDocument> documents = new();

    /// <summary>
    /// Ouvre un nouveau document.
    /// </summary>
    public SettexDocument DidOpen(string uri, string text)
    {
        var document = new SettexDocument(uri, text);
        this.documents[uri] = document;
        return document;
    }

    /// <summary>
    /// Modifie un document existant.
    /// </summary>
    public SettexDocument? DidChange(string uri, string newText)
    {
        if (this.documents.TryGetValue(uri, out var document))
        {
            document.Update(newText);
            return document;
        }
        return null;
    }

    /// <summary>
    /// Ferme un document.
    /// </summary>
    public void DidClose(string uri)
    {
        this.documents.TryRemove(uri, out _);
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
}
