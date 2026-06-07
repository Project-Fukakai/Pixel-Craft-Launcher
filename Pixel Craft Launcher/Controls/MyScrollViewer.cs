using Avalonia.Controls;

namespace Pixel_Craft_Launcher.Controls;

public class MyScrollViewer : PixelScrollHostBase
{
    private Control? _content;

    public Control? Content
    {
        get => _content;
        set
        {
            if (_content == value)
                return;

            if (_content is not null)
                Children.Remove(_content);

            _content = value;
            if (_content is not null)
                Children.Add(_content);
        }
    }
}
