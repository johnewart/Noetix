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

    public void RemoveById(string documentId)
    {
        // This is expensive, we should ideally keep a mapping of IDs to file paths 
        // to avoid scanning the directory. 
        var files = Directory.EnumerateFiles(_storagePath, "*.json");

        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var document = JsonSerializer.Deserialize<Document>(json);
            if (document != null && document.Id == documentId)
            {
                File.Delete(file);
                return; // Exit after deleting the first matching document
            }
        }

        // If we reach here, no document with the specified ID was found.
        throw new KeyNotFoundException($"Document with ID {documentId} not found.");
    }

    public Document? GetById(string documentId)
    {
        var files = Directory.EnumerateFiles(_storagePath, "*.json");

        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var document = JsonSerializer.Deserialize<Document>(json);
            if (document != null && document.Id == documentId)
            {
                return document; // Return the first matching documentment
            }
        }
        
        // If we reach here, no document with the specified ID was found.
        return null;
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