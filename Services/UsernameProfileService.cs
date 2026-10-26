using System.Net;
using System.Net.Http;

namespace HamTool.Services;

/// <summary>
/// Checks only public profile URLs. HTTP responses are leads, not proof of identity.
/// No login, CAPTCHA/WAF bypass, proxy rotation, or private-data collection is performed.
/// </summary>
public static class UsernameProfileService
{
    private sealed record Site(string Name, string UrlTemplate);

    // Keep this list conservative and editable. A URL returning HTTP 200 is only a candidate;
    // many sites return 200 for login pages, soft-404 pages, or bot challenges.
    private static readonly Site[] Sites =
    [
        new("GitHub", "https://github.com/{0}"),
        new("GitLab", "https://gitlab.com/{0}"),
        new("Reddit", "https://www.reddit.com/user/{0}/"),
        new("Twitch", "https://www.twitch.tv/{0}"),
        new("Pinterest", "https://www.pinterest.com/{0}/"),
        new("Medium", "https://medium.com/@{0}"),
        new("SoundCloud", "https://soundcloud.com/{0}"),
        new("DeviantArt", "https://www.deviantart.com/{0}"),
        new("Behance", "https://www.behance.net/{0}"),
        new("Codeberg", "https://codeberg.org/{0}"),
        new("Hacker News", "https://news.ycombinator.com/user?id={0}"),
        new("Instagram", "https://www.instagram.com/{0}/"),
        new("TikTok", "https://www.tiktok.com/@{0}"),
        new("X", "https://x.com/{0}"),
        new("YouTube", "https://www.youtube.com/@{0}"),
        new("Steam Community", "https://steamcommunity.com/id/{0}/")
    ];

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 5 };
        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "HamTool-Pro/2.1 (public profile URL checks; manual verification required)");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9,*/*;q=0.8");
        return client;
    }

    public static async Task<List<LiveSearchResult>> SearchAsync(string rawUsername, CancellationToken token = default, string? language = null)
    {
        var username = rawUsername.Trim().TrimStart('@');
        if (username.Length is < 1 or > 64 || username.Any(char.IsWhiteSpace))
            throw new ArgumentException(LocalizationService.Translate("Enter one username without spaces.", language), nameof(rawUsername));

        // Avoid path/query injection: encode the handle before inserting it into URL templates.
        var encoded = Uri.EscapeDataString(username);
        using var gate = new SemaphoreSlim(6, 6);
        var tasks = Sites.Select(async site =>
        {
            await gate.WaitAsync(token);
            try
            {
                var url = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    site.UrlTemplate, encoded);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(8));
                try
                {
                    using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                    var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;
                    var status = (int)response.StatusCode;
                    string summary;
                    if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone)
                        summary = LocalizationService.Translate("Not found by HTTP status {0}; this is not definitive.", language).Replace("{0}", status.ToString());
                    else if (response.IsSuccessStatusCode)
                        summary = LocalizationService.Translate("Candidate only (HTTP {0}). Open the page and verify manually; HTTP success does not confirm an account.", language).Replace("{0}", status.ToString());
                    else if (status is 401 or 403 or 429)
                        summary = LocalizationService.Translate("Could not verify (HTTP {0}; access restricted or rate-limited).", language).Replace("{0}", status.ToString());
                    else
                        summary = LocalizationService.Translate("Could not verify (HTTP {0}).", language).Replace("{0}", status.ToString());

                    return new LiveSearchResult(site.Name, username, summary, finalUrl);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    return new LiveSearchResult(site.Name, username, "Could not verify (request timed out).", url);
                }
                catch (HttpRequestException)
                {
                    return new LiveSearchResult(site.Name, username, LocalizationService.Translate("Could not verify (network or site error).", language), url);
                }
            }
            finally { gate.Release(); }
        });

        return (await Task.WhenAll(tasks)).ToList();
    }
}
