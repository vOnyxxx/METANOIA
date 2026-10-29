using System.Windows;
using Metanoia.App.ViewModels;

namespace Metanoia.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = ContainsSingleFile(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel ||
            e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: 1 } files)
        {
            return;
        }

        await viewModel.LoadFileAsync(files[0]);
    }

    private static bool ContainsSingleFile(IDataObject data) =>
        data.GetDataPresent(DataFormats.FileDrop) &&
        data.GetData(DataFormats.FileDrop) is string[] { Length: 1 };
}
