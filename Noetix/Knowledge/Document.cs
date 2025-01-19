using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Noetix.Knowledge;

public class Document(
    string content,
    string name,
    string id,
    Dictionary<string, string> metadata = null,
    double[] embedding = null,
    Dictionary<string, string> usage = null)
{
    public string Content { get; set; } = content;
    public string Name { get; set; } = name;
    public string Id { get; set; } = id;
    public Dictionary<string, string> Metadata { get; set; } = metadata ?? new Dictionary<string, string>();
    public double[] Embedding { get; set; } = embedding;
    public Dictionary<string, string> Usage { get; set; } = usage ?? new Dictionary<string, string>();

    public string CleanedContent
    {
        get
        {
            string cleanedText = Regex.Replace(Content, "\n+", "\n");
            cleanedText = Regex.Replace(cleanedText, "\\s+", " ");
            cleanedText = Regex.Replace(cleanedText, "\t+", "\t");
            cleanedText = Regex.Replace(cleanedText, "\r+", "\r");
            cleanedText = Regex.Replace(cleanedText, "\f+", "\f");
            return cleanedText;
        }
    }

    public List<Document> Chunks(int chunkSize)
    {
        string cleanedContent = CleanedContent;
        string[] words = cleanedContent.Split(' ');
        int contentLength = words.Length;
        List<Document> chunkedDocuments = new List<Document>();
        int chunkNumber = 1;
        Dictionary<string, string> chunkMetaData = new Dictionary<string, string>(Metadata);

        int start = 0;
        while (start < contentLength)
        {
            int end = Math.Min(start + chunkSize, contentLength);
            string chunk = string.Join(" ", words.Skip(start).Take(end - start));
            var metaData = new Dictionary<string, string>(chunkMetaData)
            {
                { "chunk", chunkNumber.ToString() },
                { "chunk_size", chunk.Length.ToString() }
            };
            string chunkId = $"{Id}_{chunkNumber}";

            chunkedDocuments.Add(new Document(chunk, Name, chunkId, metaData));
            chunkNumber++;
            start = end;
        }
        return chunkedDocuments;
    }

    public string ContentHash
    {
        get
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(CleanedContent));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}