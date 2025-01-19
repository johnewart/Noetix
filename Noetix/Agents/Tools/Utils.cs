namespace Noetix.Agents.Tools;

public class Utils
{
    public static string StructToXMLLikeString(Dictionary<string, object> context)
    {
        // if (context.Count == 0) return string.Empty;

        var lines = context.Keys.Select(key =>
        {
            var value = context[key];
            switch (value)
            {
                case string v when !string.IsNullOrEmpty(v):
                    return $"<{key}>{v}</{key}>";
                case IEnumerable<object> v:
                    var children = v.Select(item =>
                    {
                        switch (item)
                        {
                            case string s:
                                return $"<item>{s}</item>";
                            case Dictionary<string, object> m:
                                var inner = StructToXMLLikeString(m);
                                return $"<{key}>{inner}<{key}>";
                            default:
                                return string.Empty;
                        }
                    });
                    return $"<{key}>{string.Join("\n", children)}</{key}>";
                case Dictionary<string, object> v:
                    var inner = StructToXMLLikeString(v);
                    return $"<{key}>{inner}</{key}>";
                default:
                    return string.Empty;
            }
        });
            
        return string.Join("\n", lines);
    }
}