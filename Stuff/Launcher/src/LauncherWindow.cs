using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameLauncher;

internal sealed class LauncherWindow : Window
{
    private static readonly Brush TextBrush = Hex("#C9C9D4");
    private static readonly Brush ErrorBrush = Hex("#FF7B7B");

    private readonly TextBlock _status;
    private readonly TextBlock _detail;
    private readonly ProgressBar _bar;
    private readonly StackPanel _buttons;
    private TaskCompletionSource<bool>? _choice;

    /// <summary>Cancelled when the user closes the window.</summary>
    public CancellationTokenSource Cancel { get; } = new();

    public LauncherWindow(string appName, BitmapImage? logo, IReadOnlyList<string> news)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Title = appName;

        var panel = new StackPanel { Margin = new Thickness(32, 30, 32, 28) };

        if (logo != null)
            panel.Children.Add(new Image
            {
                Source = logo, MaxHeight = 120, MaxWidth = 300, Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 0, 16), HorizontalAlignment = HorizontalAlignment.Center
            });

        panel.Children.Add(new TextBlock
        {
            Text = appName, FontSize = 28, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 18)
        });

        _status = new TextBlock
        {
            Text = "Checking for updates...", FontSize = 14, Foreground = TextBrush,
            TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap
        };
        panel.Children.Add(_status);

        _bar = new ProgressBar
        {
            Height = 6, Minimum = 0, Maximum = 1, IsIndeterminate = true, BorderThickness = new Thickness(0),
            Foreground = Hex("#4C8DFF"), Background = Hex("#33333F"), Margin = new Thickness(0, 12, 0, 6)
        };
        panel.Children.Add(_bar);

        _detail = new TextBlock
        {
            FontSize = 11, Foreground = Hex("#8A8A99"), TextAlignment = TextAlignment.Center, MinHeight = 16
        };
        panel.Children.Add(_detail);

        var retry = new Button { Content = "Retry", Padding = new Thickness(20, 5, 20, 5), Margin = new Thickness(0, 0, 8, 0) };
        var exit = new Button { Content = "Exit", Padding = new Thickness(20, 5, 20, 5) };
        retry.Click += (_, _) => _choice?.TrySetResult(true);
        exit.Click += (_, _) => _choice?.TrySetResult(false);
        _buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 14, 0, 0), Visibility = Visibility.Collapsed
        };
        _buttons.Children.Add(retry);
        _buttons.Children.Add(exit);
        panel.Children.Add(_buttons);

        if (news.Count > 0)
        {
            panel.Children.Add(new Border
            {
                BorderBrush = Hex("#33333F"), BorderThickness = new Thickness(0, 1, 0, 0),
                               Margin = new Thickness(0, 20, 0, 0), Padding = new Thickness(0, 14, 0, 0),
                               Child = new TextBlock
                               {
                                   Text = string.Join(Environment.NewLine + Environment.NewLine, news),
                               FontSize = 12, Foreground = Hex("#B8B8C4"), TextWrapping = TextWrapping.Wrap
                               }
            });
        }

        var close = new TextBlock
        {
            Text = "✕", FontSize = 13, Foreground = Hex("#8A8A99"), Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 8, 12, 0)
        };
        close.MouseLeftButtonUp += (_, _) => Close();

        var grid = new Grid();
        grid.Children.Add(panel);
        grid.Children.Add(close);

        Content = new Border
        {
            CornerRadius = new CornerRadius(10), Background = Hex("#1E1E26"),
            BorderBrush = Hex("#33333F"), BorderThickness = new Thickness(1), Child = grid
        };

        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        Closed += (_, _) => { Cancel.Cancel(); _choice?.TrySetResult(false); };
    }

    public void SetStatus(string text)
    {
        _status.Text = text;
        _status.Foreground = TextBrush;
    }

    /// <summary>fraction 0..1, or null for an indeterminate bar.</summary>
    public void SetProgress(double? fraction, string detail = "")
    {
        if (fraction is { } f) { _bar.IsIndeterminate = false; _bar.Value = Math.Max(0, Math.Min(1, f)); }
        else _bar.IsIndeterminate = true;
        _detail.Text = detail;
    }

    /// <summary>Shows an error with Retry/Exit buttons. Returns true if the user chose Retry.</summary>
    public async Task<bool> ShowErrorAsync(string message)
    {
        _status.Text = message;
        _status.Foreground = ErrorBrush;
        _bar.IsIndeterminate = false;
        _bar.Value = 0;
        _detail.Text = "";
        _buttons.Visibility = Visibility.Visible;

        _choice = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool retry = await _choice.Task;

        _buttons.Visibility = Visibility.Collapsed;
        _status.Foreground = TextBrush;
        if (retry) _bar.IsIndeterminate = true;
        return retry;
    }

    private static Brush Hex(string color)
    {
        var b = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
        b.Freeze();
        return b;
    }
}
