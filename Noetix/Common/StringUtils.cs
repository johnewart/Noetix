namespace Noetix.Common;

internal class StringUtils
{
    public static string[] Dedent(string text)
    {
        var lines = text.Split(
            new[] { "\r\n", "\r", "\n" },
            StringSplitOptions.None);

        // Search for the first non-empty line starting from the second line.
        // The first line is not expected to be indented.
        var firstNonemptyLine = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length == 0) continue;

            firstNonemptyLine = i;
            break;
        }

        if (firstNonemptyLine < 0) return lines;

        // Search for the second non-empty line.
        // If there is no second non-empty line, we can return immediately as we
        // can not pin the indent.
        var secondNonemptyLine = -1;
        for (var i = firstNonemptyLine + 1; i < lines.Length; i++)
        {
            if (lines[i].Length == 0) continue;

            secondNonemptyLine = i;
            break;
        }

        if (secondNonemptyLine < 0) return lines;

        // Match the common prefix with at least two non-empty lines

        var firstNonemptyLineLength = lines[firstNonemptyLine].Length;
        var prefixLength = 0;

        for (int column = 0; column < firstNonemptyLineLength; column++)
        {
            char c = lines[firstNonemptyLine][column];
            if (c != ' ' && c != '\t') break;

            bool matched = true;
            for (int lineIdx = firstNonemptyLine + 1; lineIdx < lines.Length;
                 lineIdx++)
            {
                if (lines[lineIdx].Length == 0) continue;

                if (lines[lineIdx].Length < column + 1)
                {
                    matched = false;
                    break;
                }

                if (lines[lineIdx][column] != c)
                {
                    matched = false;
                    break;
                }
            }

            if (!matched) break;

            prefixLength++;
        }

        if (prefixLength == 0) return lines;

        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length > 0) lines[i] = lines[i].Substring(prefixLength);
        }

        return lines;
    }
}