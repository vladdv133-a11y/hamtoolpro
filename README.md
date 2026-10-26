# HamTool Pro 2.3.0 — 5,000 search modules

HamTool Pro is a Windows WPF research organizer with **live public API search**, curated public search destinations, a local SQLite audit trail, case labels, settings, and exports. It is designed to be extended through individual source adapters rather than pretending every website has a universal API.

## Build and run
1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) on Windows.
2. Extract the archive.
3. Double-click `BUILD-WINDOWS.bat`, or run `dotnet restore` and `dotnet build -c Release` from this directory.

## Live search adapters
The current release makes parallel requests to these public APIs when available:
- Wikipedia search API
- Crossref scholarly metadata API
- OpenAlex scholarly works API
- Hacker News Algolia search API
- Internet Archive advanced search API
- GitHub repository search API (for keywords and public organizations; subject to unauthenticated API rate limits)
- crt.sh certificate-transparency JSON endpoint and Google Public DNS-over-HTTPS (for domain queries)

The app reports provider status and result counts, deduplicates results by URL, and lets the operator open source URLs. A provider can return no matches, be offline, or rate-limit requests. Search results are not verified facts.

## Search destinations and module catalogue
The app exposes **5,000 uniquely named search modules** built from **190 curated public-source entries** and **27 transparent search recipes** (exact phrase, title, URL, file types, public contact pages, news context, and other search operators). These are combinations of a source and a search recipe, not 5,000 independent websites or live API integrations. Each generates a Google `site:` search link; direct API adapters are listed separately above. Results must be checked at the original source, and availability depends on the source and search engine.

## Features
- One persistent interface-language setting across the WPF window: Ukrainian, English, Polish, German, French, and Spanish. The choice is saved in the local settings file.
- The catalog exposes 5,000 source-plus-recipe modules from 190 curated public sources and 27 query recipes, grouped into topical categories with a working category filter; they are not 5,000 live API connectors.
- Phone-number research limited to public web and business listings; no private-person reverse-lookup or data-broker integrations.
- Public organization contact-email search links and public IP/ASN registry links.
- Batch website availability checks for a user-supplied list of up to 5,000 unique public URLs. Paste one URL per line in the search field and choose **Batch website check**. It sends one request per URL, limits concurrency to 10, reads response headers only, rejects obvious local/private IP literals, and does not crawl paths or scan for vulnerabilities.
- Explicit cancellation control and scrollable live results, search-link lists, and local history grid.
- Live parallel public API search with per-provider status and timeouts.
- Search links for additional public sources that do not provide a suitable open API.
- Live-result export to JSON, CSV, or standalone HTML report.
- SQLite research log, query filter, delete one entry, and clear history with confirmation.
- JSON/CSV/TOML export of research log.
- Local preferences for history, browser opening, timestamps, and default export.
- White/light WPF interface; local database and settings under `%LOCALAPPDATA%\HamTool`.
- Optional Rust helper source in `Rust/`; it is not required by the app.
- 5,000 generated source-plus-recipe search modules, based on 190 curated public sources and 27 query recipes, kept separate from live API adapters.

## Batch website checks and limits

The 5,000 figure is the maximum number of unique URLs the batch checker accepts from a user-provided list; it is **not** a built-in database of 5,000 sites. The checker performs a low-impact availability request per URL. HTTP status is not proof of page quality or security, and a timeout does not prove a site is down. Use it only for public sites you are authorized to check. Large batches can take a long time and can be cancelled.

Phone and email inputs generate links to public search engines or public organization listings; they do not establish who owns a number or mailbox.

## What the module count means
The runtime catalog has exactly 5,000 uniquely named combinations selected from 190 curated source entries × 27 query recipes (5,130 possible combinations). All 190 source entries are represented; the first 26 recipes are available for every source and the 27th recipe is applied to 60 sources to reach 5,000. A module generates a public Google `site:` search link with its recipe applied. This is not 5,000 unique API integrations, and some aliases can resolve to the same underlying website. Live API integrations and direct public-profile URL checks remain a smaller, separately implemented set of adapters. The app does not bypass access controls, CAPTCHAs, or rate limits.

## Data and responsible use
The app stores query text, notes, and timestamps locally when history is enabled. Live requests are sent to the third-party providers listed above; do not submit secrets, passwords, private records, or sensitive personal data. Use public data lawfully, respect API terms and rate limits, and avoid using the tool for harassment or covert tracking of private individuals. HamTool does not bypass access controls or sign in to accounts.

## Build troubleshooting
Run `build_hamtool.ps1` from PowerShell, or double-click `win-build.bat`. The script writes build output to `build-log.txt`. To force a clean rebuild:

```powershell
.\build_hamtool.ps1 -Clean
```

### Appearance themes

Open **Preferences → Appearance** and choose **Світла** (soft gray-blue background) or **Біла** (pure white window background), then click **Save preferences**. The selection is stored locally and applied on the next launch.
## info
this app use **dotnet** `.NET 10.0` 
## Languages
this app use rust, dotnet, C# and more...
## LICENSE 
Copyright 2026 year Vlad-projects

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the “Software”), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
