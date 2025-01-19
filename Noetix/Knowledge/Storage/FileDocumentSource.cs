using NLog;

namespace Noetix.Knowledge.Storage;

public class FileDocumentSource(string rootPath, string glob, List<string>? exclude = null, int? chunkSize = null)
    : IDocumentSource
{
    private readonly List<string> _exclude = exclude ?? new List<string>();
    private readonly Logger logger = LogManager.GetCurrentClassLogger();

    public IEnumerable<Document> Documents()
    {
        logger.Info($"Loading files from {rootPath} with glob {glob}");
        if (_exclude.Any())
        {
            logger.Info($"Excluding files with names containing: {string.Join(", ", _exclude)}");
        }

        var documents = new List<Document>();
        var files = Directory.EnumerateFiles(rootPath, glob, SearchOption.AllDirectories).ToList();

        int excludedCount = 0;
        foreach (var file in files)
        {
            if (_exclude.Any(ex => file.Contains(ex)))
            {
                logger.Info($"Excluding file: {file}");
                excludedCount++;
            }
            else
            {
                var contents = File.ReadAllText(file);
                var document = new Document(contents, file, Guid.NewGuid().ToString());

                if (chunkSize.HasValue)
                {
                    documents.AddRange(document.Chunks(chunkSize.Value));
                }
                else
                {
                    documents.Add(document);
                }
            }
        }

        logger.Info($"Excluded {excludedCount} files due to exclusion criteria");
        logger.Info($"Loaded {files.Count} files as {documents.Count} documents");
        return documents;
    }

    public void Store(Document document)
    {
        var filename = Path.Combine(rootPath, document.Id);
        File.WriteAllText(filename, document.Content);
    }
}