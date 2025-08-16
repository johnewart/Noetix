namespace Noetix.Knowledge;

public interface IDocumentSearchEngine : IDisposable
{
    Task<IEnumerable<Document>> Search(string query, int limit);
    Task<IEnumerable<RankedResult>> RankedSearch(string query, int limit, float? threshold = null);
    Task Index(Document document);
    Task<int> IndexAll(IEnumerable<Document> documents);
    void RemoveDocumentFromIndex(string documentId);
    // May be a no-op if the search engine does not support it or need it
    void WriteToDisk();
    void ResetIndex();
    bool IsInitialized { get; }
    
}