using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Nerkh;

public sealed class MarketItem : INotifyPropertyChanged
{
    private decimal _price, _high, _low, _change, _percent;
    private bool _useToman = true;
    private string _time = "—";
    public required string Key { get; init; }
    public required string Symbol { get; init; }
    public required string Name { get; init; }
    public required string Icon { get; init; }
    public required string Glyph { get; init; }
    public string? IconPath { get; init; }
    public bool IsGold { get; init; }
    public Brush IconBackground => IsGold ? new SolidColorBrush(Color.FromRgb(61, 48, 25)) : new SolidColorBrush(Color.FromRgb(35, 44, 61));
    public Brush IconForeground => IsGold ? new SolidColorBrush(Color.FromRgb(243, 201, 105)) : Brushes.White;
    public List<decimal> History { get; } = [];
    public decimal Price => _price;
    public decimal High => _high;
    public decimal Low => _low;
    public decimal Percent => _percent;
    public string TimeText => _time;
    public bool UseToman { get => _useToman; set { if (_useToman == value) return; _useToman = value; RaiseDisplay(); } }
    private decimal Unit(decimal value) => _useToman ? value / 10m : value;
    public string DisplayPrice => Format(Unit(_price));
    public string DisplayHigh => Format(Unit(_high));
    public string DisplayLow => Format(Unit(_low));
    public string ChangeText => _percent switch { > 0 => $"▲ {_percent:0.##}%", < 0 => $"▼ {Math.Abs(_percent):0.##}%", _ => "— 0%" };
    public Brush ChangeBrush => _percent switch { > 0 => Brushes.MediumSpringGreen, < 0 => new SolidColorBrush(Color.FromRgb(251, 113, 133)), _ => new SolidColorBrush(Color.FromRgb(143, 150, 166)) };
    public void Update(decimal price, decimal high, decimal low, decimal change, decimal percent, string time)
    {
        _price = price; _high = high; _low = low; _change = change; _percent = percent; _time = time;
        History.Add(price); if (History.Count > 120) History.RemoveAt(0); RaiseAll();
    }
    private static string Format(decimal value) => value == 0 ? "—" : value >= 1 ? Math.Round(value).ToString("N0", CultureInfo.InvariantCulture) : value.ToString("0.####", CultureInfo.InvariantCulture);
    private void RaiseDisplay() { OnPropertyChanged(nameof(DisplayPrice)); OnPropertyChanged(nameof(DisplayHigh)); OnPropertyChanged(nameof(DisplayLow)); }
    private void RaiseAll() { OnPropertyChanged(nameof(Price)); OnPropertyChanged(nameof(High)); OnPropertyChanged(nameof(Low)); OnPropertyChanged(nameof(Percent)); OnPropertyChanged(nameof(TimeText)); OnPropertyChanged(nameof(DisplayPrice)); OnPropertyChanged(nameof(DisplayHigh)); OnPropertyChanged(nameof(DisplayLow)); OnPropertyChanged(nameof(ChangeText)); OnPropertyChanged(nameof(ChangeBrush)); }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public event PropertyChangedEventHandler? PropertyChanged;
}
