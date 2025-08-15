using NLog;
using ServiceStack;

namespace Noetix.Knowledge;

public class Knowledgebase(
    IDocumentStore documentStore,
    IDocumentSearchEngine searchEngine)
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    

    public async Task ReindexSearchEngine()
    {
        _logger.Info("Reindexing knowledgebase");
       foreach(var document in documentStore.Documents())
        {
            _logger.Info($"Reindexing document: {document.Id}");
            await searchEngine.Index(document);
        }
        _logger.Info("Reindexing complete");
    }
    
    public async Task<IEnumerable<Document>> Search(string query, int limit = 5)
    {
        _logger.Info($"Searching for documents with query: {query}");
        return await searchEngine.Search(query, limit);
    }

   
    
    public async Task Rebuild(List<IDocumentSource> sources, int? chunkSize = 1024)
    {
        _logger.Info("Rebuilding knowledgebase from  {sources.Count} sources with chunk size: {chunkSize}", sources.Count, chunkSize);
        await documentStore.Reinitialize();
        _logger.Info("Reinitialized document store, will re-add all documents");
        foreach (var source in sources)
        {
            _logger.Info($"Adding documents from {source}");
            AddAll(source.Documents(), chunkSize);
        }
    }

    private void AddAll(IEnumerable<Document> documents, int? chunkSize = 1024)
    {
        var docsInserted = 0;
        var chunksInserted = 0;
        _logger.Info($"Adding documents to knowledgebase with chunk size: {chunkSize}");
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

    public async Task<IEnumerable<RankedResult>> RankedSearch(string query, int limit, double? threshold = null)
    {
        _logger.Info($"Performing ranked-search for documents with query: {query} and limit: {limit} with threshold: {threshold}");
        return await searchEngine.RankedSearch(query, limit, (float?)threshold);
    }

    public int Size()
    {
        return documentStore.Size();
    }

    public IEnumerable<Document> Documents()
    {
        return documentStore.Documents();
    }

    public void RemoveDocument(string documentId)
    {
        _logger.Info($"Removing document with ID: {documentId}");
        var document = documentStore.GetById(documentId);
        if (document != null)
        {
            documentStore.RemoveById(documentId);
            // Remove from search engine, if applicable - this may be a noop if the search engine
            // is built into the document store, is not tracking documents, or if it handles removals automatically
            searchEngine.RemoveDocumentFromIndex(documentId);
            _logger.Info($"Document with ID: {documentId} removed successfully");
        }
        else
        {
            _logger.Warn($"Document with ID: {documentId} not found");
        }
    }

    public void SyncToDisk()
    {
        _logger.Info("Syncing knowledgebase to disk");
        try
        {
            searchEngine.WriteToDisk();
        } catch (Exception ex)
        {
            _logger.Error(ex, "Failed to sync knowledgebase to disk");
            throw;
        }
    }

    public async Task StoreAsync(List<Document> documents)
    {
        
    }
}