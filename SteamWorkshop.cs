using System.Net.Http;
using System.Text.Json;

namespace ObeliskPanel;

public record WorkshopItem(string Id, string Title, string Description, long Size);
public record WorkshopListing(string Id, string Title, string Summary, string? Preview, long Subscriptions,
                              DateTime Updated, long Size, string Tags);

public static class SteamWorkshop
{
    public const int AseAppId = 346110;
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static string ItemUrl(string id) => $"https://steamcommunity.com/sharedfiles/filedetails/?id={id}";
    public static string BrowseUrl => $"https://steamcommunity.com/app/{AseAppId}/workshop/";

    public static readonly (string label, int type)[] Sorts =
    {
        ("Trending (week)", 3), ("Most Subscribed", 12), ("Most Popular (votes)", 0), ("Newest", 1),
    };

    public static async Task<(List<WorkshopListing> items, int total)> Query(string apiKey, string? text, int sortType,
                                                                              int page, int perPage, string? requiredTag)
    {
        var q = new List<string>
        {
            "key=" + Uri.EscapeDataString(apiKey.Trim()), $"appid={AseAppId}", $"page={page}", $"numperpage={perPage}",
            "return_previews=true", "return_short_description=true", "return_tags=true", "return_metadata=true",
        };
        if (!string.IsNullOrWhiteSpace(text)) { q.Add("search_text=" + Uri.EscapeDataString(text.Trim())); q.Add("query_type=11"); }
        else { q.Add($"query_type={sortType}"); if (sortType == 3) q.Add("days=7"); }
        if (!string.IsNullOrWhiteSpace(requiredTag)) { q.Add("requiredtags[0]=" + Uri.EscapeDataString(requiredTag)); q.Add("match_all_tags=true"); }

        var r = await Http.GetAsync("https://api.steampowered.com/IPublishedFileService/QueryFiles/v1/?" + string.Join("&", q));
        if (r.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Unauthorized)
            throw new HttpRequestException("Steam rejected the Web API key (check it under Server Settings → Cluster & App).", null, r.StatusCode);
        r.EnsureSuccessStatusCode();
        var resp = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("response");
        int total = resp.TryGetProperty("total", out var tt) && tt.TryGetInt32(out var n) ? n : 0;
        var list = new List<WorkshopListing>();
        if (resp.TryGetProperty("publishedfiledetails", out var arr))
            foreach (var e in arr.EnumerateArray())
            {
                string S(string k) => e.TryGetProperty(k, out var v) ? v.ToString() : "";
                long L(string k) => long.TryParse(S(k), out var x) ? x : 0;
                var tags = e.TryGetProperty("tags", out var tg) && tg.ValueKind == JsonValueKind.Array
                    ? string.Join(", ", tg.EnumerateArray().Select(x => x.TryGetProperty("tag", out var tv) ? tv.GetString() : "")) : "";
                var summary = S("short_description");
                if (summary == "") summary = S("file_description");
                list.Add(new WorkshopListing(S("publishedfileid"), S("title"), summary,
                    S("preview_url") is var pv && pv != "" ? pv : null, L("subscriptions"),
                    DateTimeOffset.FromUnixTimeSeconds(L("time_updated")).LocalDateTime, L("file_size"), tags));
            }
        return (list, total);
    }

    public static async Task<List<WorkshopItem>> GetDetails(IReadOnlyList<string> ids)
    {
        var form = new List<KeyValuePair<string, string>> { new("itemcount", ids.Count.ToString()) };
        for (int i = 0; i < ids.Count; i++) form.Add(new($"publishedfileids[{i}]", ids[i]));
        var r = await Http.PostAsync("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
                                     new FormUrlEncodedContent(form));
        r.EnsureSuccessStatusCode();
        var root = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
        var list = new List<WorkshopItem>();
        if (!root.GetProperty("response").TryGetProperty("publishedfiledetails", out var arr)) return list;
        foreach (var e in arr.EnumerateArray())
        {
            if (e.TryGetProperty("result", out var res) && res.GetInt32() != 1) continue;
            string S(string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            long size = e.TryGetProperty("file_size", out var fs) && long.TryParse(fs.ToString(), out var n) ? n : 0;
            list.Add(new WorkshopItem(S("publishedfileid"), S("title"), S("description"), size));
        }
        return list;
    }
}
