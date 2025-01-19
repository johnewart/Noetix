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
                metadata["Id"] = document.Id;
                metadata["Name"] = document.Name;
                metadata["chunkSize"] = chunk.Length.ToString();
                return JsonSerializer.Serialize(metadata);
            },
            ChunkSize = 128
        });
    }
    
    public async Task IndexAll(IEnumerable<Document> documents)
    {
        foreach (var document in documents)
        {
            await Index(document);              
        }
    }

    public async Task<IEnumerable<RankedResult>> RankedSearch(string query, int limit, float? threshold = null)
    {
        _logger.Info($"Searching for documents with query: '{query}'");
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
