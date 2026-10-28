using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Nerkh;

public partial class MainWindow : Window
{
    private readonly TgjuService _service = new();
    private readonly ObservableCollection<MarketItem> _markets = [];
    private readonly Dictionary<string, MarketItem> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly CancellationTokenSource _shutdown = new();
    private ICollectionView? _marketView;
    private MarketItem? _selected;
    private bool _refreshing, _useToman = true;

    private static readonly Dictionary<string, string> PersianNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"]="دلار آمریکا", ["EUR"]="یورو", ["GBP"]="پوند بریتانیا", ["AED"]="درهم امارات", ["TRY"]="لیر ترکیه",
        ["CAD"]="دلار کانادا", ["AUD"]="دلار استرالیا", ["CHF"]="فرانک سوئیس", ["CNY"]="یوان چین", ["JPY"]="ین ژاپن",
        ["RUB"]="روبل روسیه", ["INR"]="روپیه هند", ["IQD"]="دینار عراق", ["AFN"]="افغانی", ["SAR"]="ریال عربستان",
        ["QAR"]="ریال قطر", ["KWD"]="دینار کویت", ["OMR"]="ریال عمان", ["BHD"]="دینار بحرین", ["PKR"]="روپیه پاکستان",
        ["AZN"]="منات آذربایجان", ["AMD"]="درام ارمنستان", ["GEL"]="لاری گرجستان", ["SEK"]="کرون سوئد", ["NOK"]="کرون نروژ",
        ["DKK"]="کرون دانمارک", ["NZD"]="دلار نیوزیلند", ["SGD"]="دلار سنگاپور", ["HKD"]="دلار هنگ‌کنگ", ["KRW"]="وون کره جنوبی",
        ["BRL"]="رئال برزیل", ["MXN"]="پزوی مکزیک", ["ZAR"]="راند آفریقای جنوبی", ["THB"]="بات تایلند", ["MYR"]="رینگیت مالزی"
    };

    public MainWindow() { InitializeComponent(); _timer.Tick += async (_, _) => await RefreshAsync(); }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Add("price_dollar_rl", "USD", "دلار آمریکا", "$", "$"); Add("price_eur", "EUR", "یورو", "€", "€"); Add("price_gbp", "GBP", "پوند بریتانیا", "£", "£"); Add("price_aed", "AED", "درهم امارات", "د.إ", "د.إ"); Add("geram18", "GOLD", "طلای ۱۸ عیار", "◆", "Au", true);
        _marketView = CollectionViewSource.GetDefaultView(_markets); _marketView.Filter = FilterMarket; MarketList.ItemsSource = _marketView; QuickList.ItemsSource = _markets.Take(7);
        _selected = _byKey["price_dollar_rl"]; BuildPinnedCards(); RenderDetail(); _timer.Start(); await RefreshAsync();
    }

    private void Add(string key, string symbol, string name, string? icon = null, string? glyph = null, bool gold = false)
    {
        if (_byKey.ContainsKey(key)) return;
        var item = new MarketItem { Key = key, Symbol = symbol, Name = name, Icon = icon ?? CurrencyGlyph(symbol), Glyph = glyph ?? CurrencyGlyph(symbol), IconPath = gold ? null : FlagPathForCurrency(symbol), IsGold = gold, UseToman = _useToman }; _byKey[key] = item; _markets.Add(item);
    }

    private void AddCurrenciesFromFeed(Dictionary<string, Quote> quotes)
    {
        var isoNames = CurrencyNames();
        foreach (var pair in quotes)
        {
            if (!pair.Key.StartsWith("price_", StringComparison.OrdinalIgnoreCase)) continue;
            var code = pair.Key[6..].ToUpperInvariant();
            if (code.Length != 3 || !isoNames.TryGetValue(code, out var english)) continue;
            Add(pair.Key, code, PersianNames.GetValueOrDefault(code, english));
        }
    }

    private static string CurrencyGlyph(string code) => code.ToUpperInvariant() switch
    {
        "USD" or "CAD" or "AUD" or "NZD" or "SGD" or "HKD" => "$", "EUR" => "€", "GBP" => "£", "JPY" or "CNY" => "¥",
        "RUB" => "₽", "INR" => "₹", "KRW" => "₩", "TRY" => "₺", "THB" => "฿", "ILS" => "₪", "VND" => "₫",
        "AED" => "د.إ", "SAR" or "QAR" or "OMR" => "ر.س", _ => "¤"
    };

    private static string? FlagPathForCurrency(string code)
    {
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"]="US", ["EUR"]="EU", ["GBP"]="GB", ["AED"]="AE", ["CAD"]="CA", ["AUD"]="AU", ["CHF"]="CH", ["CNY"]="CN", ["JPY"]="JP", ["TRY"]="TR",
            ["RUB"]="RU", ["INR"]="IN", ["IQD"]="IQ", ["AFN"]="AF", ["SAR"]="SA", ["QAR"]="QA", ["KWD"]="KW", ["OMR"]="OM", ["BHD"]="BH", ["PKR"]="PK",
            ["AZN"]="AZ", ["AMD"]="AM", ["GEL"]="GE", ["SEK"]="SE", ["NOK"]="NO", ["DKK"]="DK", ["NZD"]="NZ", ["SGD"]="SG", ["HKD"]="HK", ["KRW"]="KR",
            ["BRL"]="BR", ["MXN"]="MX", ["ZAR"]="ZA", ["THB"]="TH", ["MYR"]="MY"
        };
        var country = overrides.GetValueOrDefault(code);
        if (country is null)
        {
            foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                try { var region = new RegionInfo(culture.Name); if (region.ISOCurrencySymbol.Equals(code, StringComparison.OrdinalIgnoreCase)) { country = region.TwoLetterISORegionName; break; } } catch { }
            }
        }
        return country is { Length: 2 } ? $"pack://application:,,,/Nerkho;component/Assets/Flags/{country.ToLowerInvariant()}.png" : null;
    }

    private static Dictionary<string, string> CurrencyNames()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures)) { try { var region = new RegionInfo(culture.Name); result.TryAdd(region.ISOCurrencySymbol, region.CurrencyEnglishName); } catch { } }
        return result;
    }

    private async Task RefreshAsync()
    {
        if (_refreshing) return; _refreshing = true;
        try
        {
            var quotes = await _service.GetQuotesAsync(_shutdown.Token); AddCurrenciesFromFeed(quotes);
            foreach (var pair in _byKey) if (quotes.TryGetValue(pair.Key, out var q) && q.Price > 0) pair.Value.Update(q.Price, q.High, q.Low, q.Change, q.Percent, q.Time);
            QuickList.ItemsSource = _markets.Take(7).ToList(); ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(45, 212, 191)); ConnectionText.Text = "متصل به بازار"; UpdatedText.Text = $"بروزرسانی {DateTime.Now:HH:mm:ss}"; RenderDetail();
        }
        catch (OperationCanceledException) { }
        catch { ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(251, 191, 36)); ConnectionText.Text = "تلاش برای اتصال…"; UpdatedText.Text = "نمایش آخرین داده دریافت‌شده"; }
        finally { _refreshing = false; }
    }

    private void BuildPinnedCards()
    {
        PinnedGrid.Children.Clear(); var keys = new[] { "price_dollar_rl", "price_eur", "price_gbp", "price_aed", "geram18" };
        for (var i = 0; i < keys.Length; i++)
        {
            var item = _byKey[keys[i]];
            var border = new Border { Background = new SolidColorBrush(Color.FromRgb(21, 28, 40)), BorderBrush = new SolidColorBrush(Color.FromRgb(39, 49, 67)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(15), Margin = new Thickness(i == 0 ? 0 : 7, 0, i == 4 ? 0 : 7, 0), Cursor = Cursors.Hand, Tag = item };
            border.MouseLeftButtonUp += PinnedCard_Click; var panel = new StackPanel(); var top = new Grid(); top.ColumnDefinitions.Add(new ColumnDefinition()); top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var badge = new Border { Width = 38, Height = 38, Background = item.IconBackground, CornerRadius = new CornerRadius(12), Child = new TextBlock { Text = item.Glyph, FontSize = item.Symbol == "AED" ? 10 : 18, FontWeight = FontWeights.SemiBold, Foreground = item.IconForeground, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } }; top.Children.Add(badge);
            var flagHost = new Grid { Width = 28, Height = 28 };
            flagHost.Children.Add(new TextBlock { Text = item.Icon, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = item.IconForeground, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            var flagBrush = new ImageBrush { Stretch = Stretch.UniformToFill }; BindingOperations.SetBinding(flagBrush, ImageBrush.ImageSourceProperty, new Binding(nameof(MarketItem.IconPath)) { Source = item });
            flagHost.Children.Add(new System.Windows.Shapes.Ellipse { Width = 27, Height = 27, Fill = flagBrush, Stroke = new SolidColorBrush(Color.FromRgb(55, 66, 84)), StrokeThickness = 1 }); Grid.SetColumn(flagHost, 1); top.Children.Add(flagHost); panel.Children.Add(top);
            panel.Children.Add(new TextBlock { Text = item.Name, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(143, 150, 166)), Margin = new Thickness(0, 13, 0, 3) });
            var price = new TextBlock { FontSize = 17, FontWeight = FontWeights.SemiBold }; price.SetBinding(TextBlock.TextProperty, new Binding(nameof(MarketItem.DisplayPrice)) { Source = item }); panel.Children.Add(price);
            var change = new TextBlock { FontSize = 10, Margin = new Thickness(0, 7, 0, 0) }; change.SetBinding(TextBlock.TextProperty, new Binding(nameof(MarketItem.ChangeText)) { Source = item }); change.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(MarketItem.ChangeBrush)) { Source = item }); panel.Children.Add(change);
            border.Child = panel; Grid.SetColumn(border, i); PinnedGrid.Children.Add(border);
        }
    }

    private void PinnedCard_Click(object sender, MouseButtonEventArgs e) { if (sender is Border { Tag: MarketItem item }) { _selected = item; RenderDetail(); } }
    private void MarketList_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (MarketList.SelectedItem is MarketItem item) { _selected = item; RenderDetail(); } }
    private void RenderDetail()
    {
        if (_selected is null) return; DetailName.Text = _selected.Name; DetailSymbol.Text = $"{_selected.Symbol} / {(_useToman ? "TOMAN" : "IRR")}"; DetailPrice.Text = _selected.DisplayPrice; DetailChange.Text = _selected.ChangeText; DetailChange.Foreground = _selected.ChangeBrush; LowText.Text = _selected.DisplayLow; HighText.Text = _selected.DisplayHigh; SourceTimeText.Text = _selected.TimeText; RenderChart();
    }

    private void RenderChart()
    {
        if (_selected is null || DetailChart.ActualWidth <= 1 || DetailChart.ActualHeight <= 1) return; var history = _selected.History;
        if (history.Count == 0) { DetailLine.Points.Clear(); DetailLineGlow.Points.Clear(); return; }
        var values = history.Count == 1 ? new[] { history[0], history[0] } : history.ToArray(); var min = values.Min(); var max = values.Max(); var range = max - min; if (range == 0) range = Math.Max(max * .002m, 1m); var points = new PointCollection();
        for (var i = 0; i < values.Length; i++) { var x = i * DetailChart.ActualWidth / Math.Max(1, values.Length - 1); var normalized = (double)((values[i] - min) / range); var y = DetailChart.ActualHeight - 12 - normalized * (DetailChart.ActualHeight - 24); if (max == min) y = DetailChart.ActualHeight / 2; points.Add(new Point(x, y)); }
        DetailLine.Points = points; DetailLineGlow.Points = points;
    }

    private bool FilterMarket(object value) { if (value is not MarketItem item) return false; var q = SearchBox?.Text?.Trim(); return string.IsNullOrWhiteSpace(q) || item.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) || item.Symbol.Contains(q, StringComparison.OrdinalIgnoreCase); }
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed; _marketView?.Refresh(); }
    private void Toman_Click(object sender, RoutedEventArgs e) => SetUnit(true);
    private void Rial_Click(object sender, RoutedEventArgs e) => SetUnit(false);
    private void SetUnit(bool toman)
    {
        _useToman = toman; foreach (var item in _markets) item.UseToman = toman; TomanButton.Background = toman ? new SolidColorBrush(Color.FromRgb(42, 37, 75)) : Brushes.Transparent; RialButton.Background = !toman ? new SolidColorBrush(Color.FromRgb(42, 37, 75)) : Brushes.Transparent; TomanButton.Foreground = toman ? Brushes.White : new SolidColorBrush(Color.FromRgb(139, 150, 169)); RialButton.Foreground = !toman ? Brushes.White : new SolidColorBrush(Color.FromRgb(139, 150, 169)); RenderDetail();
    }
    private void DashboardNav_Click(object sender, RoutedEventArgs e) { PinnedGrid.Visibility = Visibility.Visible; AnalysisGrid.Visibility = Visibility.Visible; PageTitle.Text = "نبض بازار"; PageSubtitle.Text = "قیمت لحظه‌ای ارز و طلا در بازار ایران"; MarketList.MaxHeight = 310; SetNav(true); }
    private void MarketsNav_Click(object sender, RoutedEventArgs e) { PinnedGrid.Visibility = Visibility.Collapsed; AnalysisGrid.Visibility = Visibility.Collapsed; PageTitle.Text = "همه بازارها"; PageSubtitle.Text = $"{_markets.Count} ارز جهانی با قیمت بازار ایران"; MarketList.MaxHeight = double.PositiveInfinity; SetNav(false); }
    private void SetNav(bool dashboard) { DashboardNav.Background = dashboard ? new SolidColorBrush(Color.FromRgb(42, 37, 75)) : Brushes.Transparent; MarketsNav.Background = !dashboard ? new SolidColorBrush(Color.FromRgb(42, 37, 75)) : Brushes.Transparent; DashboardNav.Foreground = dashboard ? Brushes.White : new SolidColorBrush(Color.FromRgb(139, 150, 169)); MarketsNav.Foreground = !dashboard ? Brushes.White : new SolidColorBrush(Color.FromRgb(139, 150, 169)); }
    private void DetailChart_SizeChanged(object sender, SizeChangedEventArgs e) => RenderChart();
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 2) ToggleMaximize(); else DragMove(); }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_Closed(object? sender, EventArgs e) { _timer.Stop(); _shutdown.Cancel(); _shutdown.Dispose(); _service.Dispose(); }
}
