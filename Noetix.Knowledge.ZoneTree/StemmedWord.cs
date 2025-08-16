namespace Noetix.Knowledge.ZoneTree;

public struct StemmedWord
{
    public readonly string Value;

    public readonly string Unstemmed;

    public StemmedWord(string value, string unstemmed)
    {
        Value = value;
        Unstemmed = unstemmed;
    }

    public ReadOnlySpan<char> AsSpan()
    {
        return Value.AsSpan();
    }
}