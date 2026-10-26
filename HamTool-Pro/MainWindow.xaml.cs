using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using HamTool.Models;
using HamTool.Services;

namespace HamTool;
public partial class MainWindow : Window
{
    private readonly StorageService store = new();
    private AppSettings settings;
    private List<SearchLink> currentLinks = new();
    private List<SearchLink> displayedLinks = new();
    private List<LiveSearchResult> currentResults = new();
    private CancellationTokenSource? searchCts;
    private bool _loadingPreferences;
    private string _lastQuery = string.Empty;
    private string _lastType = "Keywords";
    private string T(string value) => LocalizationService.Translate(value, settings?.Language);
    // Keep the result lists scrollable with the mouse wheel even when nested inside a TabControl.
    private void ScrollableList_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (sender is not DependencyObject source) return;
        var scrollViewer = FindVisualChild<ScrollViewer>(source);
        if (scrollViewer is null) return;

        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta / 3.0);
        e.Handled = true;
    }

    private static TChild? FindVisualChild<TChild>(DependencyObject parent) where TChild : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TChild match) return match;
            var nested = FindVisualChild<TChild>(child);
            if (nested is not null) return nested;
        }
        return null;
    }


    public MainWindow()
    {
        InitializeComponent();
        settings = store.LoadSettings();
        settings.Language = LocalizationService.Normalize(settings.Language);
        ApplyTheme(settings.Theme);
        LoadPreferencesIntoUi();
        InitializeSourceCategories();
        DataFolderText.Text = store.DataDirectory;
        SearchDestinationCount.Text = SourceCatalogService.Count.ToString();
        UpdateCount();
        LocalizationService.Apply(this, settings.Language);
        RefreshQueryTypeLabels();
        SearchDestinationDetails.Text = T("Grouped into {0} categories").Replace("{0}", SourceCatalogService.CategoryCount.ToString());
    }
    private void ShowPage(string page, string subtitle)
    {
        OverviewPage.Visibility = ResearchPage.Visibility = LogPage.Visibility = CasesPage.Visibility = PreferencesPage.Visibility = Visibility.Collapsed;
        switch (page)
        {
            case "Overview": OverviewPage.Visibility = Visibility.Visible; PageTitle.Text = T("Overview"); break;
            case "Research": ResearchPage.Visibility = Visibility.Visible; PageTitle.Text = T("New research"); break;
            case "Log": LogPage.Visibility = Visibility.Visible; PageTitle.Text = T("Research log"); LoadLog(""); break;
            case "Cases": CasesPage.Visibility = Visibility.Visible; PageTitle.Text = T("Cases & notes"); break;
            case "Preferences": PreferencesPage.Visibility = Visibility.Visible; PageTitle.Text = T("Preferences"); break;
        }
        PageSubtitle.Text = T(subtitle);
    }
    private void Overview_Click(object s, RoutedEventArgs e) => ShowPage("Overview", "A clean workspace for public-source research.");
    private void Research_Click(object s, RoutedEventArgs e) => ShowPage("Research", "Search live public APIs and organize source links.");
    private void Log_Click(object s, RoutedEventArgs e) => ShowPage("Log", "Searchable local audit trail.");
    private void Cases_Click(object s, RoutedEventArgs e) => ShowPage("Cases", "Group research entries by case or project.");
    private void Preferences_Click(object s, RoutedEventArgs e) => ShowPage("Preferences", "Tune local storage and export behavior.");
    private void RefreshQueryTypeLabels()
    {
        if (QueryType is null) return;
        foreach (var item in QueryType.Items.OfType<ComboBoxItem>())
        {
            var key = item.Tag?.ToString() switch
            {
                "Keywords" => "Keywords",
                "Username / handle" => "Username / handle",
                "Domain / website" => "Domain / website",
                "Public organization" => "Public organization",
                "Phone number" => "Phone number (public listings)",
                "IP address" => "IP address / ASN",
                "Email address" => "Email / public contact",
                "Website batch scan" => "Batch website check (up to 5,000 URLs)",
                _ => item.Content?.ToString() ?? string.Empty
            };
            item.Content = T(key);
        }
    }

    private string TypeValue() => (QueryType.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() ?? "Keywords";

    private bool ValidateQuery(out string query)
    {
        query = QueryInput.Text.Trim();
        if (query.Length < 2) { MessageBox.Show(T("Enter at least two characters."), "HamTool", MessageBoxButton.OK, MessageBoxImage.Information); return false; }
        var type = TypeValue();
        var maxLength = type == "Website batch scan" ? 500000 : 300;
        if (query.Length > maxLength)
        {
            MessageBox.Show(type == "Website batch scan" ? T("Batch input is too long.") : T("Search terms are limited to 300 characters."));
            return false;
        }
        if (type == "Website batch scan")
        {
            var urls = BatchWebsiteScanService.ParseUrls(query);
            if (urls.Count == 0) { MessageBox.Show(T("Paste at least one valid public website URL, one per line.")); return false; }
            if (urls.Count > BatchWebsiteScanService.MaximumUrls) { MessageBox.Show(T("Batch checks are limited to 5,000 unique URLs.")); return false; }
        }
        if (type == "Phone number")
        {
            var digits = query.Count(char.IsDigit);
            if (digits < 7 || digits > 20) { MessageBox.Show(T("Enter a phone number with 7 to 20 digits; include country code when known.")); return false; }
        }
        if (type == "Email address" && (!query.Contains('@') || !query.Contains('.')))
        { MessageBox.Show(T("Enter a public organization contact email address.")); return false; }
        if (type == "IP address" && !System.Net.IPAddress.TryParse(query, out _))
        { MessageBox.Show(T("Enter a valid IPv4 or IPv6 address.")); return false; }
        return true;
    }
    private void SaveQuery(string query)
    {
        if (settings.SaveSearchHistory)
        {
            var savedQuery = TypeValue() == "Website batch scan"
                ? $"Batch website check ({BatchWebsiteScanService.ParseUrls(query).Count} URLs)"
                : query;
            store.Add(string.IsNullOrWhiteSpace(CaseInput.Text) ? "General" : CaseInput.Text.Trim(), TypeValue(), savedQuery, NotesInput.Text.Trim());
        }
        UpdateCount();
    }
    private void Generate_Click(object s, RoutedEventArgs e)
    {
        if (!ValidateQuery(out var q)) return;
        if (TypeValue() == "Website batch scan")
        {
            StatusText.Text = T("Batch mode is ready. Click Run live search to check up to 5,000 URLs.");
            return;
        }
        SaveQuery(q); _lastQuery = q; _lastType = TypeValue(); currentLinks = PublicSearchService.Build(_lastType, q, settings.Language);
        UpdateLinksList();
        StatusText.Text = T("Prepared {0} public search links from {1} catalog modules. These are source links, not all live API connectors.").Replace("{0}", currentLinks.Count.ToString()).Replace("{1}", SourceCatalogService.Count.ToString());
    }
    private async void LiveSearch_Click(object s, RoutedEventArgs e)
    {
        if (!ValidateQuery(out var q)) return;
        searchCts?.Cancel(); searchCts?.Dispose();
        var activeType = TypeValue();
        var activeCts = new CancellationTokenSource(activeType == "Website batch scan" ? TimeSpan.FromMinutes(50) : TimeSpan.FromSeconds(30));
        searchCts = activeCts;
        SaveQuery(q); _lastQuery = q; _lastType = activeType;
        currentLinks = activeType == "Website batch scan" ? new List<SearchLink>() : PublicSearchService.Build(activeType, q, settings.Language);
        UpdateLinksList();
        LiveResultsList.ItemsSource = null; currentResults.Clear();
        ProviderStatusText.Text = T("Querying available public APIs…"); StatusText.Text = T("Live search in progress…");
        try
        {
            LiveSearchReport report;
            if (activeType == "Website batch scan")
            {
                var urls = BatchWebsiteScanService.ParseUrls(q);
                ProviderStatusText.Text = T("Checking {0} public website URLs with limited concurrency…").Replace("{0}", urls.Count.ToString());
                var progress = new Progress<int>(done =>
                {
                    if (ReferenceEquals(searchCts, activeCts) && (done == urls.Count || done % 25 == 0))
                        ProviderStatusText.Text = T("Checked {0} of {1} URLs…").Replace("{0}", done.ToString()).Replace("{1}", urls.Count.ToString());
                });
                report = await BatchWebsiteScanService.ScanAsync(urls, activeCts.Token, settings.Language, progress);
            }
            else
            {
                report = await PublicSearchService.SearchLiveAsync(activeType, q, activeCts.Token, settings.Language);
            }
            // An older cancelled run must never overwrite the results of a newer run.
            if (!ReferenceEquals(searchCts, activeCts)) return;
            currentResults = report.Results; LiveResultsList.ItemsSource = currentResults;
            LiveResultsList.DisplayMemberPath = nameof(LiveSearchResult.Display);
            if (activeType == "Website batch scan")
            {
                var httpResponses = report.Results.Count(x => x.Summary.Contains("HTTP ", StringComparison.Ordinal));
                ProviderStatusText.Text = T("Batch checked {0} URLs; {1} returned HTTP responses. Availability only; not a vulnerability scan.")
                    .Replace("{0}", report.Results.Count.ToString()).Replace("{1}", httpResponses.ToString());
                StatusText.Text = T("Batch website check completed: {0} URL results.").Replace("{0}", report.Results.Count.ToString());
            }
            else if (activeType is "Phone number" or "Email address" or "IP address")
            {
                ProviderStatusText.Text = T("Prepared {0} public-source links. These links are not a live identity lookup.").Replace("{0}", report.Results.Count.ToString());
                StatusText.Text = T("Prepared {0} public-source links.").Replace("{0}", report.Results.Count.ToString());
            }
            else
            {
                ProviderStatusText.Text = T("Providers responding: {0}/{1}. Results: {2}. ").Replace("{0}", report.SuccessfulProviders.ToString()).Replace("{1}", report.Providers.Count.ToString()).Replace("{2}", report.Results.Count.ToString()) +
                    string.Join(" · ", report.Providers.Select(p => $"{p.Name}: {T(p.Status)} ({p.Results})"));
                StatusText.Text = T("Live search finished: {0} unique results from {1} responding providers.").Replace("{0}", report.Results.Count.ToString()).Replace("{1}", report.SuccessfulProviders.ToString());
            }
        }
        catch (OperationCanceledException)
        {
            if (!ReferenceEquals(searchCts, activeCts)) return;
            ProviderStatusText.Text = T("Search cancelled. No further requests will be started.");
            StatusText.Text = T("Search cancelled.");
        }
        catch (Exception ex)
        {
            if (!ReferenceEquals(searchCts, activeCts)) return;
            ProviderStatusText.Text = T("Search failed. Check the network and build-log or try again.");
            StatusText.Text = T("Live search failed.");
            MessageBox.Show(T("Live search failed: ") + ex.Message, "HamTool", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    private void InitializeSourceCategories()
    {
        _loadingPreferences = true;
        try
        {
            SourceCategoryFilter.Items.Clear();
            SourceCategoryFilter.Items.Add(new ComboBoxItem { Content = T("All categories"), Tag = "*" });
            foreach (var category in SourceCatalogService.Categories)
                SourceCategoryFilter.Items.Add(new ComboBoxItem { Content = LocalizationService.Translate(category, settings.Language), Tag = category });
            SourceCategoryFilter.SelectedIndex = 0;
        }
        finally { _loadingPreferences = false; }
    }

    private void SourceCategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingPreferences || settings is null || SourceCategoryFilter is null) return;
        UpdateLinksList();
    }

    private void UpdateLinksList()
    {
        var selected = (SourceCategoryFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "*";
        // Catalog purpose tags store the invariant category key, not the translated UI label.
        displayedLinks = currentLinks.Where(link => selected == "*" || !link.Purpose.StartsWith("[", StringComparison.Ordinal) || link.Purpose.StartsWith($"[{selected}]", StringComparison.OrdinalIgnoreCase)).ToList();
        LinksList.ItemsSource = displayedLinks.Select(x => $"{x.Name}   |   {DisplayPurpose(x)}\n{x.Url}").ToList();
    }

    private string DisplayPurpose(SearchLink link)
    {
        if (link.Purpose.StartsWith("[", StringComparison.Ordinal)) return link.Purpose;
        return T("Public-source search link; verify results at the original source.");
    }

    private void RefreshSourceCategoryLabels()
    {
        var selected = (SourceCategoryFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "*";
        _loadingPreferences = true;
        try
        {
            SourceCategoryFilter.Items.Clear();
            SourceCategoryFilter.Items.Add(new ComboBoxItem { Content = T("All categories"), Tag = "*" });
            foreach (var category in SourceCatalogService.Categories)
                SourceCategoryFilter.Items.Add(new ComboBoxItem { Content = LocalizationService.Translate(category, settings.Language), Tag = category });
            SourceCategoryFilter.SelectedIndex = 0;
            for (var i = 0; i < SourceCategoryFilter.Items.Count; i++)
                if ((SourceCategoryFilter.Items[i] as ComboBoxItem)?.Tag?.ToString() == selected) { SourceCategoryFilter.SelectedIndex = i; break; }
        }
        finally { _loadingPreferences = false; }
        UpdateLinksList();
    }

    private void LinksList_DoubleClick(object s, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!settings.OpenResultsInBrowser || LinksList.SelectedIndex < 0 || LinksList.SelectedIndex >= displayedLinks.Count) return;
        OpenExternal(displayedLinks[LinksList.SelectedIndex].Url);
    }
    private void LiveResults_DoubleClick(object s, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!settings.OpenResultsInBrowser || LiveResultsList.SelectedItem is not LiveSearchResult result) return;
        OpenExternal(result.Url);
    }
    private void OpenExternal(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(T("Could not open browser: ") + ex.Message); }
    }
    private void CancelSearch_Click(object s, RoutedEventArgs e)
    {
        if (searchCts is null || searchCts.IsCancellationRequested) return;
        searchCts.Cancel();
        ProviderStatusText.Text = T("Cancellation requested; completed results may still be displayed.");
        StatusText.Text = T("Cancelling search…");
    }

    private void ClearForm_Click(object s, RoutedEventArgs e)
    {
        searchCts?.Cancel(); QueryInput.Clear(); NotesInput.Clear(); LinksList.ItemsSource = null; LiveResultsList.ItemsSource = null; _lastQuery = string.Empty; currentLinks.Clear(); displayedLinks.Clear();
        ProviderStatusText.Text = T("Ready to query public APIs."); currentLinks.Clear(); currentResults.Clear();
    }
    private void LoadLog(string filter) { LogGrid.ItemsSource = store.GetAll(filter); UpdateCount(); ApplyLogColumnTranslations(); }
    private void LogGrid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e) => e.Column.Header = T(ColumnHeaderKey(e.PropertyName));
    private void ApplyLogColumnTranslations()
    {
        foreach (var column in LogGrid.Columns)
        {
            var key = column.SortMemberPath switch
            {
                "Id" => "ID", "CaseName" => "Case / project", "QueryType" => "Query category",
                "Query" => "Search terms", "CreatedAt" => "Created at", "Notes" => "Notes", _ => column.SortMemberPath
            };
            column.Header = T(key);
        }
    }
    private static string ColumnHeaderKey(string propertyName) => propertyName switch
    {
        "Id" => "ID", "CaseName" => "Case / project", "QueryType" => "Query category",
        "Query" => "Search terms", "CreatedAt" => "Created at", "Notes" => "Notes", _ => propertyName
    };
    private void FilterLog_Click(object s, RoutedEventArgs e) => LoadLog(LogFilter.Text.Trim());
    private void RefreshLog_Click(object s, RoutedEventArgs e) => LoadLog(LogFilter.Text.Trim());
    private void UpdateCount() => RecordCount.Text = store.GetAll().Count.ToString();
    private void Export_Click(object s, RoutedEventArgs e)
    {
        string f = (ExportFormat.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? settings.DefaultExport;
        var dlg = new Microsoft.Win32.SaveFileDialog { Title = T("Export HamTool research log"), FileName = $"hamtool-export.{f.ToLowerInvariant()}", Filter = $"{T("File")} {f}|*.{f.ToLowerInvariant()}|{T("All files")}|*.*" };
        if (dlg.ShowDialog() != true) return;
        try { var rows = store.GetAll(LogFilter.Text.Trim()); if (!settings.IncludeTimestamps) foreach (var r in rows) r.CreatedAt = ""; ExportService.Save(rows, dlg.FileName, f); StatusText.Text = T("Export complete."); MessageBox.Show(T("Export completed successfully."), "HamTool"); }
        catch (Exception ex) { MessageBox.Show(T("Export failed: ") + ex.Message); }
    }
    private void ExportLiveResults_Click(object s, RoutedEventArgs e)
    {
        if (currentResults.Count == 0) { MessageBox.Show(T("Run a live search first."), "HamTool"); return; }
        var dlg = new Microsoft.Win32.SaveFileDialog { Title = T("Export live search results"), FileName = "hamtool-live-results.json", Filter = $"JSON {T("File")} (*.json)|*.json|CSV {T("File")} (*.csv)|*.csv|HTML {T("Report")} (*.html)|*.html" };
        if (dlg.ShowDialog() != true) return;
        string ext = System.IO.Path.GetExtension(dlg.FileName).ToLowerInvariant();
        string format;
        if (ext == ".csv") format = "CSV";
        else if (ext == ".html" || ext == ".htm") format = "HTML";
        else format = "JSON";
        try { ExportService.SaveLiveResults(currentResults, dlg.FileName, format); StatusText.Text = T("Exported {0} live results.").Replace("{0}", currentResults.Count.ToString()); }
        catch (Exception ex) { MessageBox.Show(T("Could not export live results") + ": " + ex.Message, "HamTool", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void DeleteSelected_Click(object s, RoutedEventArgs e)
    {
        if (LogGrid.SelectedItem is not ResearchRecord r) { MessageBox.Show(T("Select a log entry first.")); return; }
        if (MessageBox.Show(T("Delete entry #{0}?").Replace("{0}", r.Id.ToString()), T("Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        store.Delete(r.Id); LoadLog(LogFilter.Text.Trim()); StatusText.Text = T("Entry deleted.");
    }
    private void ClearAll_Click(object s, RoutedEventArgs e)
    {
        if (settings.ConfirmBeforeClear && MessageBox.Show(T("Delete all local research history? This cannot be undone."), T("Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        store.Clear(); LoadLog(""); StatusText.Text = T("Research history cleared.");
    }
    private void ApplyTheme(string? theme)
    {
        // Apply immediately to the whole window. The original implementation changed only
        // the Window background, which was mostly hidden behind hard-coded white surfaces.
        bool white = string.Equals(theme, "White", StringComparison.OrdinalIgnoreCase);
        var canvas = new SolidColorBrush((Color)ColorConverter.ConvertFromString(white ? "#FFFFFF" : "#EAF0F7"));
        Background = canvas;
        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#172033"));
        ApplyThemeToVisualTree(this, white);
    }

    private static void ApplyThemeToVisualTree(DependencyObject parent, bool white)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            string cardColor = white ? "#FFFFFF" : "#FCFDFE";
            string canvasColor = white ? "#FFFFFF" : "#EAF0F7";
            if (child is Border border && IsThemeSurface(border.Background))
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(cardColor));
            else if (child is Panel panel && IsThemeSurface(panel.Background))
                panel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(canvasColor));
            else if (child is Control control && IsThemeSurface(control.Background))
                control.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(cardColor));
            ApplyThemeToVisualTree(child, white);
        }
    }

    private static bool IsThemeSurface(Brush? brush)
    {
        if (brush is not SolidColorBrush solid) return false;
        Color c = solid.Color;
        return c == Colors.White || c == (Color)ColorConverter.ConvertFromString("#FFFFFF")
            || c == (Color)ColorConverter.ConvertFromString("#F5F7FB")
            || c == (Color)ColorConverter.ConvertFromString("#FCFDFE")
            || c == (Color)ColorConverter.ConvertFromString("#EAF0F7");
    }

    private void ThemeChoice_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // SelectionChanged can fire while InitializeComponent is building the window.
        if (_loadingPreferences || settings is null || ThemeChoice is null || ThemeChoice.SelectedIndex < 0) return;
        settings.Theme = ThemeChoice.SelectedIndex == 1 ? "White" : "Light";
        ApplyTheme(settings.Theme);
        store.SaveSettings(settings);
        if (StatusText is not null) StatusText.Text = settings.Theme == "White" ? T("White theme applied and saved.") : T("Light theme applied and saved.");
    }

    private void RefreshPageLocalization()
    {
        if (OverviewPage.Visibility == Visibility.Visible) ShowPage("Overview", "A clean workspace for public-source research.");
        else if (ResearchPage.Visibility == Visibility.Visible) ShowPage("Research", "Search live public APIs and organize source links.");
        else if (LogPage.Visibility == Visibility.Visible) ShowPage("Log", "Searchable local audit trail.");
        else if (CasesPage.Visibility == Visibility.Visible) ShowPage("Cases", "Group research entries by case or project.");
        else if (PreferencesPage.Visibility == Visibility.Visible) ShowPage("Preferences", "Tune local storage and export behavior.");
    }

    private void LoadPreferencesIntoUi()
    {
        _loadingPreferences = true;
        try
        {
            ThemeChoice.SelectedIndex = string.Equals(settings.Theme, "White", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            SaveHistoryCheck.IsChecked = settings.SaveSearchHistory;
            ConfirmClearCheck.IsChecked = settings.ConfirmBeforeClear;
            OpenBrowserCheck.IsChecked = settings.OpenResultsInBrowser;
            TimestampCheck.IsChecked = settings.IncludeTimestamps;
            ExportFormat.SelectedIndex = settings.DefaultExport switch { "CSV" => 1, "TOML" => 2, _ => 0 };
            var language = LocalizationService.Normalize(settings.Language);
            settings.Language = language;
            LanguageChoice.SelectedIndex = language switch { "en" => 1, "pl" => 2, "de" => 3, "fr" => 4, "es" => 5, _ => 0 };
        }
        finally { _loadingPreferences = false; }
    }

    private void LanguageChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingPreferences || settings is null || LanguageChoice?.SelectedItem is not ComboBoxItem item) return;
        var language = item.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(language)) return;
        settings.Language = LocalizationService.Normalize(language);
        store.SaveSettings(settings);
        LocalizationService.Apply(this, settings.Language);
        RefreshQueryTypeLabels();
        SearchDestinationDetails.Text = T("Grouped into {0} categories").Replace("{0}", SourceCatalogService.CategoryCount.ToString());
        RefreshSourceCategoryLabels();
        if (_lastQuery.Length >= 2 && _lastType != "Website batch scan")
        {
            currentLinks = PublicSearchService.Build(_lastType, _lastQuery, settings.Language);
            UpdateLinksList();
        }
        ApplyLogColumnTranslations();
        if (currentResults.Count > 0)
        {
            if (_lastType is "Phone number" or "Email address" or "IP address")
            {
                currentResults = PublicSearchService.Build(_lastType, _lastQuery, settings.Language)
                    .Select(link => new LiveSearchResult(link.Name, link.Purpose,
                        T("Search link only; open it to search public listings. No private-person lookup is performed."), link.Url)).ToList();
            }
            else if (_lastType == "Website batch scan")
            {
                currentResults = currentResults.Select(result => RelocalizeBatchResult(result)).ToList();
            }
            LiveResultsList.ItemsSource = null;
            LiveResultsList.ItemsSource = currentResults;
            LiveResultsList.DisplayMemberPath = nameof(LiveSearchResult.Display);
            ProviderStatusText.Text = T("Existing results are source content; run the search again to refresh localized status messages.");
        }
        RefreshPageLocalization(); StatusText.Text = T("Language saved and applied to the entire interface.");
    }

    private LiveSearchResult RelocalizeBatchResult(LiveSearchResult result)
    {
        var summary = result.Summary;
        var match = System.Text.RegularExpressions.Regex.Match(summary, @"^HTTP (\d{3})");
        if (match.Success)
        {
            var code = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var key = code is >= 200 and < 300
                ? "HTTP {0} — response received; page content was not analyzed."
                : "HTTP {0} — server responded; this alone does not prove the site is broken.";
            summary = T(key).Replace("{0}", code.ToString());
        }
        else if (summary.Contains("Timed out", StringComparison.OrdinalIgnoreCase) || summary.Contains("очікування", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("limitu czasu", StringComparison.OrdinalIgnoreCase) || summary.Contains("Zeitüberschreitung", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("délai dépassé", StringComparison.OrdinalIgnoreCase) || summary.Contains("tiempo de espera", StringComparison.OrdinalIgnoreCase))
            summary = T("Timed out after 5 seconds.");
        else if (summary.Contains("Network", StringComparison.OrdinalIgnoreCase) || summary.Contains("TLS", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("DNS", StringComparison.OrdinalIgnoreCase) || summary.Contains("Помилка мережі", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("Błąd sieci", StringComparison.OrdinalIgnoreCase) || summary.Contains("Netzwerk", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("Erreur réseau", StringComparison.OrdinalIgnoreCase) || summary.Contains("Error de red", StringComparison.OrdinalIgnoreCase))
            summary = T("Network, TLS, DNS, or connection error.");
        return new LiveSearchResult(T("Website availability"), result.Title, summary, result.Url);
    }

    private void SavePreferences_Click(object s, RoutedEventArgs e)
    {
        settings.Theme = ThemeChoice.SelectedIndex == 1 ? "White" : "Light"; ApplyTheme(settings.Theme);
        settings.SaveSearchHistory = SaveHistoryCheck.IsChecked == true; settings.ConfirmBeforeClear = ConfirmClearCheck.IsChecked == true;
        settings.OpenResultsInBrowser = OpenBrowserCheck.IsChecked == true; settings.IncludeTimestamps = TimestampCheck.IsChecked == true;
        settings.DefaultExport = (ExportFormat.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "JSON";
        store.SaveSettings(settings); StatusText.Text = T("Preferences saved locally."); MessageBox.Show(T("Preferences saved."), "HamTool");
    }
    private void OpenDataFolder_Click(object s, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(store.DataDirectory) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
}
