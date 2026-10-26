using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace HamTool.Services;

/// <summary>
/// Curated directory of public-source search modules. These modules generate a search link;
/// they are not represented as live API connectors unless implemented separately in PublicSearchService.
/// </summary>
public static class SourceCatalogService
{
    private sealed record Module(string Name, string Domain, string Purpose, string RecipeKey = "general")
    {
        public string Category => Classify(Name, Domain, Purpose);
    }

    private sealed record SearchRecipe(string Key, string Name, string Description, Func<string, string> Apply);

    // These are search recipes over curated public sources, not independent live API integrations.
    // The catalog exposes 5,000 unique source + recipe combinations, while preserving the curated seed list.
    private static readonly SearchRecipe[] Recipes =
    [
        new("general", "General query", "general query", q => q),
        new("exact", "Exact phrase", "exact phrase", q => $"\"{q}\""),
        new("title", "Page title", "title-focused query", q => $"intitle:\"{q}\""),
        new("url", "URL keywords", "URL keyword query", q => $"inurl:{q}"),
        new("pdf", "PDF documents", "PDF document search", q => $"\"{q}\" filetype:pdf"),
        new("docx", "Word documents", "Word document search", q => $"\"{q}\" filetype:docx"),
        new("xlsx", "Excel spreadsheets", "spreadsheet search", q => $"\"{q}\" filetype:xlsx"),
        new("csv", "CSV datasets", "CSV dataset search", q => $"\"{q}\" filetype:csv"),
        new("pptx", "Presentations", "presentation search", q => $"\"{q}\" filetype:pptx"),
        new("intext", "Page text", "page text query", q => $"intext:\"{q}\""),
        new("contact", "Public contacts", "public contact-page search", q => $"\"{q}\" contact"),
        new("about", "About pages", "public about-page search", q => $"\"{q}\" about"),
        new("docs", "Documentation", "documentation search", q => $"\"{q}\" documentation OR docs"),
        new("policy", "Policies", "public policy-page search", q => $"\"{q}\" policy OR terms"),
        new("press", "Press releases", "press-release search", q => $"\"{q}\" press OR news"),
        new("careers", "Careers pages", "public careers-page search", q => $"\"{q}\" careers OR jobs"),
        new("github", "GitHub mentions", "public GitHub mention search", q => $"\"{q}\" github"),
        new("news", "News context", "news-context search", q => $"\"{q}\" news"),
        new("recent", "Recent mentions", "recently dated public-page search", q => $"\"{q}\" after:{DateTime.UtcNow.AddYears(-1):yyyy-MM-dd}"),
        new("older", "Historical mentions", "older public-page search", q => $"\"{q}\" before:{DateTime.UtcNow.AddYears(-1):yyyy-MM-dd}"),
        new("exclude_jobs", "Exclude job listings", "query excluding job listings", q => $"\"{q}\" -jobs -careers"),
        new("exclude_ads", "Exclude advertisements", "query excluding advertisements", q => $"\"{q}\" -ads -sponsored"),
        new("definition", "Definitions", "definition and reference search", q => $"define:{q}"),
        new("json", "JSON resources", "JSON resource search", q => $"\"{q}\" filetype:json"),
        new("xml", "XML resources", "XML resource search", q => $"\"{q}\" filetype:xml"),
        new("txt", "Text files", "plain-text file search", q => $"\"{q}\" filetype:txt"),
        new("markdown", "Markdown files", "Markdown file search", q => $"\"{q}\" filetype:md")
    ];

    private static string Classify(string name, string domain, string purpose)
    {
        var text = $"{name} {domain} {purpose}".ToLowerInvariant();
        if (text.Contains("social") || text.Contains("profile") || text.Contains("reddit") || text.Contains("instagram") || text.Contains("tiktok") || text.Contains("youtube") || text.Contains("twitch") || text.Contains("mastodon") || text.Contains("bluesky")) return "Social & public profiles";
        if (text.Contains("code") || text.Contains("github") || text.Contains("gitlab") || text.Contains("package") || text.Contains("developer") || text.Contains("stack overflow") || text.Contains("npm") || text.Contains("pypi")) return "Code & developer communities";
        if (text.Contains("research") || text.Contains("scholarly") || text.Contains("publication") || text.Contains("academic") || text.Contains("arxiv") || text.Contains("pubmed") || text.Contains("crossref") || text.Contains("openalex")) return "Research & academia";
        if (text.Contains("news") || text.Contains("journalism") || text.Contains("investigative") || text.Contains("media literacy")) return "News & investigations";
        if (text.Contains("domain") || text.Contains("dns") || text.Contains("network") || text.Contains("ip ") || text.Contains("certificate") || text.Contains("routing") || text.Contains("rdap") || text.Contains("whois")) return "Domains & internet infrastructure";
        if (text.Contains("vulnerability") || text.Contains("malware") || text.Contains("phishing") || text.Contains("security") || text.Contains("threat") || text.Contains("cve")) return "Cybersecurity references";
        if (text.Contains("government") || text.Contains("public data") || text.Contains("company") || text.Contains("corporate") || text.Contains("procurement") || text.Contains("registry") || text.Contains("sanctions") || text.Contains("financial")) return "Government, companies & public records";
        if (text.Contains("map") || text.Contains("geographic") || text.Contains("earth observation") || text.Contains("geospatial")) return "Maps & geospatial data";
        if (text.Contains("image") || text.Contains("photo") || text.Contains("video") || text.Contains("media catalog") || text.Contains("music") || text.Contains("film")) return "Images, video & media";
        if (text.Contains("book") || text.Contains("library") || text.Contains("culture") || text.Contains("encyclopedia") || text.Contains("knowledge")) return "Books, culture & reference";
        if (text.Contains("product") || text.Contains("economic") || text.Contains("dataset") || text.Contains("data catalog")) return "Products & open datasets";
        if (text.Contains("archive") || text.Contains("crawl") || text.Contains("wayback")) return "Web archives & historical data";
        return "General web search";
    }

    private static readonly Module[] SeedModules =
    [
        new("Google Search", "google.com", "General web search"),
        new("Bing Search", "bing.com", "General web search"),
        new("DuckDuckGo", "duckduckgo.com", "General web search"),
        new("Brave Search", "search.brave.com", "General web search"),
        new("Mojeek", "mojeek.com", "Independent web search"),
        new("Yahoo Search", "search.yahoo.com", "General web search"),
        new("Yandex Search", "yandex.com", "General web search"),
        new("Ecosia", "ecosia.org", "General web search"),
        new("Qwant", "qwant.com", "General web search"),
        new("Startpage", "startpage.com", "Privacy-oriented web search"),
        new("Wikipedia", "wikipedia.org", "Encyclopedia"),
        new("Wikidata", "wikidata.org", "Structured public knowledge"),
        new("Wikimedia Commons", "commons.wikimedia.org", "Public media library"),
        new("Internet Archive", "archive.org", "Web and media archive"),
        new("Wayback Machine", "web.archive.org", "Archived public pages"),
        new("Common Crawl", "commoncrawl.org", "Public web crawl data"),
        new("Google Books", "books.google.com", "Books and references"),
        new("Open Library", "openlibrary.org", "Book catalog"),
        new("WorldCat", "worldcat.org", "Library catalog"),
        new("Europeana", "europeana.eu", "European cultural collections"),
        new("GitHub", "github.com", "Public repositories and profiles"),
        new("GitLab", "gitlab.com", "Public repositories"),
        new("Codeberg", "codeberg.org", "Public repositories"),
        new("SourceHut", "sr.ht", "Public code hosting"),
        new("Bitbucket", "bitbucket.org", "Public repositories"),
        new("Gitea Explore", "gitea.com", "Public code hosting"),
        new("Stack Overflow", "stackoverflow.com", "Public technical Q&A"),
        new("Stack Exchange", "stackexchange.com", "Public technical communities"),
        new("Super User", "superuser.com", "Public technical Q&A"),
        new("Server Fault", "serverfault.com", "Public systems Q&A"),
        new("Hacker News", "news.ycombinator.com", "Public technology discussion"),
        new("Lobsters", "lobste.rs", "Public technology discussion"),
        new("DEV Community", "dev.to", "Public developer articles"),
        new("Hashnode", "hashnode.com", "Public developer blogs"),
        new("CodePen", "codepen.io", "Public code demos"),
        new("JSFiddle", "jsfiddle.net", "Public code demos"),
        new("Replit", "replit.com", "Public code projects"),
        new("npm", "npmjs.com", "Public package metadata"),
        new("PyPI", "pypi.org", "Public Python packages"),
        new("crates.io", "crates.io", "Public Rust packages"),
        new("Maven Central", "search.maven.org", "Public Java packages"),
        new("Reddit", "reddit.com", "Public discussions"),
        new("Mastodon", "mastodon.social", "Public social web search"),
        new("Bluesky", "bsky.app", "Public social posts"),
        new("Threads", "threads.net", "Public social profiles"),
        new("Instagram", "instagram.com", "Public profile discovery"),
        new("TikTok", "tiktok.com", "Public profile discovery"),
        new("YouTube", "youtube.com", "Public channels and videos"),
        new("Twitch", "twitch.tv", "Public streaming profiles"),
        new("Pinterest", "pinterest.com", "Public boards and profiles"),
        new("Tumblr", "tumblr.com", "Public blogs"),
        new("Medium", "medium.com", "Public articles"),
        new("Substack", "substack.com", "Public newsletters"),
        new("SoundCloud", "soundcloud.com", "Public audio profiles"),
        new("Bandcamp", "bandcamp.com", "Public music pages"),
        new("DeviantArt", "deviantart.com", "Public art profiles"),
        new("Behance", "behance.net", "Public creative portfolios"),
        new("Dribbble", "dribbble.com", "Public design portfolios"),
        new("ArtStation", "artstation.com", "Public creative portfolios"),
        new("Flickr", "flickr.com", "Public photo pages"),
        new("Vimeo", "vimeo.com", "Public video pages"),
        new("Dailymotion", "dailymotion.com", "Public video pages"),
        new("Goodreads", "goodreads.com", "Public book profiles"),
        new("Letterboxd", "letterboxd.com", "Public film profiles"),
        new("Last.fm", "last.fm", "Public music profiles"),
        new("Steam Community", "steamcommunity.com", "Public gaming profiles"),
        new("Chess.com", "chess.com", "Public chess profiles"),
        new("Strava", "strava.com", "Public activity pages"),
        new("Komoot", "komoot.com", "Public route pages"),
        new("Product Hunt", "producthunt.com", "Public product launches"),
        new("Hugging Face", "huggingface.co", "Public AI models and profiles"),
        new("Kaggle", "kaggle.com", "Public data science profiles"),
        new("OpenAlex", "openalex.org", "Scholarly works and authors"),
        new("Crossref", "crossref.org", "Publication metadata"),
        new("Google Scholar", "scholar.google.com", "Scholarly search"),
        new("Semantic Scholar", "semanticscholar.org", "Scholarly literature"),
        new("PubMed", "pubmed.ncbi.nlm.nih.gov", "Biomedical publications"),
        new("Europe PMC", "europepmc.org", "Life-sciences publications"),
        new("DOAJ", "doaj.org", "Open-access journals"),
        new("CORE", "core.ac.uk", "Open-access research"),
        new("arXiv", "arxiv.org", "Research preprints"),
        new("bioRxiv", "biorxiv.org", "Biology preprints"),
        new("medRxiv", "medrxiv.org", "Medical preprints"),
        new("Zenodo", "zenodo.org", "Research data and papers"),
        new("OSF", "osf.io", "Open research projects"),
        new("ORCID", "orcid.org", "Public researcher identifiers"),
        new("DBLP", "dblp.org", "Computer science bibliography"),
        new("Google Patents", "patents.google.com", "Public patent search"),
        new("Lens", "lens.org", "Public scholarly and patent search"),
        new("OpenAIRE", "openaire.eu", "Open research records"),
        new("NASA ADS", "ui.adsabs.harvard.edu", "Astronomy publications"),
        new("ERIC", "eric.ed.gov", "Education research"),
        new("GDELT", "gdeltproject.org", "Global news and events data"),
        new("Google News", "news.google.com", "Public news search"),
        new("Bing News", "bing.com/news", "Public news search"),
        new("Reuters", "reuters.com", "News reporting"),
        new("Associated Press", "apnews.com", "News reporting"),
        new("BBC", "bbc.com", "News reporting"),
        new("The Guardian", "theguardian.com", "News reporting"),
        new("NPR", "npr.org", "News reporting"),
        new("DW", "dw.com", "International news"),
        new("France 24", "france24.com", "International news"),
        new("Deutsche Welle", "dw.com", "International news"),
        new("Kyiv Independent", "kyivindependent.com", "Ukrainian news"),
        new("Ukrainska Pravda", "pravda.com.ua", "Ukrainian news"),
        new("Suspilne", "suspilne.media", "Ukrainian public broadcaster"),
        new("Bellingcat", "bellingcat.com", "Open-source investigations"),
        new("OCCRP", "occrp.org", "Public-interest investigations"),
        new("ICIJ", "icij.org", "Investigative journalism"),
        new("ProPublica", "propublica.org", "Investigative journalism"),
        new("Poynter", "poynter.org", "Media research"),
        new("NewsGuard", "newsguardtech.com", "Media literacy research"),
        new("ICANN Lookup", "lookup.icann.org", "Domain registration lookup"),
        new("RDAP", "rdap.org", "Public registration data"),
        new("crt.sh", "crt.sh", "Certificate transparency"),
        new("urlscan.io", "urlscan.io", "Public website scan metadata"),
        new("DNS Google", "dns.google", "Public DNS records"),
        new("Cloudflare Radar", "radar.cloudflare.com", "Public internet measurements"),
        new("RIPEstat", "stat.ripe.net", "Public network registration data"),
        new("RIPE Database", "ripe.net", "Public network registry"),
        new("ARIN Whois", "arin.net", "Public IP and ASN registry"),
        new("APNIC Whois", "apnic.net", "Public IP and ASN registry"),
        new("LACNIC", "lacnic.net", "Public IP and ASN registry"),
        new("AFRINIC", "afrinic.net", "Public IP and ASN registry"),
        new("BGP.Tools", "bgp.tools", "Public routing information"),
        new("Hurricane Electric BGP", "bgp.he.net", "Public routing information"),
        new("PeeringDB", "peeringdb.com", "Public network interconnection data"),
        new("Internet.nl", "internet.nl", "Public internet configuration checks"),
        new("SecurityTrails", "securitytrails.com", "Public domain intelligence search"),
        new("VirusTotal", "virustotal.com", "Public URL and file reputation"),
        new("AlienVault OTX", "otx.alienvault.com", "Public threat indicators"),
        new("AbuseIPDB", "abuseipdb.com", "Public IP reputation reports"),
        new("URLhaus", "urlhaus.abuse.ch", "Public malicious URL reports"),
        new("MalwareBazaar", "bazaar.abuse.ch", "Public malware sample metadata"),
        new("PhishTank", "phishtank.org", "Public phishing reports"),
        new("CISA KEV", "cisa.gov", "Known exploited vulnerability catalog"),
        new("NVD", "nvd.nist.gov", "Public vulnerability records"),
        new("CVE.org", "cve.org", "Public vulnerability identifiers"),
        new("OSV", "osv.dev", "Open-source vulnerability database"),
        new("GitHub Advisories", "github.com/advisories", "Public software security advisories"),
        new("Wikimedia Maps", "wikimedia.org", "Public reference material"),
        new("OpenStreetMap", "openstreetmap.org", "Open map data"),
        new("OpenStreetMap Wiki", "wiki.openstreetmap.org", "Map documentation"),
        new("Geonames", "geonames.org", "Public geographic names"),
        new("USGS", "usgs.gov", "Public geographic and science data"),
        new("NASA Earthdata", "earthdata.nasa.gov", "Public Earth observation data"),
        new("Copernicus", "dataspace.copernicus.eu", "Public Earth observation data"),
        new("OpenSanctions", "opensanctions.org", "Public sanctions and PEP datasets"),
        new("OpenCorporates", "opencorporates.com", "Public company registry index"),
        new("Companies House", "find-and-update.company-information.service.gov.uk", "UK company records"),
        new("SEC EDGAR", "sec.gov/edgar", "Public US company filings"),
        new("GLEIF", "gleif.org", "Legal entity identifiers"),
        new("World Bank Data", "data.worldbank.org", "Public economic data"),
        new("OECD Data", "oecd.org", "Public economic data"),
        new("UN Data", "data.un.org", "Public international datasets"),
        new("EU Open Data", "data.europa.eu", "European public datasets"),
        new("Data.gov", "data.gov", "US public datasets"),
        new("Data.gov.uk", "data.gov.uk", "UK public datasets"),
        new("Gov.uk", "gov.uk", "UK public information"),
        new("EU WhoisWHO", "op.europa.eu", "Public EU organization information"),
        new("Open Government Ukraine", "data.gov.ua", "Ukrainian public datasets"),
        new("YouControl public search", "youcontrol.com.ua", "Public company information search"),
        new("Clarity Project", "clarity-project.info", "Public procurement and company information"),
        new("Prozorro", "prozorro.gov.ua", "Public procurement data"),
        new("Dozorro", "dozorro.org", "Public procurement monitoring"),
        new("Open Budget Ukraine", "openbudget.gov.ua", "Public budget data"),
        new("NBU", "bank.gov.ua", "Ukrainian public financial data"),
        new("State Statistics Ukraine", "ukrstat.gov.ua", "Public statistical data"),
        new("Ukrainian Parliament", "rada.gov.ua", "Public legislative information"),
        new("Court Register Ukraine", "reyestr.court.gov.ua", "Public court decisions"),
        new("Google Images", "images.google.com", "Public image search"),
        new("Bing Images", "bing.com/images", "Public image search"),
        new("TinEye", "tineye.com", "Public reverse-image search service"),
        new("Flickr Search", "flickr.com/search", "Public photo search"),
        new("Wikimedia Commons Search", "commons.wikimedia.org", "Public media search"),
        new("Google Videos", "google.com/videohp", "Public video search"),
        new("YouTube Search", "youtube.com/results", "Public video search"),
        new("Internet Archive Media", "archive.org/details", "Public archived media"),
        new("Openverse", "openverse.org", "Openly licensed media search"),
        new("Unsplash", "unsplash.com", "Public photo catalog"),
        new("Pexels", "pexels.com", "Public photo catalog"),
        new("Pixabay", "pixabay.com", "Public media catalog"),
        new("IMDb", "imdb.com", "Public film metadata"),
        new("TMDB", "themoviedb.org", "Public film and TV metadata"),
        new("MusicBrainz", "musicbrainz.org", "Public music metadata"),
        new("Discogs", "discogs.com", "Public music catalog"),
        new("AllMusic", "allmusic.com", "Public music metadata"),
        new("Google Play Books", "play.google.com/books", "Public book catalog"),
        new("Open Food Facts", "openfoodfacts.org", "Public product data"),
        new("Wikidata Query Service", "query.wikidata.org", "Structured public knowledge graph"),
    ];

    private static readonly Module[] Modules = BuildCatalog();

    private static List<Module> LoadSources()
    {
        var sources = SeedModules.ToList();
        // Optional user-maintained CSV: Name,Domain,Purpose. Supports up to 5,000 extra rows.
        // Search destinations are generated locally; importing a row does not create a live API adapter.
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Sources", "sources.csv"),
            Path.Combine(AppContext.BaseDirectory, "sources.csv"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HamTool", "sources.csv")
        };
        var file = candidates.FirstOrDefault(File.Exists);
        if (file is null) return sources;

        var knownDomains = new HashSet<string>(sources.Select(x => NormalizeDomain(x.Domain)), StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var line in File.ReadLines(file).Skip(1))
            {
                if (sources.Count >= 5190) break;
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#", StringComparison.Ordinal)) continue;
                var fields = ParseCsvLine(line);
                if (fields.Count < 2) continue;
                var name = fields[0].Trim();
                var domain = NormalizeDomain(fields[1]);
                var purpose = fields.Count > 2 && !string.IsNullOrWhiteSpace(fields[2]) ? fields[2].Trim() : "Public-source search";
                if (name.Length == 0 || !IsSafePublicDomain(domain) || !knownDomains.Add(domain)) continue;
                sources.Add(new Module(name, domain, purpose));
            }
        }
        catch (IOException) { /* Keep the built-in catalog available if the optional file is locked. */ }
        return sources;
    }

    private static string NormalizeDomain(string value)
    {
        var raw = value.Trim();
        if (raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) raw = raw[8..];
        else if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) raw = raw[7..];
        raw = raw.Split('/', '?', '#')[0].Trim().TrimEnd('.').ToLowerInvariant();
        return raw.StartsWith("www.", StringComparison.Ordinal) ? raw[4..] : raw;
    }

    private static bool IsSafePublicDomain(string domain)
    {
        if (domain.Length is < 3 or > 253 || !domain.Contains('.')) return false;
        if (domain.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-' || c == '.'))) return false;
        if (domain.StartsWith('.') || domain.EndsWith('.') || domain.Contains("..", StringComparison.Ordinal)) return false;
        if (System.Net.IPAddress.TryParse(domain, out _)) return false;
        return !domain.Equals("localhost", StringComparison.OrdinalIgnoreCase) && !domain.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var value = new System.Text.StringBuilder();
        var quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; }
            else if (c == '"') quoted = !quoted;
            else if (c == ',' && !quoted) { result.Add(value.ToString()); value.Clear(); }
            else value.Append(c);
        }
        result.Add(value.ToString());
        return result;
    }

    private static Module[] BuildCatalog()
    {
        var sources = LoadSources();
        var modules = new List<Module>(5000);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // First give every unique source a base module, so 5,000 imported sources remain visible.
        foreach (var source in sources)
        {
            if (modules.Count >= 5000) break;
            var module = new Module(source.Name, source.Domain, source.Purpose, "general");
            if (seen.Add(module.Name)) modules.Add(module);
        }

        // Then fill the remaining slots with transparent source + search-recipe combinations.
        foreach (var recipe in Recipes.Skip(1))
        {
            foreach (var source in sources)
            {
                if (modules.Count >= 5000) break;
                var module = new Module($"{source.Name} — {recipe.Name}", source.Domain,
                    $"{source.Purpose}; {recipe.Description}", recipe.Key);
                if (seen.Add(module.Name)) modules.Add(module);
            }
            if (modules.Count >= 5000) break;
        }
        return modules.ToArray();
    }

    public static int Count => Modules.Length;
    public static int SeedSourceCount => Math.Min(5000, LoadSources().Count);
    public static int RecipeCount => Recipes.Length;
    public static int CategoryCount => Modules.Select(x => x.Category).Distinct(StringComparer.Ordinal).Count();
    public static IReadOnlyList<string> Categories => Modules.Select(x => x.Category).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    public static List<SearchLink> Build(string rawQuery, string? language = null)
    {
        var query = rawQuery.Trim();
        var recipeByKey = Recipes.ToDictionary(x => x.Key, StringComparer.Ordinal);
        return Modules
            .Select(module =>
            {
                var host = NormalizeDomain(module.Domain);
                var recipe = recipeByKey.TryGetValue(module.RecipeKey, out var found) ? found : Recipes[0];
                var searchQuery = $"site:{host} {recipe.Apply(query)}";
                var encodedQuery = Uri.EscapeDataString(searchQuery);
                var category = LocalizationService.Translate(module.Category, language);
                var description = LocalizationService.Translate("Public-source search link; verify results at the original source.", language);
                return new SearchLink(
                    module.Name,
                    $"[{category}] {description} ({recipe.Description})",
                    $"https://www.google.com/search?q={encodedQuery}");
            })
            .ToList();
    }
}
