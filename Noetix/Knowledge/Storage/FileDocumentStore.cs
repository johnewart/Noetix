namespace Noetix.Knowledge.Storage;

using System.Text.Json;

public class FileDocumentStore : IDocumentStore
{
    private readonly string _storagePath;

    public FileDocumentStore(string storagePath)
    {
        _storagePath = storagePath;
        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    public void Add(Document document)
    {
        var filePath = Path.Combine(_storagePath, $"{document.Id}.json");
        var json = JsonSerializer.Serialize(document);
        File.WriteAllText(filePath, json);
    }

    public void AddAll(IEnumerable<Document> documents)
    {
        foreach (var document in documents)
        {
            Add(document);
        }
    }

    public Task Reinitialize()
    {
        if (Directory.Exists(_storagePath))
        {
            Directory.Delete(_storagePath, true);
        }

        Directory.CreateDirectory(_storagePath);
        return Task.CompletedTask;
    }

    public int Size()
    {
        return Directory.EnumerateFiles(_storagePath, "*.json").Count();
    }

    public IEnumerable<Document> Documents()
    {
        var documents = new List<Document>();
        var files = Directory.EnumerateFiles(_storagePath, "*.json");

        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var document = JsonSerializer.Deserialize<Document>(json);
            if (document != null)
            {
                documents.Add(document);
            }
        }

        return documents;
    }

    public void Store(Document document)
    {
        Add(document);
    }
}