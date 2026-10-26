# HamTool Pro update

## Localization
- Added a persistent `Language` setting in `%LOCALAPPDATA%\HamTool\settings.json`.
- Added Ukrainian, English, Polish, German, French, and Spanish interface languages.
- Applying a language updates the WPF interface, navigation, settings, dialogs, status messages, search category labels, and research-log column headers.
- The chosen language is saved immediately and restored on the next launch.

## Source catalogue
- Kept 190 source-link modules.
- Added automatic grouping into 13 topical categories and a category filter in the research results view.
- Category filtering preserves general search links and filters catalog modules by their category.
- Fixed the displayed-link selection mapping so opening a filtered result opens the correct URL.

## Accuracy notes
- The 190 entries are public-source search-link modules, not 190 live API integrations. They currently create Google `site:` search links for the named source.
- Existing live API adapters remain separate; source responses and third-party content can still appear in their original language.
- The project was statically checked for valid XAML XML, 190 catalog entries, 13 computed categories, balanced braces/parentheses, and duplicate localization keys. A full WPF build could not be run in this Linux environment because the .NET SDK is not installed here. Build on Windows using `win-build.bat`.


## 2.2 follow-up fixes
- Added a batch website availability checker for up to 5,000 unique user-supplied public URLs, with capped concurrency, per-request timeouts, HTTP status summaries, and cancellation. This is an availability check only, not a vulnerability scanner.
- Added phone-number public-listing search, public contact-email search, and IP/ASN public registry link modes. Phone/email modes use public search links rather than private-person lookup services.
- Added explicit vertical scrolling to live results, search-link lists, and the history grid.
- Expanded interface translations for new query types, validation, and status messages in Ukrainian, English, Polish, German, French, and Spanish.
- Kept the distinction between 190 source-link catalogue entries and the smaller number of live API adapters.


### 2.2.1 — reliability fixes
- Fixed category filtering so it uses invariant category keys and continues to work after changing UI language.
- Fixed live-search cancellation so user cancellation propagates instead of being reported as an ordinary empty/partial search.
- Prevented older cancelled searches from overwriting results from a newer search.
- Updated SQLitePCLRaw native library packages to 2.1.14 to address the high-severity SQLite dependency warning associated with older 2.1.11 packages.
- Neutralized formula-leading values in CSV exports to reduce spreadsheet formula-injection risk.

### 2.2.2 — language selector follow-up
- Normalized the saved language before initializing the UI and applied the selected language on startup.
- Explicitly refreshed query-mode labels from stable internal tags whenever the language changes, including phone, IP/ASN, email, and batch website modes.
- Added translations for all query-mode labels and the language-change confirmation across Ukrainian, Polish, German, French, and Spanish; English uses the source labels.
- Kept the language selection saved immediately and restored from settings on the next launch.


### 2.3.0 — 5,000 search modules
- Expanded the runtime catalogue to exactly 5,000 uniquely named source-plus-recipe modules, based on 190 curated public source entries and 27 search recipes.
- Added query recipes for exact phrases, titles, URLs, common document/data file types, public contact/about pages, documentation, policy/news/careers context, GitHub mentions, date filters, exclusions, and structured/text resources.
- Preserved the distinction between generated public search links and real live API integrations.
- Updated the module catalogue and README to document the count and avoid representing recipes as independent API connectors.
