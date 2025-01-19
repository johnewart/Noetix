using System.Text.Json;
using System.Text.RegularExpressions;

namespace Noetix.Common;

public class JsonResult(string json, bool valid, string error)
{
    public string Json { get; set; } = json;
    public bool Valid { get; set; } = valid;
    public string Error { get; set; } = error;

    public T? Deserialize<T>()
    {
        if (Valid)
        {
            return JsonHandling.DeserializeJson<T>(Json);
        }
        else
        {
            throw new Exception(message: "JSON was not valid, can't deserialize");
        }
    }
}

public static class JsonHandling
{
    public static O? DeserializeJson<O>(string input) => JsonSerializer.Deserialize<O>(input,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    public static JsonResult ExtractJSON(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(text))
                {
                    return new JsonResult(text, true, "");
                }
            }
            catch (JsonException)
            {
                // Ignore error, continue to next block
            }

            var jsonWithGuards = Regex.Matches(text, @"(?s)```json(.*?)```");
            if (jsonWithGuards.Count > 0)
            {
                var json = jsonWithGuards[0].Groups[1].Value;
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        return new JsonResult(json, true, "");
                    }
                }
                catch (JsonException e)
                {
                    return new JsonResult(json, false, e.Message);
                }
            }

            var jsonWithoutGuards = Regex.Match(text, @"(?s).*?(\{.*?\})");
            if (jsonWithoutGuards.Success)
            {
                var json = jsonWithoutGuards.Groups[1].Value;
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        return new JsonResult(json, true, "");
                    }
                }
                catch (JsonException e)
                {
                    return new JsonResult(json, false, e.Message);
                }
            }
        }

        return new JsonResult(text, false, "No JSON found");
    }
}