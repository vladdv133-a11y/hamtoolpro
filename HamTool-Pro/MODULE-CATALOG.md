# HamTool Pro 2.4.0 — expandable source catalog

The application builds up to **5,000 displayed modules** from built-in sources and additional public sources imported from `Sources/sources.csv`. Each source gets a base module before recipe variants. The shipped CSV is a starter list, not 5,000 preloaded websites.

Each module generates a Google `site:` search link with one transparent query recipe. Recipes include general query, exact phrase, page title, URL keywords, PDF/DOCX/XLSX/CSV/PPTX, page text, public contact/about pages, documentation, policies, press/news, careers, GitHub mentions, recent/historical mentions, exclusions, definitions, JSON/XML/TXT/Markdown. Query operators are search-engine features and may behave differently over time.

**Important:** these are 5,000 search recipes, not 5,000 independent websites or live API connectors. Built-in sources are maintained in `Services/SourceCatalogService.cs`; extra sources can be added to `Sources/sources.csv`. Direct live APIs are separate. Search results are not guaranteed and should be verified at the original source. Use public information responsibly and respect source terms, privacy, and applicable law.

## Catalogue counts
- Generated modules: 5,000
- Built-in curated source entries: 190
- Additional CSV entries supported: up to 5,000
- Query recipes: 27
- Displayed modules: up to 5,000

The in-app category filter uses the source/category metadata. The app does not bypass access controls, CAPTCHA, logins, or rate limits.
