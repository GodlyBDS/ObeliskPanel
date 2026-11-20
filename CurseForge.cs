using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ObeliskPanel;

public record CfMod(int Id, string Name, string Summary, long Downloads, DateTime Updated, DateTime Released,
                    string? Logo, string Categories, long Size, string? Url);
public record CfCategory(int Id, string Name)
{
    public override string ToString() => Name;
}

public sealed class CurseForge : IDisposable
{
    public const int GameId = 83374;
    readonly HttpClient _http = new() { BaseAddress = new Uri("https://api.curseforge.com/v1/"), Timeout = TimeSpan.FromSeconds(20) };

    public CurseForge(string apiKey)
    {
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey.Trim());
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    static async Task<JsonElement> Read(HttpResponseMessage r)
    {
        if (!r.IsSuccessStatusCode)
            throw new HttpRequestException(r.StatusCode == HttpStatusCode.Forbidden
                ? $"CurseForge refused this request for your API key (403 at {r.RequestMessage?.RequestUri?.AbsolutePath})."
                : $"CurseForge error {(int)r.StatusCode}", null, r.StatusCode);
        return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
    }

    async Task<JsonElement> Get(string url) => await Read(await _http.GetAsync(url));

    public async Task<(List<CfMod> mods, int total)> Search(string? query, int? categoryId, int sortField, int index, int pageSize)
    {
        var url = $"mods/search?gameId={GameId}&index={index}&pageSize={pageSize}&sortField={sortField}&sortOrder=desc";
        if (!string.IsNullOrWhiteSpace(query)) url += "&searchFilter=" + Uri.EscapeDataString(query.Trim());
        if (categoryId != null) url += $"&categoryId={categoryId}";
        var root = await Get(url);
        var mods = root.GetProperty("data").EnumerateArray().Select(Parse).ToList();
        return (mods, root.GetProperty("pagination").GetProperty("totalCount").GetInt32());
    }

    public async Task<List<CfCategory>> Categories()
    {
        var root = await Get($"categories?gameId={GameId}");
        return root.GetProperty("data").EnumerateArray()
            .Where(e => !(e.TryGetProperty("isClass", out var c) && c.ValueKind == JsonValueKind.True))
            .Select(e => new CfCategory(e.GetProperty("id").GetInt32(), e.GetProperty("name").GetString() ?? ""))
            .OrderBy(c => c.Name).ToList();
    }

    public async Task<List<CfMod>> GetMods(IEnumerable<int> ids)
    {
        var body = JsonSerializer.Serialize(new { modIds = ids.ToArray() });
        var root = await Read(await _http.PostAsync("mods", new StringContent(body, Encoding.UTF8, "application/json")));
        return root.GetProperty("data").EnumerateArray().Select(Parse).ToList();
    }

    public async Task<string> Description(int id) =>
        (await Get($"mods/{id}/description")).GetProperty("data").GetString() ?? "";

    static string S(JsonElement e, string k) =>
        e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    static CfMod Parse(JsonElement e)
    {
        long size = 0;
        if (e.TryGetProperty("latestFiles", out var lf) && lf.ValueKind == JsonValueKind.Array && lf.GetArrayLength() > 0 &&
            lf[0].TryGetProperty("fileLength", out var fl)) size = fl.GetInt64();
        string? logo = e.TryGetProperty("logo", out var lg) && lg.ValueKind == JsonValueKind.Object ? S(lg, "thumbnailUrl") : null;
        string? url = e.TryGetProperty("links", out var ln) && ln.ValueKind == JsonValueKind.Object ? S(ln, "websiteUrl") : null;
        var cats = e.TryGetProperty("categories", out var cs) && cs.ValueKind == JsonValueKind.Array
            ? string.Join(", ", cs.EnumerateArray().Select(c => S(c, "name"))) : "";
        DateTime D(string k) => DateTime.TryParse(S(e, k), out var d) ? d : default;
        long dl = e.TryGetProperty("downloadCount", out var dc) && dc.ValueKind == JsonValueKind.Number ? (long)dc.GetDouble() : 0;
        return new CfMod(e.GetProperty("id").GetInt32(), S(e, "name"), S(e, "summary"), dl,
                         D("dateModified"), D("dateReleased"), string.IsNullOrEmpty(logo) ? null : logo, cats, size,
                         string.IsNullOrEmpty(url) ? null : url);
    }

    public void Dispose() => _http.Dispose();
}
