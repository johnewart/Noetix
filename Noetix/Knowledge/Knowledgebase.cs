using NLog;
using ServiceStack;

namespace Noetix.Knowledge;

public class Knowledgebase(
    IDocumentStore documentStore,
    IDocumentSearchEngine searchEngine,
    int? chunkSize = null)
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    
    public Task<IEnumerable<Document>> Search(string query, int limit = 5)
    {
        _logger.Info($"Searching for documents with query: {query}");
        return searchEngine.Search(query, limit);
    }

    public async Task Rebuild(List<IDocumentSource> sources)
    {
        _logger.Info("Rebuilding knowledgebase");
        await documentStore.Reinitialize();
        _logger.Info("Reinitialized document store, will re-add all documents");
        foreach (var source in sources)
        {
            _logger.Info($"Adding documents from {source}");
            AddAll(source.Documents());
        }
    }

    private void AddAll(IEnumerable<Document> documents)
    {
        var docsInserted = 0;
        var chunksInserted = 0;
        _logger.Info($"Adding documents to knowledgebase");
        var docList = documents.ToList();
        docList.Each(doc =>
        {
            if (chunkSize.HasValue)
            {
                var chunkList = doc.Chunks(chunkSize.Value).ToList();
                chunkList.Each(chunk =>
                {
                    documentStore.Add(chunk);
                    searchEngine.Index(chunk);
                    chunksInserted++;
                });
            }
            else
            {
                documentStore.Add(doc);
                searchEngine.Index(doc);
            }

            docsInserted++;
        });
        _logger.Info($"Inserted {docsInserted} documents");
        if (chunksInserted > 0)
            _logger.Info($"Chunked documents into {chunksInserted} documents");
    }

    public void Store(IEnumerable<Document> kbDocs)
    {
        kbDocs.Each(doc =>
        {
            documentStore.Add(doc);
            searchEngine.Index(doc);
        });
    }

    public async Task<IEnumerable<RankedResult>> RankedSearch(string query, int limit)
    {
        _logger.Info($"Performing ranked-search for documents with query: {query}");
        return await searchEngine.RankedSearch(query, limit);
    }

    public int Size()
    {
        return documentStore.Size();
    }

    public IEnumerable<Document> Documents()
    {
        return documentStore.Documents();
    }
}