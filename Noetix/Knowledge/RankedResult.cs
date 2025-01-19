namespace Noetix.Knowledge;

public class RankedResult(Document document, double score)
{
    public Document Document { get; set; } = document;
    public double Score { get; set; } = score;
}