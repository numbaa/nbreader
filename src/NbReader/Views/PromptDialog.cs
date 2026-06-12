using Avalonia.Controls;
using Avalonia.Input;

namespace NbReader.Views;

/// <summary>
/// 简单的文本输入弹窗（Enter 确认，Escape 取消）。
/// </summary>
public partial class PromptDialog : Window
{
    private readonly TextBox _textBox;

    /// <summary>用户输入的结果，null 表示取消。</summary>
    public string? Result { get; private set; }

    public PromptDialog(string title, string message, string initialValue = "")
    {
        Title = title;
        Width = 360;
        Height = 180;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        var panel = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = message, FontSize = 13 });

        _textBox = new TextBox { Text = initialValue, Watermark = "输入名称..." };
        panel.Children.Add(_textBox);

        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        var okBtn = new Button { Content = "确定", Width = 80 };
        okBtn.Click += (_, _) => { Result = _textBox.Text; Close(); };
        var cancelBtn = new Button { Content = "取消", Width = 80 };
        cancelBtn.Click += (_, _) => { Result = null; Close(); };
        buttons.Children.Add(okBtn);
        buttons.Children.Add(cancelBtn);
        panel.Children.Add(buttons);

        Content = panel;

        // 键盘支持：Enter 确认，Escape 取消
        _textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Result = _textBox.Text;
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Result = null;
                Close();
                e.Handled = true;
            }
        };

        // 窗口打开后自动聚焦输入框
        Opened += (_, _) => _textBox.Focus();
    }
}
