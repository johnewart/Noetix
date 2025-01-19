namespace Noetix.Knowledge;

public interface IDocumentSource
{
    IEnumerable<Document> Documents();
}