using System.Net.Http;
using System.Text.Json;

namespace HamTool.Services;

public sealed record SearchLink(string Name, string Purpose, string Url);
public sealed record LiveSearchResult(string Source, string Title, string Summary, string Url)
{
    public string Display => $"[{Source}] {Title}\n{Summary}\n{Url}";
}
public sealed record ProviderStatus(string Name, string Status, int Results, string Detail);
public sealed record LiveSearchReport(List<LiveSearchResult> Results, List<ProviderStatus> Providers)
{
    public int SuccessfulProviders => Providers.Count(x => x.Status == "OK");
}

/// <summary>
/// Public-source search helpers. Link generation is separate from live API connectors;
/// no login, access-control bypass, or private-account collection is performed.
/// </summary>
public static class PublicSearchService
{
    private static readonly HttpClient Http = CreateClient();
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HamTool-Pro/2.0 (public-source research; contact: local-app)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    public static List<SearchLink> Build(string type, string raw, string? language = null)
    {
        var q = raw.Trim(); var e = Uri.EscapeDataString(q);
        var list = new List<SearchLink>
        {
            new("Google", "General web search", $"https://www.google.com/search?q={e}"),
            new("Bing", "General web search", $"https://www.bing.com/search?q={e}"),
            new("DuckDuckGo", "General web search", $"https://duckduckgo.com/?q={e}"),
            new("Brave Search", "Web search", $"https://search.brave.com/search?q={e}"),
            new("Google exact phrase", "Exact phrase", $"https://www.google.com/search?q=%22{e}%22"),
            new("Bing exact phrase", "Exact phrase", $"https://www.bing.com/search?q=%22{e}%22"),
            new("Wikipedia", "Encyclopedia search", $"https://en.wikipedia.org/w/index.php?search={e}"),
            new("Wikidata", "Structured public knowledge graph", $"https://www.wikidata.org/w/index.php?search={e}"),
            new("Internet Archive", "Public web and media archive", $"https://archive.org/search?query={e}"),
            new("OpenAlex", "Open scholarly research catalog", $"https://openalex.org/works?search={e}"),
            new("Crossref", "Scholarly publication metadata", $"https://search.crossref.org/?q={e}"),
            new("GitHub", "Public code and repository search", $"https://github.com/search?q={e}"),
            new("GitLab", "Public projects search", $"https://gitlab.com/search?search={e}"),
            new("Stack Overflow", "Public technical Q&A", $"https://stackoverflow.com/search?q={e}"),
            new("Hacker News", "Public community discussion search", $"https://hn.algolia.com/?q={e}"),
            new("Reddit", "Public posts search", $"https://www.reddit.com/search/?q={e}"),
            new("Common Crawl", "Public web crawl index information", "https://index.commoncrawl.org/"),
            new("Google Patents", "Patent search", $"https://patents.google.com/?q={e}"),
            new("Google Scholar", "Scholarly search", $"https://scholar.google.com/scholar?q={e}"),
            new("GDELT", "Global news and events data", $"https://www.gdeltproject.org/"),
            new("ICANN Lookup", "Domain registration lookup", $"https://lookup.icann.org/en/lookup?name={e}"),
            new("crt.sh", "Public certificate-transparency records", $"https://crt.sh/?q={e}"),
            new("urlscan.io", "Public website scan search", $"https://urlscan.io/search/#q={e}"),
            new("DNS over HTTPS", "Google public DNS query", $"https://dns.google/resolve?name={e}&type=ANY")
        };
        if (type == "Phone number")
        {
            // Public listings only. This intentionally avoids private-person reverse lookup/data-broker services.
            return new List<SearchLink>
            {
                new("Google exact phone search", "Search public pages for this exact number", $"https://www.google.com/search?q=%22{e}%22"),
                new("Bing exact phone search", "Search public pages for this exact number", $"https://www.bing.com/search?q=%22{e}%22"),
                new("DuckDuckGo exact phone search", "Search public pages for this exact number", $"https://duckduckgo.com/?q=%22{e}%22"),
                new("Brave Search", "Search publicly indexed pages", $"https://search.brave.com/search?q=%22{e}%22"),
                new("Google Maps public listings", "Look for businesses that publish this contact number", $"https://www.google.com/maps/search/{e}"),
                new("Google business/contact context", "Search public business or official contact pages", $"https://www.google.com/search?q={e}+business+OR+official+contact")
            };
        }
        if (type == "Email address")
        {
            return new List<SearchLink>
            {
                new("Google exact email search", "Search publicly indexed organization/contact pages", $"https://www.google.com/search?q=%22{e}%22"),
                new("Bing exact email search", "Search publicly indexed organization/contact pages", $"https://www.bing.com/search?q=%22{e}%22"),
                new("DuckDuckGo exact email search", "Search publicly indexed organization/contact pages", $"https://duckduckgo.com/?q=%22{e}%22"),
                new("GitHub public code search", "Search public repositories for an address", $"https://github.com/search?q={e}&type=code"),
                new("Google public contact pages", "Search public organization contact pages", $"https://www.google.com/search?q={e}+contact")
            };
        }
        if (type == "IP address")
        {
            var ip = Uri.EscapeDataString(q);
            return new List<SearchLink>
            {
                new("RIPEstat", "Public IP / ASN registration and routing data", $"https://stat.ripe.net/{ip}"),
                new("ARIN RDAP", "Public internet number registry", $"https://search.arin.net/rdap/?query={ip}"),
                new("Google DNS", "Public DNS resolver query", $"https://dns.google/resolve?name={ip}&type=PTR"),
                new("BGP.tools", "Public routing and ASN information", $"https://bgp.tools/prefix/{ip}"),
                new("Google exact IP search", "Search public pages mentioning the address", $"https://www.google.com/search?q=%22{ip}%22"),
                new("Bing exact IP search", "Search public pages mentioning the address", $"https://www.bing.com/search?q=%22{ip}%22")
            };
        }
        if (type == "Domain / website")
        {
            var d = NormalizeDomain(q); var de = Uri.EscapeDataString(d);
            list.AddRange(new[] {
                new SearchLink("Certificate Transparency", "Search certificates for domain and subdomains", $"https://crt.sh/?q=%25.{de}"),
                new SearchLink("urlscan.io domain", "Search public scan metadata", $"https://urlscan.io/search/#domain:{de}"),
                new SearchLink("ICANN RDAP", "Registration data", $"https://lookup.icann.org/en/lookup?name={de}"),
                new SearchLink("DNS records", "Public DNS-over-HTTPS response", $"https://dns.google/resolve?name={de}&type=ANY"),
                new SearchLink("Wayback Machine", "Archived public website captures", $"https://web.archive.org/web/*/{de}")
            });
        }
        if (type == "Username / handle")
            list.AddRange(new[] {
                new SearchLink("GitHub users", "Public profile search", $"https://github.com/search?q={e}&type=users"),
                new SearchLink("Mastodon search", "Search public web results for handle", $"https://www.google.com/search?q=%22{e}%22+site%3Amastodon.social"),
                new SearchLink("Reddit", "Public posts and profile mentions", $"https://www.reddit.com/search/?q={e}")
            });

        // The catalogue creates source-specific public search links. These are link modules,
        // not claims that all listed sites have a live API adapter.
        list.AddRange(SourceCatalogService.Build(q, language));
        return list
            .GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public static async Task<LiveSearchReport> SearchLiveAsync(string type, string raw, CancellationToken token = default, string? language = null)
    {
        var q = raw.Trim();
        if (type is "Phone number" or "Email address")
        {
            var links = Build(type, q, language);
            var rows = links.Select(link => new LiveSearchResult(link.Name, link.Purpose,
                LocalizationService.Translate("Search link only; open it to search public listings. No private-person lookup is performed.", language), link.Url)).ToList();
            return new LiveSearchReport(rows, new List<ProviderStatus>
            {
                new("Public search links", "Links only", rows.Count, "No live API is queried for this input type")
            });
        }
        if (type == "IP address")
        {
            var ip = q.Trim();
            if (!System.Net.IPAddress.TryParse(ip, out _))
                throw new ArgumentException(LocalizationService.Translate("Enter a valid IPv4 or IPv6 address.", language));
            var links = Build(type, ip, language);
            var rows = links.Select(link => new LiveSearchResult(link.Name, link.Purpose,
                LocalizationService.Translate("Public-source link; open it to inspect the result.", language), link.Url)).ToList();
            return new LiveSearchReport(rows, new List<ProviderStatus>
            {
                new("Public IP/ASN sources", "Links only", rows.Count, "Use registry pages to inspect public infrastructure data")
            });
        }
        var jobs = new List<Task<(List<LiveSearchResult> Results, ProviderStatus Status)>>
        {
            RunProvider("Wikipedia", token, () => SearchWikipedia(q, token)),
            RunProvider("Crossref", token, () => SearchCrossref(q, token)),
            RunProvider("OpenAlex", token, () => SearchOpenAlex(q, token)),
            RunProvider("Hacker News", token, () => SearchHackerNews(q, token)),
            RunProvider("Internet Archive", token, () => SearchArchive(q, token))
        };
        if (type == "Username / handle")
            jobs.Add(RunProvider("Public username profiles", token, () => UsernameProfileService.SearchAsync(q, token, language)));
        if (type == "Domain / website")
        {
            var domain = NormalizeDomain(q);
            jobs.Add(RunProvider("crt.sh", token, () => SearchCertificates(domain, token)));
            jobs.Add(RunProvider("DNS", token, () => SearchDns(domain, token)));
        }
        if (type == "Keywords" || type == "Public organization")
            jobs.Add(RunProvider("GitHub repositories", token, () => SearchGitHub(q, token)));

        var completed = await Task.WhenAll(jobs);
        var results = completed.SelectMany(x => x.Results).GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First()).Take(100).ToList();
        return new LiveSearchReport(results, completed.Select(x => x.Status).ToList());
    }

    private static async Task<(List<LiveSearchResult>, ProviderStatus)> RunProvider(string name, CancellationToken token, Func<Task<List<LiveSearchResult>>> action)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            var rows = await action();
            token.ThrowIfCancellationRequested();
            return (rows, new ProviderStatus(name, "OK", rows.Count, rows.Count == 0 ? "No matches returned" : "Live response received"));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Preserve explicit user cancellation instead of silently reporting an empty result set.
            throw;
        }
        catch (OperationCanceledException) { return (new(), new ProviderStatus(name, "Skipped", 0, "Provider request timed out")); }
        catch (Exception ex) { return (new(), new ProviderStatus(name, "Unavailable", 0, SafeError(ex))); }
    }
    private static string SafeError(Exception ex) => ex is HttpRequestException ? "Network/API unavailable or rate limited" : ex is TaskCanceledException ? "Timed out" : "Unexpected response format";
    private static async Task<JsonDocument> GetJson(string url, CancellationToken token)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        return await JsonDocument.ParseAsync(stream, cancellationToken: token);
    }
    private static string S(JsonElement el, string name) => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
    private static string Trim(string s, int max = 280) => string.IsNullOrWhiteSpace(s) ? "" : s.Length <= max ? s : s[..max] + "…";
    private static string NormalizeDomain(string value)
    {
        var input = value.Trim(); if (!input.Contains("://", StringComparison.Ordinal)) input = "https://" + input;
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri)) return uri.Host.TrimEnd('.');
        return value.Trim().Split('/')[0];
    }
    private static async Task<List<LiveSearchResult>> SearchWikipedia(string q, CancellationToken t)
    {
        var url = "https://en.wikipedia.org/w/api.php?action=query&list=search&format=json&utf8=1&srlimit=8&srsearch=" + Uri.EscapeDataString(q);
        using var doc = await GetJson(url, t); var rows = new List<LiveSearchResult>();
        if (doc.RootElement.GetProperty("query").TryGetProperty("search", out var arr)) foreach (var x in arr.EnumerateArray())
        { var title = S(x, "title"); var page = "https://en.wikipedia.org/wiki/" + Uri.EscapeDataString(title.Replace(' ', '_')); rows.Add(new("Wikipedia", title, Trim(System.Text.RegularExpressions.Regex.Replace(S(x, "snippet"), "<.*?>", "")), page)); }
        return rows;
    }
    private static async Task<List<LiveSearchResult>> SearchCrossref(string q, CancellationToken t)
    {
        using var doc = await GetJson($"https://api.crossref.org/works?rows=8&query={Uri.EscapeDataString(q)}", t); var rows = new List<LiveSearchResult>();
        foreach (var x in doc.RootElement.GetProperty("message").GetProperty("items").EnumerateArray())
        { var title = x.TryGetProperty("title", out var ts) && ts.GetArrayLength() > 0 ? ts[0].GetString() ?? "Untitled work" : "Untitled work"; var url = S(x, "URL"); if (url.Length > 0) rows.Add(new("Crossref", title, Trim(S(x, "publisher")), url)); }
        return rows;
    }
    private static async Task<List<LiveSearchResult>> SearchOpenAlex(string q, CancellationToken t)
    {
        using var doc = await GetJson($"https://api.openalex.org/works?per-page=8&search={Uri.EscapeDataString(q)}", t); var rows = new List<LiveSearchResult>();
        foreach (var x in doc.RootElement.GetProperty("results").EnumerateArray()) { var title = S(x, "title"); var url = S(x, "id"); if (title.Length > 0 && url.Length > 0) rows.Add(new("OpenAlex", title, "Open scholarly metadata", url)); }
        return rows;
    }
    private static async Task<List<LiveSearchResult>> SearchHackerNews(string q, CancellationToken t)
    {
        using var doc = await GetJson($"https://hn.algolia.com/api/v1/search?hitsPerPage=8&query={Uri.EscapeDataString(q)}", t); var rows = new List<LiveSearchResult>();
        foreach (var x in doc.RootElement.GetProperty("hits").EnumerateArray()) { var title = S(x, "title"); if (title.Length == 0) title = S(x, "story_title"); var url = S(x, "url"); if (url.Length == 0) url = "https://news.ycombinator.com/item?id=" + S(x, "objectID"); if (title.Length > 0) rows.Add(new("Hacker News", title, Trim(S(x, "author")), url)); }
        return rows;
    }
    private static async Task<List<LiveSearchResult>> SearchArchive(string q, CancellationToken t)
    {
        using var doc = await GetJson($"https://archive.org/advancedsearch.php?q={Uri.EscapeDataString(q)}&fl[]=identifier,title,description&rows=8&page=1&output=json", t); var rows = new List<LiveSearchResult>();
        foreach (var x in doc.RootElement.GetProperty("response").GetProperty("docs").EnumerateArray()) { var id = S(x, "identifier"); if (id.Length > 0) rows.Add(new("Internet Archive", S(x, "title") is { Length: > 0 } title ? title : id, Trim(S(x, "description")), "https://archive.org/details/" + Uri.EscapeDataString(id))); }
        return rows;
    }
    private static async Task<List<LiveSearchResult>> SearchGitHub(string q, CancellationToken t)
    {
        using var doc = await GetJson($"https://api.github.com/search/repositories?per_page=8&sort=updated&q={Uri.EscapeDataString(q)}", t); var rows = new List<LiveSearchResult>();
        foreach (var x in doc.RootElement.GetProperty("items").EnumerateArray()) { var name = S(x, "full_name"); var url = S(x, "html_url"); if (name.Length > 0 && url.Length > 0) rows.Add(new("GitHub", name, Trim(S(x, "description")), url)); }
        return rows;
    }
    private static async Task<List<LiveSearchResult>> SearchCertificates(string domain, CancellationToken t)
    {
        using var doc = await GetJson($"https://crt.sh/?q={Uri.EscapeDataString(domain)}&output=json", t); var rows = new List<LiveSearchResult>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var x in doc.RootElement.EnumerateArray()) { var name = S(x, "name_value").Replace("\n", ", "); if (name.Length > 0 && seen.Add(name)) rows.Add(new("crt.sh", name, "Public certificate transparency entry", "https://crt.sh/?q=" + Uri.EscapeDataString(name))); if (rows.Count >= 30) break; }
        return rows;
    }
    private static async Task<List<LiveSearchResult>> SearchDns(string domain, CancellationToken t)
    {
        using var doc = await GetJson($"https://dns.google/resolve?name={Uri.EscapeDataString(domain)}&type=A", t); var rows = new List<LiveSearchResult>();
        if (doc.RootElement.TryGetProperty("Answer", out var answers)) foreach (var x in answers.EnumerateArray()) rows.Add(new("DNS", S(x, "name"), $"Type {x.GetProperty("type").GetInt32()} · {S(x, "data")}", $"https://dns.google/resolve?name={Uri.EscapeDataString(domain)}&type=A"));
        if (rows.Count == 0) rows.Add(new("DNS", domain, "No A record returned by this resolver", $"https://dns.google/resolve?name={Uri.EscapeDataString(domain)}&type=ANY"));
        return rows;
    }
}
