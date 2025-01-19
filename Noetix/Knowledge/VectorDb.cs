namespace Noetix.Knowledge;

public interface IVectorDb
{
    Task<IEnumerable<Document>> Search(string query, int limit);
    Task<IEnumerable<RankedResult>> RankedSearch(string query, int limit);
    Task Insert(IEnumerable<Document> documents);
    Task RecreateTables();
    Task<bool> TableExists();
}