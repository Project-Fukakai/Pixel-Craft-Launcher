using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;

namespace Pixel_Craft_Launcher.Controls;

public class FontSelector : ContentControl
{
    public delegate void SelectionChangedEventHandler(object sender, SelectionChangedEventArgs e);

    public static readonly StyledProperty<string?> TooltipProperty =
        AvaloniaProperty.Register<FontSelector, string?>(nameof(Tooltip));

    private readonly MyComboBox _comboFont = new();
    private bool _isInitializing;
    private string? _pendingFontTag;
    private bool _loadedFonts;
    private bool _requestedIsEnabled = true;

    public FontSelector()
    {
        MinHeight = 36;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        Content = _comboFont;
        _comboFont.MinHeight = 36;
        _comboFont.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        _comboFont.ItemsSource = CustomFontCollection;
        _comboFont.SelectionChanged += ComboFontOnSelectionChanged;
    }

    public string? Tooltip
    {
        get => GetValue(TooltipProperty);
        set => SetValue(TooltipProperty, value);
    }

    public ObservableCollection<CustomFontProperties> CustomFontCollection { get; } = new();

    public string SelectedFontTag
    {
        get => _comboFont.SelectedItem is CustomFontProperties selected ? selected.Tag : string.Empty;
        set
        {
            if (!_loadedFonts)
            {
                _pendingFontTag = value;
                return;
            }

            _isInitializing = true;
            _comboFont.SelectedItem = CustomFontCollection.FirstOrDefault(item => item.Tag == (value ?? string.Empty))
                                      ?? CustomFontCollection.FirstOrDefault();
            _isInitializing = false;
        }
    }

    public int SelectedIndex
    {
        get => _comboFont.SelectedIndex;
        set => _comboFont.SelectedIndex = value;
    }

    public new bool IsEnabled
    {
        get => _comboFont.IsEnabled;
        set
        {
            _requestedIsEnabled = value;
            _comboFont.IsEnabled = value && (!_loadedFonts || CustomFontCollection.Count > 0);
        }
    }

    public event SelectionChangedEventHandler? SelectionChanged;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_loadedFonts)
            LoadFonts();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TooltipProperty)
            ToolTip.SetTip(_comboFont, Tooltip);
    }

    private void LoadFonts()
    {
        _loadedFonts = true;
        _isInitializing = true;
        _comboFont.IsEnabled = false;
        CustomFontCollection.Clear();
        CustomFontCollection.Add(new CustomFontProperties { Name = "加载中...", Font = FontFamily.Default, Tag = string.Empty });
        _comboFont.SelectedIndex = 0;

        _ = Task.Run(() =>
        {
            return FontManager.Current.SystemFonts
                .Select(CreateFontProperties)
                .Where(item => !string.IsNullOrWhiteSpace(item.Tag))
                .GroupBy(item => item.Tag, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }).ContinueWith(task =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                CustomFontCollection.Clear();
                CustomFontCollection.Add(new CustomFontProperties { Name = "默认", Font = FontFamily.Default, Tag = string.Empty });
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    foreach (var font in task.Result)
                        CustomFontCollection.Add(font);
                }

                _comboFont.IsEnabled = _requestedIsEnabled;
                _isInitializing = false;
                SelectedFontTag = _pendingFontTag ?? string.Empty;
                _pendingFontTag = null;
            });
        });
    }

    private static CustomFontProperties CreateFontProperties(FontFamily font)
    {
        var tag = !string.IsNullOrWhiteSpace(font.Name) ? font.Name! : font.ToString();
        return new CustomFontProperties
        {
            Name = tag,
            Font = font,
            Tag = tag
        };
    }

    private void ComboFontOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitializing)
            SelectionChanged?.Invoke(sender ?? this, e);
    }

    public sealed class CustomFontProperties
    {
        public string Name { get; set; } = string.Empty;
        public FontFamily Font { get; set; } = FontFamily.Default;
        public string Tag { get; set; } = string.Empty;

        public override string ToString()
        {
            return Name;
        }
    }
}
