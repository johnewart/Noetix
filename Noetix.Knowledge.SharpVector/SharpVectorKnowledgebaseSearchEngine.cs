using System.Text.Json;
using Build5Nines.SharpVector;
using Build5Nines.SharpVector.Data;
using NLog;

namespace Noetix.Knowledge.SharpVector;

public class SharpVectorKnowledgebaseSearchEngine : IDocumentSearchEngine
{
    private Dictionary<string, List<int>> _documentIdToIndexIds = new();

    private readonly BasicMemoryVectorDatabase vdb = new();
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private int _chunkSizeInWords;
    private int _overlapSizeInWords;
    private string? _vectorDatabasePath;
    private Mutex _vectorDatabaseMutex = new Mutex(false, "SharpVectorKnowledgebaseSearchEngineMutex");

    public bool LoadedFromFile { get; private set; } = false;

    public SharpVectorKnowledgebaseSearchEngine(int chunkSizeInWords = 512, int overlapSizeInWords = 40,
        string? vectorDatabasePath = null)
    {
        _chunkSizeInWords = chunkSizeInWords;
        _overlapSizeInWords = overlapSizeInWords;
        _vectorDatabasePath = vectorDatabasePath;

        if (vectorDatabasePath != null)
        {
            if (File.Exists(vectorDatabasePath))
            {
                _logger.Info($"Loading vector database from path: {vectorDatabasePath}");
                try
                {
                    vdb.LoadFromFile(vectorDatabasePath);
                    LoadedFromFile = true;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error loading vector database from path: {vectorDatabasePath} - {ex.Message}");
                    // If loading fails, we will create a new in-memory database
                    LoadedFromFile = false;
                }
            }
            else
            {
                _logger.Warn(
                    $"Specified vector database path does not exist: {vectorDatabasePath}. A new database will be created in memory only.");
            }
        }
        
    }

    public void WriteToDisk()
    {
        if (_vectorDatabasePath != null)
        {
            _logger.Info($"Saving vector database to path: {_vectorDatabasePath}");
            try
            {
                vdb.SaveToFile(_vectorDatabasePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error saving vector database to path: {_vectorDatabasePath} - {ex.Message}");
            }
        }
    }


    public async Task Index(Document document)
    {
        _logger.Trace(
            $"Indexing document with ID: {document.Id}, Name: {document.Name}, Content Length: {document.Content.Length}");
        var indexTimer = new System.Diagnostics.Stopwatch();
        indexTimer.Start();

        var loader = new TextDataLoader<int, string>(vdb);
        var ids = (await loader.AddDocumentAsync(document.Content, new TextChunkingOptions<string>
        {
            Method = TextChunkingMethod.OverlappingWindow,
            ChunkSize = _chunkSizeInWords,
            // Number of words to overlap text chunks
            OverlapSize = _overlapSizeInWords,
            RetrieveMetadata = (chunk) => 
            {
                var metadata = document.Metadata;
                // prune any null values
                metadata = metadata.Where(kv => kv.Key != null && kv.Value != null)
                    .ToDictionary(kv => kv.Key, kv => kv.Value!);
                metadata["Id"] = document.Id;
                metadata["Name"] = document.Name;
                metadata["chunkSize"] = chunk.Length.ToString();
                return JsonSerializer.Serialize(metadata);
            }
        })).ToList();

        _documentIdToIndexIds[document.Id] = ids;

        indexTimer.Stop();
        _logger.Trace(
            $"Indexed document with ID: {document.Id} in {indexTimer.ElapsedMilliseconds} ms, {ids.Count} chunks created");
    }

    public async Task<int> IndexAll(IEnumerable<Document> documents)
    {
        int count = 0;
        foreach (var document in documents)
        {
            count++;
            await Index(document);
        }

        return count;
    }

    public void RemoveDocumentFromIndex(string documentId)
    {
        _logger.Trace($"Removing document with ID: {documentId} from index");
        try
        {
            if (!_documentIdToIndexIds.TryGetValue(documentId, out var indexIds))
            {
                _logger.Warn($"Document with ID: {documentId} not found in index");
                return;
            }


            var removedChunks = 0;
            foreach (var indexId in indexIds)
            {
                try
                {
                    vdb.DeleteText(indexId);
                    removedChunks++;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex,
                        $"Error removing document with ID: {documentId} and index ID: {indexId} - {ex.Message}");
                }
            }

            _documentIdToIndexIds.Remove(documentId);
            _logger.Info($"Removed {removedChunks} chunks for document with ID: {documentId}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Error removing document with ID: {documentId} - {ex.Message}");
        }
    }

    public async Task<IEnumerable<RankedResult>> RankedSearch(string query, int limit, float? threshold = null)
    {
        _logger.Info($"Searching for documents with query: '{query}' and threshold: {threshold}, limit: {limit}");
        var searchVectorTimer = new System.Diagnostics.Stopwatch();
        searchVectorTimer.Start();
        try
        {
            var result = await vdb.SearchAsync(query, threshold: threshold, pageCount: limit);
            searchVectorTimer.Stop();
            _logger.Info($"Vector search completed in {searchVectorTimer.ElapsedMilliseconds} ms");
            var texts = result.Texts.ToList();
            _logger.Info($"Found {texts.Count} results for query: '{query}'");

            return texts.Select(t =>
            {
                var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(t.Metadata);
                return new RankedResult(
                    new Document(id: metadata["Id"], content: t.Text, name: metadata["Name"], metadata: metadata),
                    t.VectorComparison);
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Error during search: {ex.Message}");
            return Enumerable.Empty<RankedResult>();
        }
        finally
        {
            searchVectorTimer.Stop();
        }
    }

    public Task RecreateTables()
    {
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<Document>> Search(string query, int limit)
    {
        return (await RankedSearch(query, limit)).Select(r => r.Document);
    }

    public async Task<bool> TableExists()
    {
        return true;
    }
}