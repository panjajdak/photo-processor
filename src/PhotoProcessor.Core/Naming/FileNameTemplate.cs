namespace PhotoProcessor.Core.Naming;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// File name pattern such as "{type} {date:yyyy-MM-dd HH.mm.ss}[ {device}][ {counter}]".
/// Text outside braces is literal; "[...]" is left out unless every token inside has a value;
/// "{{", "}}", "[[" and "]]" stand for the literal characters.
/// </summary>
internal sealed class FileNameTemplate
{
    public const string Default = "{type} {date:yyyy-MM-dd HH.mm.ss}[ {device}][ {counter}]";

    private const string DefaultDateFormat = "yyyy-MM-dd HH.mm.ss";
    private static readonly HashSet<string> TokenNames = ["type", "date", "device", "make", "model", "original", "counter"];
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    private readonly IReadOnlyList<Segment> segments;

    private FileNameTemplate(IReadOnlyList<Segment> segments)
    {
        this.segments = segments;
    }

    /// <exception cref="FormatException">The template is invalid.</exception>
    public static FileNameTemplate Parse(string template)
    {
        var segments = new List<Segment>();
        List<Segment>? optional = null;
        var literal = new StringBuilder();

        void FlushLiteral()
        {
            if (literal.Length > 0)
            {
                (optional ?? segments).Add(new Literal(literal.ToString()));
                literal.Clear();
            }
        }

        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            var next = i + 1 < template.Length ? template[i + 1] : '\0';

            if (c is '{' or '}' or '[' or ']' && next == c)
            {
                literal.Append(c);
                i++;
                continue;
            }

            switch (c)
            {
                case '{':
                    var end = template.IndexOf('}', i);
                    if (end < 0)
                    {
                        throw Error(template, "'{' is not closed");
                    }

                    FlushLiteral();
                    (optional ?? segments).Add(ParseToken(template, template[(i + 1)..end]));
                    i = end;
                    break;
                case '}':
                    throw Error(template, "'}' without '{' (write '}}' for a literal brace)");
                case '[':
                    if (optional != null)
                    {
                        throw Error(template, "optional sections '[...]' cannot be nested");
                    }

                    FlushLiteral();
                    optional = [];
                    break;
                case ']':
                    if (optional == null)
                    {
                        throw Error(template, "']' without '[' (write ']]' for a literal bracket)");
                    }

                    FlushLiteral();
                    if (!optional.OfType<Token>().Any())
                    {
                        throw Error(template, "an optional section '[...]' must contain a token");
                    }

                    segments.Add(new Optional(optional));
                    optional = null;
                    break;
                default:
                    literal.Append(c);
                    break;
            }
        }

        if (optional != null)
        {
            throw Error(template, "'[' is not closed");
        }

        FlushLiteral();
        Validate(template, segments);

        // Collisions must always be resolvable, so a template without a counter gets one at the end.
        if (!AllTokens(segments).Any(t => t.Name == "counter"))
        {
            segments.Add(new Optional([new Literal(" "), new Token("counter", null)]));
        }

        return new FileNameTemplate(segments);
    }

    /// <summary>
    /// Renders the file name without extension. Counter 0 means "no counter".
    /// </summary>
    public string Render(NameContext context, int counter)
    {
        var sb = new StringBuilder();
        foreach (var segment in segments)
        {
            switch (segment)
            {
                case Literal literal:
                    sb.Append(literal.Text);
                    break;
                case Token token:
                    sb.Append(Value(token, context, counter));
                    break;
                case Optional optional:
                    var parts = optional.Segments.Select(s => s is Token t ? Value(t, context, counter) : ((Literal)s).Text).ToList();
                    if (!parts.Contains(null))
                    {
                        sb.Append(string.Concat(parts));
                    }

                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// True when <paramref name="stem"/> is what <see cref="Render"/> produces for this context with any counter.
    /// {original} matches anything, so a file named after its original name is recognized on later runs.
    /// </summary>
    public bool Matches(string stem, NameContext context)
    {
        var pattern = new StringBuilder("^");
        foreach (var segment in segments)
        {
            switch (segment)
            {
                case Literal literal:
                    pattern.Append(Regex.Escape(literal.Text));
                    break;
                case Token token:
                    pattern.Append(TokenPattern(token, context) ?? "");
                    break;
                case Optional optional:
                    var parts = optional.Segments.Select(s => s is Token t ? TokenPattern(t, context) : Regex.Escape(((Literal)s).Text)).ToList();
                    if (parts.Contains(null))
                    {
                        break;
                    }

                    var isWildcard = optional.Segments.OfType<Token>().Any(t => t.Name is "counter" or "original");
                    pattern.Append(isWildcard ? $"(?:{string.Concat(parts)})?" : string.Concat(parts));
                    break;
            }
        }

        pattern.Append('$');
        return Regex.IsMatch(stem, pattern.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string? Value(Token token, NameContext context, int counter)
    {
        var value = token.Name switch
        {
            "type" => context.Type,
            "date" => context.Date.ToString(token.Format ?? DefaultDateFormat, CultureInfo.InvariantCulture),
            "device" => context.Device,
            "make" => Sanitize(context.Make),
            "model" => Sanitize(context.Model),
            "original" => Sanitize(context.Original),
            "counter" => counter == 0 ? null : counter.ToString(token.Format, CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException(token.Name),
        };

        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string? TokenPattern(Token token, NameContext context) => token.Name switch
    {
        "counter" => @"\d+",
        "original" => ".+?",
        _ => Value(token, context, 0) is { } value ? Regex.Escape(value) : null,
    };

    private static string? Sanitize(string? value)
        => value == null ? null : new string(value.Select(c => InvalidFileNameChars.Contains(c) ? '_' : c).ToArray()).Trim();

    private static Token ParseToken(string template, string body)
    {
        var colon = body.IndexOf(':');
        var name = (colon < 0 ? body : body[..colon]).Trim().ToLowerInvariant();
        var format = colon < 0 ? null : body[(colon + 1)..];

        if (!TokenNames.Contains(name))
        {
            throw Error(template, $"unknown token '{{{body}}}'; available: {string.Join(", ", TokenNames.Select(n => "{" + n + "}"))}");
        }

        if (format != null && name is not ("date" or "counter"))
        {
            throw Error(template, $"'{{{name}}}' does not take a format");
        }

        return new Token(name, format);
    }

    private static void Validate(string template, IReadOnlyList<Segment> segments)
    {
        var tokens = AllTokens(segments).ToList();
        if (!tokens.Any(t => t.Name is "date" or "original"))
        {
            throw Error(template, "it must contain {date} or {original}");
        }

        // A counter that disappears with a missing value could never number collisions.
        if (segments.OfType<Optional>().Any(o => o.Segments.OfType<Token>().Any(t => t.Name == "counter") && o.Segments.OfType<Token>().Count() > 1))
        {
            throw Error(template, "{counter} cannot share an optional section with other tokens");
        }

        foreach (var literal in segments.Concat(segments.OfType<Optional>().SelectMany(o => o.Segments)).OfType<Literal>())
        {
            if (literal.Text.IndexOfAny(InvalidFileNameChars) >= 0)
            {
                throw Error(template, $"'{literal.Text}' contains characters not allowed in file names");
            }
        }

        var sample = new DateTime(2021, 12, 31, 23, 59, 58, 123);
        foreach (var token in tokens.Where(t => t.Format != null))
        {
            string rendered;
            try
            {
                rendered = token.Name == "date"
                    ? sample.ToString(token.Format, CultureInfo.InvariantCulture)
                    : 7.ToString(token.Format, CultureInfo.InvariantCulture);
            }
            catch (FormatException ex)
            {
                throw Error(template, $"'{token.Format}' is not a valid {token.Name} format: {ex.Message}");
            }

            if (rendered.Length == 0 || rendered.IndexOfAny(InvalidFileNameChars) >= 0)
            {
                throw Error(template, $"format '{token.Format}' produces '{rendered}', which is not allowed in file names");
            }
        }

        // Windows silently drops trailing spaces and dots, which would break re-run detection.
        if (segments.LastOrDefault() is Literal last && (last.Text.EndsWith(' ') || last.Text.EndsWith('.')))
        {
            throw Error(template, "it must not end with a space or a dot");
        }
    }

    private static IEnumerable<Token> AllTokens(IEnumerable<Segment> segments)
        => segments.SelectMany(s => s switch
        {
            Token t => [t],
            Optional o => o.Segments.OfType<Token>(),
            _ => [],
        });

    private static FormatException Error(string template, string reason)
        => new($"Invalid FileNameTemplate \"{template}\": {reason}.");

    private abstract record Segment;

    private sealed record Literal(string Text) : Segment;

    private sealed record Token(string Name, string? Format) : Segment;

    private sealed record Optional(IReadOnlyList<Segment> Segments) : Segment;
}
