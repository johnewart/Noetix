namespace Noetix.Knowledge;

/// <summary>
/// Store for documents 
/// </summary>
public interface IDocumentStore : IDocumentSource
{
    void Add(Document document);
    void AddAll(IEnumerable<Document> documents);
    Task Reinitialize();
    int Size();
}