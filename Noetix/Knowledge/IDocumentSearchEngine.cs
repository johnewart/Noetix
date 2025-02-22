namespace Noetix.Knowledge;

public interface IDocumentSearchEngine
{
    Task<IEnumerable<Document>> Search(string query, int limit);
    Task<IEnumerable<RankedResult>> RankedSearch(string query, int limit, float? threshold = null);
    Task Index(Document document);
    Task<int> IndexAll(IEnumerable<Document> documents);
}