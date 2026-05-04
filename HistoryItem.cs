namespace Math2Tex;

public sealed class HistoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Source { get; set; } = string.Empty;
    public string Latex { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string Title => string.IsNullOrWhiteSpace(Source) ? "(empty)" : Collapse(Source);
    public string Subtitle => CreatedAt.ToString("MM-dd HH:mm");
    public string LatexPreview => string.IsNullOrWhiteSpace(Latex) ? "" : Collapse(Latex);

    private static string Collapse(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        bool lastWasSpace = false;
        foreach (var ch in s)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace) { sb.Append(' '); lastWasSpace = true; }
            }
            else { sb.Append(ch); lastWasSpace = false; }
        }
        return sb.ToString().Trim();
    }
}
