using System.Text.RegularExpressions;

namespace XtermSharp.Addons.WebLinks;

internal static class WebLinkComputer
{
    private const int MaximumWindowLength = 2048;

    public static IReadOnlyList<TerminalLink> ComputeLinks(
        int bufferLineNumber,
        Regex regex,
        TerminalSnapshot snapshot,
        Action<TerminalLinkEvent, string> activate)
    {
        ArgumentNullException.ThrowIfNull(regex);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(activate);

        (List<string> lines, int startLineIndex) = GetWindowedLineStrings(
            bufferLineNumber - 1,
            snapshot.ActiveBuffer);
        string line = string.Concat(lines);
        var result = new List<TerminalLink>();
        foreach (Match match in regex.Matches(line))
        {
            string text = match.Value;
            if (text.Length == 0 || !IsUrl(text))
            {
                continue;
            }

            (int startY, int startX) = MapStringIndex(
                snapshot.ActiveBuffer,
                startLineIndex,
                0,
                match.Index);
            (int endY, int endX) = MapStringIndex(
                snapshot.ActiveBuffer,
                startY,
                startX,
                text.Length);
            if (startY == -1 || startX == -1 || endY == -1 || endX == -1)
            {
                continue;
            }

            result.Add(new TerminalLink(
                new TerminalLinkRange(
                    new TerminalLinkPosition(startX + 1, startY + 1),
                    new TerminalLinkPosition(endX, endY + 1)),
                text,
                activate));
        }
        return result;
    }

    internal static bool IsUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        var parsedBase = new System.Text.StringBuilder();
        parsedBase.Append(uri.Scheme);
        parsedBase.Append("://");
        if (uri.UserInfo.Length != 0)
        {
            parsedBase.Append(uri.UserInfo);
            parsedBase.Append('@');
        }
        parsedBase.Append(uri.Host);
        if (!uri.IsDefaultPort)
        {
            parsedBase.Append(':');
            parsedBase.Append(uri.Port);
        }
        return value.StartsWith(parsedBase.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static (List<string> Lines, int StartLineIndex) GetWindowedLineStrings(
        int lineIndex,
        TerminalBufferSnapshot buffer)
    {
        int topIndex = lineIndex;
        int bottomIndex = lineIndex;
        var lines = new List<string>();
        TerminalLineSnapshot? line = buffer.GetLine(lineIndex);
        if (line is null)
        {
            return (lines, topIndex);
        }

        string currentContent = line.TranslateToString(trimRight: true);
        if (line.IsWrapped && (currentContent.Length == 0 || currentContent[0] != ' '))
        {
            int length = 0;
            while (length < MaximumWindowLength && (line = buffer.GetLine(--topIndex)) is not null)
            {
                string content = line.TranslateToString(trimRight: true);
                length += content.Length;
                lines.Add(content);
                if (!line.IsWrapped || content.Contains(' ', StringComparison.Ordinal))
                {
                    break;
                }
            }
            lines.Reverse();
        }

        lines.Add(currentContent);
        int bottomLength = 0;
        while (bottomLength < MaximumWindowLength &&
               (line = buffer.GetLine(++bottomIndex)) is { IsWrapped: true })
        {
            string content = line.TranslateToString(trimRight: true);
            bottomLength += content.Length;
            lines.Add(content);
            if (content.Contains(' ', StringComparison.Ordinal))
            {
                break;
            }
        }
        return (lines, topIndex);
    }

    private static (int LineIndex, int ColumnIndex) MapStringIndex(
        TerminalBufferSnapshot buffer,
        int lineIndex,
        int rowIndex,
        int stringIndex)
    {
        int start = rowIndex;
        while (stringIndex != 0)
        {
            TerminalLineSnapshot? line = buffer.GetLine(lineIndex);
            if (line is null)
            {
                return (-1, -1);
            }
            for (int index = start; index < line.Length; index++)
            {
                TerminalCellSnapshot cell = line.Cells[index];
                string characters = cell.GetChars();
                if (cell.GetWidth() != 0)
                {
                    stringIndex -= characters.Length == 0 ? 1 : characters.Length;
                    if (index == line.Length - 1 && characters.Length == 0)
                    {
                        TerminalLineSnapshot? nextLine = buffer.GetLine(lineIndex + 1);
                        if (nextLine is { IsWrapped: true } &&
                            nextLine.GetCell(0) is TerminalCellSnapshot nextCell &&
                            nextCell.GetWidth() == 2)
                        {
                            stringIndex++;
                        }
                    }
                }
                if (stringIndex < 0)
                {
                    return (lineIndex, index);
                }
            }
            lineIndex++;
            start = 0;
        }
        return (lineIndex, start);
    }
}
