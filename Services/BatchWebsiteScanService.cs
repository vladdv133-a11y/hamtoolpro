using System.Net;
using System.Net.Http;

namespace HamTool.Services;

/// <summary>
/// Low-impact availability check for a user-supplied list of public website URLs.
/// It performs one GET per URL, reads headers only, limits concurrency, and does not crawl
/// paths, probe vulnerabilities, authenticate, or bypass access controls.
/// </summary>
public static class BatchWebsiteScanService
{
    public const int MaximumUrls = 5000;
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HamTool-Pro/2.2 (public website availability checks)");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9,*/*;q=0.5");
        return client;
    }

    public static List<Uri> ParseUrls(string input)
    {
        var lines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var urls = new List<Uri>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            if (urls.Count > MaximumUrls) break;
            if (line.StartsWith("#", StringComparison.Ordinal)) continue;
            var candidate = line.Contains("://", StringComparison.Ordinal) ? line : "https://" + line;
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)) continue;
            if (uri.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(uri.Host)) continue;
            // This mode is for public websites. Reject bare labels such as "vd-40" which
            // are not public DNS names; ask the user for a complete domain like example.com.
            if (!IPAddress.TryParse(uri.Host, out _) && !uri.IdnHost.Contains('.', StringComparison.Ordinal)) continue;
            if (uri.UserInfo.Length > 0) continue;
            if ((uri.Scheme == "http" && uri.Port != 80) || (uri.Scheme == "https" && uri.Port != 443)) continue;
            if (IsClearlyLocalHost(uri.IdnHost)) continue;
            var normalized = uri.GetLeftPart(UriPartial.Path).TrimEnd('/') + uri.Query;
            if (seen.Add(normalized)) urls.Add(uri);
        }
        return urls;
    }

    private static bool IsClearlyLocalHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)) return true;
        if (!IPAddress.TryParse(host, out var ip)) return false;
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 10 || b[0] == 127 || (b[0] == 169 && b[1] == 254) || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || b[0] == 0;
        }
        return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast || ip.Equals(IPAddress.IPv6None) || ip.Equals(IPAddress.IPv6Any);
    }

    public static async Task<LiveSearchReport> ScanAsync(IReadOnlyList<Uri> urls, CancellationToken cancellationToken, string? language = null, IProgress<int>? progress = null)
    {
        if (urls.Count > MaximumUrls) throw new ArgumentOutOfRangeException(nameof(urls), $"A maximum of {MaximumUrls} URLs is supported.");
        using var gate = new SemaphoreSlim(10, 10);
        var completedCount = 0;
        var tasks = urls.Select(async uri =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var perRequest = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                perRequest.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, perRequest.Token).ConfigureAwait(false);
                    var status = (int)response.StatusCode;
                    var summaryKey = response.IsSuccessStatusCode
                        ? "HTTP {0} — response received; page content was not analyzed."
                        : "HTTP {0} — server responded; this alone does not prove the site is broken.";
                    var summary = LocalizationService.Translate(summaryKey, language).Replace("{0}", status.ToString());
                    return new LiveSearchResult(LocalizationService.Translate("Website availability", language), uri.Host, summary, response.RequestMessage?.RequestUri?.AbsoluteUri ?? uri.AbsoluteUri);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return new LiveSearchResult(LocalizationService.Translate("Website availability", language), uri.Host, LocalizationService.Translate("Timed out after 5 seconds.", language), uri.AbsoluteUri);
                }
                catch (HttpRequestException ex)
                {
                    var reason = ex.StatusCode is null
                        ? LocalizationService.Translate("Network, TLS, DNS, or connection error.", language)
                        : LocalizationService.Translate("HTTP {0} response.", language).Replace("{0}", ((int)ex.StatusCode.Value).ToString());
                    return new LiveSearchResult(LocalizationService.Translate("Website availability", language), uri.Host, reason, uri.AbsoluteUri);
                }
            }
            finally
            {
                gate.Release();
                var completed = Interlocked.Increment(ref completedCount);
                progress?.Report(completed);
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var reachable = results.Count(x => x.Summary.StartsWith("HTTP ", StringComparison.Ordinal));
        var provider = new ProviderStatus(LocalizationService.Translate("Batch website availability", language), "OK", results.Length,
            LocalizationService.Translate("Checked {0} URL(s); {1} returned HTTP responses. Availability only; not a vulnerability scan.", language)
                .Replace("{0}", results.Length.ToString()).Replace("{1}", reachable.ToString()));
        return new LiveSearchReport(results.ToList(), new List<ProviderStatus> { provider });
    }
}
