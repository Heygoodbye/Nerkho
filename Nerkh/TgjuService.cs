using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace Nerkh;

public sealed class TgjuService : IDisposable
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(8) };
    public TgjuService() { _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Nerkh/1.0"); _client.DefaultRequestHeaders.Referrer = new Uri("https://www.tgju.org/"); }
    public async Task<Dictionary<string, Quote>> GetQuotesAsync(CancellationToken token)
    {
        using var response = await _client.GetAsync("https://call3.tgju.org/ajax.json", HttpCompletionOption.ResponseHeadersRead, token); response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token); using var json = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        var result = new Dictionary<string, Quote>(StringComparer.OrdinalIgnoreCase); if (!json.RootElement.TryGetProperty("current", out var current)) return result;
        foreach (var entry in current.EnumerateObject()) { var v = entry.Value; result[entry.Name] = new Quote(Number(v, "p"), Number(v, "h"), Number(v, "l"), Number(v, "d"), Number(v, "dp"), Text(v, "t_en", "—"), Text(v, "ts", "")); }
        return result;
    }
    private static decimal Number(JsonElement item, string name) { if (!item.TryGetProperty(name, out var node)) return 0; var raw = node.ValueKind == JsonValueKind.String ? node.GetString() : node.GetRawText(); return decimal.TryParse(raw?.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0; }
    private static string Text(JsonElement item, string name, string fallback) => item.TryGetProperty(name, out var node) ? node.ToString() : fallback;
    public void Dispose() => _client.Dispose();
}
public readonly record struct Quote(decimal Price, decimal High, decimal Low, decimal Change, decimal Percent, string Time, string Timestamp);
