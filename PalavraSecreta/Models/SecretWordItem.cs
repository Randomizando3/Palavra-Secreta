namespace PalavraSecreta.Models;

public sealed class SecretWordItem
{
    public string Theme { get; set; } = "";

    public string BaseWord { get; set; } = "";
    public int HiddenStart { get; set; } = 0;
    public int HiddenLength { get; set; } = 1;
    public string HiddenColor { get; set; } = "red";

    public string RevealedWord { get; set; } = "";

    public string HiddenPart => SafeHiddenPart();

    public string ComputeRevealed()
    {
        var w = (BaseWord ?? "").Trim().ToUpperInvariant();
        if (w.Length == 0) return "";

        if (HiddenStart < 0 || HiddenStart >= w.Length) return w;
        if (HiddenLength < 1) return w;
        if (HiddenStart + HiddenLength > w.Length) return w;

        return w.Remove(HiddenStart, HiddenLength);
    }

    public bool IsStructurallyValid()
    {
        var baseW = (BaseWord ?? "").Trim().ToUpperInvariant();
        var revW = (RevealedWord ?? "").Trim().ToUpperInvariant();

        if (baseW.Length < 4 || baseW.Length > 10) return false;
        if (HiddenStart < 0 || HiddenStart >= baseW.Length) return false;
        if (HiddenLength < 1 || HiddenLength > baseW.Length) return false;
        if (HiddenStart + HiddenLength > baseW.Length) return false;

        var computed = ComputeRevealed();
        if (computed.Length < 2) return false;

        return string.Equals(computed, revW, StringComparison.OrdinalIgnoreCase);
    }

    private string SafeHiddenPart()
    {
        var w = (BaseWord ?? "").Trim().ToUpperInvariant();
        if (w.Length == 0) return "";
        if (HiddenStart < 0 || HiddenStart >= w.Length) return "";
        if (HiddenLength < 1) return "";
        if (HiddenStart + HiddenLength > w.Length) return "";
        return w.Substring(HiddenStart, HiddenLength);
    }
}
