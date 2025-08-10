using System.Text.Json;
using Build5Nines.SharpVector;
using Build5Nines.SharpVector.Data;
using NLog;

namespace Noetix.Knowledge.SharpVector;

public class SharpVector : IDocumentSearchEngine
{
    private readonly BasicMemoryVectorDatabase vdb = new();
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public async Task Index(Document document)
    {        
        var loader = new TextDataLoader<int, string>(vdb);
        await loader.AddDocumentAsync(document.Content, new TextChunkingOptions<string>
        {
            Method = TextChunkingMethod.FixedLength,
            RetrieveMetadata = (chunk) =>
            {
                var metadata = document.Metadata;
                // prune any null values
                metadata = metadata.Where(kv => kv.Key != null && kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value!);
                metadata["Id"] = document.Id;
                metadata["Name"] = document.Name;
                metadata["chunkSize"] = chunk.Length.ToString();
                return JsonSerializer.Serialize(metadata);
            },
            ChunkSize = 128
        });
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

    public async Task<IEnumerable<RankedResult>> RankedSearch(string query, int limit, float? threshold = null)
    {
        _logger.Info($"Searching for documents with query: '{query}'");
        try
        {
            var result = await vdb.SearchAsync(query, pageCount: limit);
            var texts = result.Texts.ToList();
            _logger.Info($"Found {texts.Count} documents");
            return texts.Select(t =>
            {
                var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(t.Metadata);
                return new RankedResult(
                    new Document(id: metadata["Id"], content: t.Text, name: metadata["Name"], metadata: metadata),
                    t.VectorComparison);
            });
        } catch (Exception ex)
        {
            _logger.Error(ex, $"Error during search: {ex.Message}");
            return Enumerable.Empty<RankedResult>();
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
