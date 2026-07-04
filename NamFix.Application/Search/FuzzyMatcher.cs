using System.Globalization;
using System.Text;

namespace NamFix.Application.Search;

/// <summary>
/// Typo- and spacing-tolerant text scorer used by provider search (typeahead + full-search recall).
///
/// SQL Server full-text (FREETEXT/CONTAINS) matches whole words and inflections, but it can't handle
/// the everyday ways people mistype a business name:
///   • wrong/missing spaces — "NamibBuild" ⇄ "Namib Build"
///   • dropped/extra letters — "Namb" → "Namib"
///   • partial words        — "Namb B" → "Namib Build"
///
/// This matcher works entirely in memory over a small, cached index of active providers, so it can
/// afford per-token edit-distance without a DB round-trip. Everything a query is compared against is
/// pre-normalized once when the index is built (see <see cref="FuzzyEntry"/>), so scoring a keystroke
/// is just arithmetic over short strings.
///
/// Scores are in [0, 1]; callers keep candidates at or above <see cref="MatchThreshold"/>.
/// </summary>
public static class FuzzyMatcher
{
    /// <summary>Minimum score for a candidate to be considered a match.</summary>
    public const double MatchThreshold = 0.5;

    /// <summary>A query prepared once (normalized token + spaceless forms) so it can be scored against
    /// many entries cheaply.</summary>
    public readonly record struct PreparedQuery(string Spaceless, string[] Tokens)
    {
        public bool IsEmpty => Tokens.Length == 0;
    }

    /// <summary>Normalize raw user input into the token/spaceless forms used for scoring.</summary>
    public static PreparedQuery PrepareQuery(string? raw)
    {
        var tokens = Tokenize(raw);
        return new PreparedQuery(string.Concat(tokens), tokens);
    }

    /// <summary>Split text into lowercase, diacritic-free alphanumeric tokens.</summary>
    public static string[] Tokenize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return Array.Empty<string>();

        var normalized = s.Normalize(NormalizationForm.FormD);
        var tokens = new List<string>();
        var sb = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue; // drop the accent, keep the base letter

            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
            else if (sb.Length > 0)
            {
                tokens.Add(sb.ToString());
                sb.Clear();
            }
        }
        if (sb.Length > 0) tokens.Add(sb.ToString());

        return tokens.ToArray();
    }

    /// <summary>Score an already-prepared query against a pre-normalized index entry (0 = no match).</summary>
    public static double Score(in PreparedQuery query, FuzzyEntry entry)
    {
        if (query.IsEmpty) return 0;

        // Whole-name spaceless signals: forgive wrong/missing spaces regardless of where they fall.
        var spaceless = SpacelessSignal(query.Spaceless, entry.NameSpaceless);

        // Per-token coverage against the name: forgives typos and partial words token-by-token.
        var nameTokens = TokenCoverage(query.Tokens, entry.NameTokens);

        // Category + tag keywords give recall for searches like "electric" or "geyser"; weighted a
        // little below a direct name hit so name matches always rank first.
        var keywordTokens = 0.85 * TokenCoverage(query.Tokens, entry.KeywordTokens);

        return Math.Max(spaceless, Math.Max(nameTokens, keywordTokens));
    }

    /// <summary>Equality / prefix / containment of the space-stripped forms.</summary>
    private static double SpacelessSignal(string q, string c)
    {
        if (q.Length == 0 || c.Length == 0) return 0;
        if (c == q) return 1.0;
        if (q.Length >= 2 && c.StartsWith(q, StringComparison.Ordinal)) return 0.95;
        // Containment is noisy for very short inputs, so only trust it once the query is specific.
        if (q.Length >= 4 && c.Contains(q, StringComparison.Ordinal)) return 0.85;
        return 0;
    }

    /// <summary>
    /// Score how well a query covers a single term's tokens (e.g. one tag). Used to work out *which*
    /// tag drove a match so the UI can show it. Same metric as the per-name token coverage.
    /// </summary>
    public static double ScoreTerm(in PreparedQuery query, string[] termTokens) =>
        TokenCoverage(query.Tokens, termTokens);

    /// <summary>Average best-match score of each query token against the candidate's tokens.</summary>
    private static double TokenCoverage(string[] queryTokens, string[] candidateTokens)
    {
        if (queryTokens.Length == 0 || candidateTokens.Length == 0) return 0;

        double sum = 0;
        foreach (var qt in queryTokens)
        {
            double best = 0;
            foreach (var ct in candidateTokens)
            {
                var s = PairScore(qt, ct);
                if (s > best) best = s;
                if (best >= 1.0) break;
            }
            sum += best;
        }
        return sum / queryTokens.Length;
    }

    /// <summary>Similarity of two single tokens: exact &gt; prefix &gt; bounded edit distance.</summary>
    private static double PairScore(string qt, string ct)
    {
        if (qt == ct) return 1.0;

        // Candidate word starts with what was typed ("b" → "build"): the closer the lengths, the
        // more of the word was actually matched.
        if (ct.StartsWith(qt, StringComparison.Ordinal))
            return 0.88 + 0.12 * ((double)qt.Length / ct.Length);

        // The typed word starts with the candidate (extra characters typed).
        if (qt.StartsWith(ct, StringComparison.Ordinal))
            return 0.80 + 0.10 * ((double)ct.Length / qt.Length);

        // Otherwise fall back to edit distance, with an allowance that grows with word length.
        var maxLen = Math.Max(qt.Length, ct.Length);
        var cap = maxLen <= 3 ? 0 : maxLen <= 5 ? 1 : maxLen <= 8 ? 2 : 3;
        if (cap == 0) return 0;

        var distance = BoundedLevenshtein(qt, ct, cap);
        if (distance > cap) return 0;

        // Keep fuzzy matches just under a prefix hit so exact prefixes always win.
        return 0.9 * (1.0 - (double)distance / maxLen);
    }

    /// <summary>Levenshtein distance with an early exit once <paramref name="cap"/> is exceeded.</summary>
    private static int BoundedLevenshtein(string a, string b, int cap)
    {
        // Length difference alone can already exceed the allowance.
        if (Math.Abs(a.Length - b.Length) > cap) return cap + 1;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowMin = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
                if (current[j] < rowMin) rowMin = current[j];
            }
            if (rowMin > cap) return cap + 1; // whole row already worse than the allowance

            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}
