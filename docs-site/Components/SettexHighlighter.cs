using System.Net;
using System.Text;

namespace Settex.Docs.Components;

/// <summary>
/// A small, self-contained syntax highlighter for Settex source shown in the
/// docs. It scans the sample text and returns HTML with <c>&lt;span class="tok-*"&gt;</c>
/// wrappers (all text is HTML-encoded), so the docs no longer rely on a dead
/// <c>lang-*</c> class with no styling behind it. Only the Settex language is
/// highlighted; other snippets (JSON, shell, …) render as plain, encoded text.
/// </summary>
public static class SettexHighlighter
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "settings", "env", "let", "include", "for", "in",
        "if", "then", "else", "and", "or", "not",
    };

    private static readonly HashSet<string> Constants = new(StringComparer.Ordinal)
    {
        "true", "false", "null",
    };

    /// <summary>
    /// Returns the highlighted HTML for a Settex snippet. The result is safe to
    /// render as raw markup: every character from <paramref name="code"/> is
    /// HTML-encoded before being emitted.
    /// </summary>
    public static string Highlight(string code)
    {
        var html = new StringBuilder(code.Length + 64);
        var plain = new StringBuilder();
        var i = 0;

        void FlushPlain()
        {
            if (plain.Length > 0)
            {
                html.Append(Encode(plain.ToString()));
                plain.Clear();
            }
        }

        while (i < code.Length)
        {
            var c = code[i];

            // Line comment: // ... to end of line
            if (c == '/' && i + 1 < code.Length && code[i + 1] == '/')
            {
                FlushPlain();
                var start = i;
                while (i < code.Length && code[i] != '\n')
                {
                    i++;
                }

                Emit(html, "tok-comment", code[start..i]);
                continue;
            }

            // String literal, with ${ } interpolation highlighted separately
            if (c == '"')
            {
                FlushPlain();
                i = AppendString(html, code, i);
                continue;
            }

            // Number literal
            if (char.IsDigit(c))
            {
                FlushPlain();
                var start = i;
                while (i < code.Length && (char.IsDigit(code[i]) || code[i] == '.'))
                {
                    i++;
                }

                Emit(html, "tok-num", code[start..i]);
                continue;
            }

            // Identifier or keyword
            if (char.IsLetter(c) || c == '_')
            {
                FlushPlain();
                var start = i;
                while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '_'))
                {
                    i++;
                }

                var word = code[start..i];
                var cls = Keywords.Contains(word) ? "tok-kw"
                        : Constants.Contains(word) ? "tok-bool"
                        : null;

                if (cls != null)
                {
                    Emit(html, cls, word);
                }
                else
                {
                    plain.Append(word);
                }

                continue;
            }

            plain.Append(c);
            i++;
        }

        FlushPlain();
        return html.ToString();
    }

    /// <summary>
    /// Appends a string literal starting at <paramref name="start"/> (the opening
    /// quote) and returns the index just past the closing quote. <c>${ … }</c>
    /// interpolation segments inside the string get their own token class.
    /// </summary>
    private static int AppendString(StringBuilder html, string code, int start)
    {
        var i = start + 1;
        var literal = new StringBuilder("\"");

        void FlushLiteral()
        {
            if (literal.Length > 0)
            {
                Emit(html, "tok-str", literal.ToString());
                literal.Clear();
            }
        }

        while (i < code.Length)
        {
            var c = code[i];

            if (c == '\\' && i + 1 < code.Length)
            {
                // Keep escape sequences (\" \\ …) as part of the string literal.
                literal.Append(c).Append(code[i + 1]);
                i += 2;
                continue;
            }

            if (c == '"')
            {
                literal.Append('"');
                i++;
                break;
            }

            if (c == '$' && i + 1 < code.Length && code[i + 1] == '{')
            {
                FlushLiteral();
                var exprStart = i;
                i += 2;
                var depth = 1;
                while (i < code.Length && depth > 0)
                {
                    if (code[i] == '{')
                    {
                        depth++;
                    }
                    else if (code[i] == '}')
                    {
                        depth--;
                    }

                    i++;
                }

                Emit(html, "tok-interp", code[exprStart..i]);
                continue;
            }

            if (c == '\n')
            {
                // Unterminated (shouldn't happen in valid samples) — stop at EOL.
                break;
            }

            literal.Append(c);
            i++;
        }

        FlushLiteral();
        return i;
    }

    private static void Emit(StringBuilder html, string cssClass, string text)
        => html.Append("<span class=\"").Append(cssClass).Append("\">").Append(Encode(text)).Append("</span>");

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}
