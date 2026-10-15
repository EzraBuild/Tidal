namespace Tidal.Core.Displays;

/// <summary>
/// A parsed DDC/CI capabilities string, e.g.
/// <c>(prot(monitor)type(lcd)model(P2422HE)vcp(10 12 60(0F 11 1B) 62)mccs_ver(2.1))</c>.
/// </summary>
/// <remarks>
/// Monitors are sloppy about the format (missing outer parentheses, codes run together without
/// spaces, unbalanced brackets), so parsing is lenient and never throws.
/// </remarks>
public sealed class MonitorCapabilities
{
    private readonly Dictionary<string, string> _sections;

    private MonitorCapabilities(string raw, Dictionary<string, string> sections, Dictionary<byte, IReadOnlyList<byte>> vcpCodes)
    {
        Raw = raw;
        _sections = sections;
        VcpCodes = vcpCodes;
    }

    public string Raw { get; }
    public string? Model => Section("model");
    public string? Type => Section("type");
    public string? MccsVersion => Section("mccs_ver");

    /// <summary>
    /// VCP codes the monitor claims to support, each with its allowed values. The list is empty for
    /// continuous features like brightness.
    /// </summary>
    public IReadOnlyDictionary<byte, IReadOnlyList<byte>> VcpCodes { get; }

    public bool Supports(byte code) => VcpCodes.ContainsKey(code);

    public IReadOnlyList<byte> AllowedValues(byte code) => VcpCodes.TryGetValue(code, out var values) ? values : [];

    public string? Section(string name) => _sections.TryGetValue(name, out var value) ? value.Trim() : null;

    public static MonitorCapabilities Parse(string raw)
    {
        string text = raw.TrimEnd('\0').Trim();
        if (text.StartsWith('('))
        {
            int close = FindClose(text, 0);
            if (close == text.Length - 1) text = text[1..^1];
            else if (close < 0) text = text[1..]; // truncated reply: the outer bracket never closes
        }

        var sections = ParseSections(text);
        var vcp = sections.TryGetValue("vcp", out var body) ? ParseVcp(body) : new Dictionary<byte, IReadOnlyList<byte>>();
        return new MonitorCapabilities(raw, sections, vcp);
    }

    private static Dictionary<string, string> ParseSections(string text)
    {
        var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int i = 0;
        while (i < text.Length)
        {
            int nameStart = i;
            while (i < text.Length && text[i] is not '(' and not ')') i++;
            if (i >= text.Length) break;
            if (text[i] == ')')
            {
                i++; // stray closing bracket
                continue;
            }

            string name = text[nameStart..i].Trim();
            int close = FindClose(text, i);
            if (close < 0) close = text.Length; // unterminated: take the rest
            if (name.Length > 0) sections.TryAdd(name, text[(i + 1)..close]);
            i = close + 1;
        }
        return sections;
    }

    private static Dictionary<byte, IReadOnlyList<byte>> ParseVcp(string body)
    {
        var codes = new Dictionary<byte, IReadOnlyList<byte>>();
        byte? lastCode = null;
        int i = 0;
        while (i < body.Length)
        {
            if (char.IsAsciiHexDigit(body[i]))
            {
                int start = i;
                while (i < body.Length && char.IsAsciiHexDigit(body[i])) i++;
                foreach (byte code in HexBytes(body[start..i]))
                {
                    codes.TryAdd(code, []);
                    lastCode = code;
                }
            }
            else if (body[i] == '(')
            {
                int close = FindClose(body, i);
                if (close < 0) close = body.Length;
                if (lastCode is byte code) codes[code] = HexBytes(body[(i + 1)..close]);
                i = close + 1;
            }
            else
            {
                i++;
            }
        }
        return codes;
    }

    /// <summary>Reads hex byte values, splitting runs like "0F111B" into 0F, 11, 1B.</summary>
    private static List<byte> HexBytes(string text)
    {
        var bytes = new List<byte>();
        int i = 0;
        while (i < text.Length)
        {
            if (!char.IsAsciiHexDigit(text[i]))
            {
                i++;
                continue;
            }
            int start = i;
            while (i < text.Length && char.IsAsciiHexDigit(text[i])) i++;
            var run = text.AsSpan(start, i - start);
            if (run.Length <= 2)
            {
                bytes.Add(Convert.ToByte(run.ToString(), 16));
                continue;
            }
            for (int j = 0; j + 1 < run.Length; j += 2)
                bytes.Add(Convert.ToByte(run.Slice(j, 2).ToString(), 16));
        }
        return bytes;
    }

    private static int FindClose(string text, int openIndex)
    {
        int depth = 0;
        for (int i = openIndex; i < text.Length; i++)
        {
            if (text[i] == '(') depth++;
            else if (text[i] == ')' && --depth == 0) return i;
        }
        return -1;
    }
}
