namespace PhotoProcessor.Core.Dating;

using System;
using System.Text.RegularExpressions;

internal static partial class FileNameDateParser
{
    public static DateTime? ParseDateTime(string fileName)
    {
        foreach (Match match in DateTimePattern().Matches(fileName))
        {
            if (TryCreate(match, includeTime: true, out var date))
            {
                return date;
            }
        }

        return null;
    }

    public static DateOnly? ParseDate(string fileName)
    {
        foreach (Match match in DatePattern().Matches(fileName))
        {
            if (TryCreate(match, includeTime: false, out var date))
            {
                return DateOnly.FromDateTime(date);
            }
        }

        return null;
    }

    private static bool TryCreate(Match match, bool includeTime, out DateTime date)
    {
        date = default;
        int Get(string group) => int.Parse(match.Groups[group].Value);

        int year = Get("y"), month = Get("mo"), day = Get("d");
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        if (!includeTime)
        {
            date = new DateTime(year, month, day);
            return true;
        }

        int hour = Get("h"), minute = Get("mi"), second = Get("s");
        if (hour > 23 || minute > 59 || second > 59)
        {
            return false;
        }

        date = new DateTime(year, month, day, hour, minute, second);
        return true;
    }

    [GeneratedRegex(@"(?<!\d)(?<y>(?:19|20)\d{2})[-_.]?(?<mo>\d{2})[-_.]?(?<d>\d{2})[ _T.-]{0,3}(?<h>\d{2})[-_.:]?(?<mi>\d{2})[-_.:]?(?<s>\d{2})")]
    private static partial Regex DateTimePattern();

    [GeneratedRegex(@"(?<!\d)(?<y>(?:19|20)\d{2})[-_.]?(?<mo>\d{2})[-_.]?(?<d>\d{2})(?!\d)")]
    private static partial Regex DatePattern();
}
