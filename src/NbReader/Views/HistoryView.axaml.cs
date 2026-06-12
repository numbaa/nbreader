using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using NbReader.Core.Models;
using NbReader.ViewModels;

namespace NbReader.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 点击历史条目 → 打开漫画。
    /// </summary>
    private void OnHistoryEntryPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.DataContext is not ReadHistoryEntry entry) return;
        if (DataContext is not HistoryViewModel vm) return;

        vm.OpenEntryCommand.Execute(entry);
    }
}
