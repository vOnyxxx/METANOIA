using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Media;
using Metanoia.App.ViewModels;

namespace Metanoia.App;

public partial class ShellWindow : Window
{
    public ShellWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (HwndSource.FromHwnd(handle)?.CompositionTarget is { } composition) composition.BackgroundColor = Colors.Transparent;
            var dark = 1; DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
            var acrylic = 3; DwmSetWindowAttribute(handle, 38, ref acrylic, sizeof(int));
        };
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = ContainsSingleFile(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is MainViewModel viewModel &&
            e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } files)
        {
            await viewModel.LoadFileAsync(files[0]);
        }
    }

    private static bool ContainsSingleFile(IDataObject data) =>
        data.GetDataPresent(DataFormats.FileDrop) &&
        data.GetData(DataFormats.FileDrop) is string[] { Length: 1 };
}
