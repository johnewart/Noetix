using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json;
using NLog;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Serializers;
using ZoneTree.FullTextSearch.SearchEngines;
using ZoneTree.FullTextSearch;
using ZoneTree.FullTextSearch.Hashing;
using ZoneTree.FullTextSearch.Normalizers;

namespace Noetix.Knowledge.ZoneTree;

public class ZoneTreeDocumentSearchEngine : IDocumentSearchEngine, IDisposable
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private HashedSearchEngine<int> _searchEngine;
    private RecordTable<int, Document> _documentTable;
    private object _lock = new object();
    private readonly string _documentTablePath;
    private readonly string _searchEnginePath;
    private bool _isInitialized;

    private Dictionary<int, string> _documentIdMap = new Dictionary<int, string>();

    public ZoneTreeDocumentSearchEngine(string storagePath)
    {
        _searchEnginePath = Path.Combine(storagePath, "searchEngine");
        _documentTablePath = Path.Combine(storagePath, "documentTable");
        _logger.Info($"Initializing ZoneTreeDocumentSearchEngine with storage path: {storagePath}");

        var searchEngineDirExists = Directory.Exists(_searchEnginePath);
        var documentTableDirExists = Directory.Exists(_documentTablePath);

        _isInitialized = searchEngineDirExists && documentTableDirExists;

        if (!_isInitialized)
        {
            _logger.Warn("Search engine or document table directories do not exist, initializing new ones.");

            if ((searchEngineDirExists && !documentTableDirExists) ||
                (!searchEngineDirExists && documentTableDirExists))
            {
                // If one exists but not the other, we need to create both
                _logger.Warn(
                    "One of the directories exists but not the other, deleting and re-creating both directories.");

                RemoveFiles();
            }

            Directory.CreateDirectory(_searchEnginePath);
            Directory.CreateDirectory(_documentTablePath);
        }

        var hashCodeGenerator = new StemmingHashCodeGenerator(new Porter2Stemmer());

        _searchEngine = new HashedSearchEngine<int>(
            dataPath: _searchEnginePath,
            useSecondaryIndex: false,
            hashCodeGenerator: hashCodeGenerator
            );

        _documentTable = new RecordTable<int, Document>(
            dataPath: _documentTablePath,
            factory1: (ztf =>
            {
                ztf.SetKeySerializer(new Int32Serializer());
                ztf.SetValueSerializer(new DocumentSerializer());
                ztf.SetComparer(new Int32ComparerAscending());
            }),
            factory2: (ztf =>
            {
                ztf.SetKeySerializer(new DocumentSerializer());
                ztf.SetValueSerializer(new Int32Serializer());
                ztf.SetComparer(new DocumentComparer());
            })
        );
    }

    public async Task<IEnumerable<Document>> Search(string query, int limit)
    {
        var result = await Task.Run(() => _searchEngine.Search(query, limit: limit));
        _logger.Debug($"Found {result.Length} results for {query}");
        var documents = new List<Document>();
        foreach (var id in result)
        {
            _logger.Debug($"Found document with ID: {id}");
            if (_documentTable.TryGetValue(id, out var document))
            {
                documents.Add(document);
            }
        }

        return documents;
    }

    public async Task<IEnumerable<RankedResult>> RankedSearch(string query, int limit, float? threshold = null)
    {
        var results = await Search(query, limit);
        return results.Select(r => new RankedResult(r, 1.0))
            .OrderByDescending(r => r.Score)
            .Take(limit)
            .ToList();
    }

    public async Task Index(Document document)
    {
        await IndexAll(new[] { document });
    }

    public async Task<int> IndexAll(IEnumerable<Document> documents)
    {
        var addedCount = 0;
        await Task.Run(() =>
        {
            foreach (var doc in documents)
            {
                try
                {
                    MD5 md5Hasher = MD5.Create();
                    var hashed = md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(doc.Id));
                    var id = BitConverter.ToInt32(hashed, 0);

                    _searchEngine.AddRecord(id, doc.CleanedContent);
                    _documentTable.UpsertRecord(id, doc);
                    _documentIdMap[id] = doc.Id;
                    addedCount++;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Error indexing document {doc.Id}: {ex.Message}");
                }
            }
        });

        return addedCount;
    }

    public void RemoveDocumentFromIndex(string documentId)
    {
        lock (_lock)
        {
            var idToRemove = _documentIdMap.FirstOrDefault(kvp => kvp.Value == documentId).Key;
            if (idToRemove != 0)
            {
                _searchEngine.DeleteRecord(idToRemove);
                // Remove from document table by upserting an empty document
                // This ensures the record is removed without leaving data around
                _documentTable.UpsertRecord(idToRemove,
                    new Document("", "", "", new() { }, Array.Empty<double>(), new() { }));
                _documentIdMap.Remove(idToRemove);
            }
            else
            {
                _logger.Warn($"Document with ID {documentId} not found in index.");
            }
        }
    }

    public void WriteToDisk()
    {
    }

    public void ResetIndex()
    {
        _logger.Info("Resetting index");
        _searchEngine.Drop();
        _documentTable.Drop();
    }

    private void RemoveFiles()
    {
        _logger.Debug($"Resetting index and document table at {_searchEnginePath} and {_documentTablePath}");
        // Remove all files in those directories that we know we own
        foreach (var entry in Directory.EnumerateFiles(_searchEnginePath, "index*"))
        {
            if (Directory.Exists(entry))
            {
                _logger.Info($"Removing index directory {entry}");
                Directory.Delete(entry, true);
            }
        }

        foreach (var entry in Directory.EnumerateFiles(_documentTablePath, "rectable*"))
        {
            if (Directory.Exists(entry))
            {
                _logger.Info($"Removing document table directory {entry}");
                Directory.Delete(entry, true);
            }
        }
    }

    public bool IsInitialized
    {
        get { return _isInitialized; }
    }

    public void Dispose()
    {
        _searchEngine?.Dispose();
        _documentTable?.Dispose();
    }
}

class DocumentSerializer : ISerializer<Document>
{
    public Document Deserialize(Memory<byte> bytes)
    {
        var jsonContent = Encoding.UTF8.GetString(bytes.Span);
        return JsonConvert.DeserializeObject<Document>(jsonContent) ??
               throw new JsonException("Failed to deserialize document");
    }

    public Memory<byte> Serialize(in Document entry)
    {
        var jsonContent = JsonConvert.SerializeObject(entry);
        return new Memory<byte>(Encoding.UTF8.GetBytes(jsonContent));
    }
}

class DocumentKeySerializer : ISerializer<int>
{
    public int Deserialize(Memory<byte> bytes)
    {
        return BitConverter.ToInt32(bytes.Span.ToArray(), 0);
    }


    public Memory<byte> Serialize(in int entry)
    {
        return new Memory<byte>(BitConverter.GetBytes(entry));
    }
}

class DocumentComparer : IRefComparer<Document>
{
    public int Compare(in Document x, in Document y)
    {
        return String.Compare(x.Id, y.Id, StringComparison.Ordinal);
    }
}

public sealed class StemmingHashCodeGenerator : IHashCodeGenerator
{
    private readonly Porter2Stemmer _stemmer;
    private readonly bool _isCaseSensitive;

    public StemmingHashCodeGenerator(Porter2Stemmer stemmer, bool isCaseSensitive = false)
    {
        _stemmer = stemmer;
        _isCaseSensitive = isCaseSensitive;
    }

    public ulong GetHashCode(ReadOnlySpan<char> text)
    {
        return GetHashCode(text.ToString());
    }

    public ulong GetHashCode(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var stemmedWord = _isCaseSensitive ? _stemmer.Stem(text) : _stemmer.Stem(text.ToLowerInvariant());
        
        return ComputeHash(stemmedWord.AsSpan());
    }

    public ulong GetHashCode(ReadOnlyMemory<char> text)
    {
        return GetHashCode(text.Span);
    }

    ulong ComputeHash(ReadOnlySpan<char> text)
    {
        if (text.IsWhiteSpace()) return 0;
        var hashedValue = 3074457345618258791ul;
        for (var i = 0; i < text.Length; i++)
        {
            hashedValue += char.ToLowerInvariant(text[i]);
            hashedValue *= 3074457345618258799ul;
        }

        return hashedValue;
    }
}