using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Metanoia.App.ViewModels;
using Metanoia.Core.Services;

namespace Metanoia.App.Views;

public partial class ImageWorkspaceView : UserControl
{
    private Point? _start;
    private Point _initialPan;
    public ImageWorkspaceView() => InitializeComponent();
    private MainViewModel? Model => DataContext as MainViewModel;
    private void OnMoveTool(object sender, RoutedEventArgs e) { if (Model is { } vm) { vm.ImageTool = "Mover"; ImageViewport.Cursor = Cursors.Hand; } }
    private void OnCropTool(object sender, RoutedEventArgs e)
    {
        if (Model is not { } vm) return;
        vm.ImageTool = "Recortar"; vm.ShowImageOriginal = true; ImageViewport.Cursor = Cursors.Cross; ResetTransform();
    }
    private void OnAdjustTool(object sender, RoutedEventArgs e) { if (Model is { } vm) { vm.ImageTool = "Color"; BrightnessControl.BringIntoView(); BrightnessControl.Focus(); } }
    private void OnFilterTool(object sender, RoutedEventArgs e) { if (Model is { } vm) { vm.ImageTool = "Filtros"; FilterSelector.BringIntoView(); FilterSelector.IsDropDownOpen = true; } }
    private void OnReset(object sender, RoutedEventArgs e) { ResetTransform(); CropSelection.Visibility = Visibility.Collapsed; Model?.ResetImageAdjustments(); }
    private void ResetTransform() { ImageScale.ScaleX = ImageScale.ScaleY = 1; ImagePan.X = ImagePan.Y = 0; }
    private void OnImageDown(object sender, MouseButtonEventArgs e)
    {
        if (Model?.IsImageLoaded != true) return;
        _start = e.GetPosition(ImageViewport); _initialPan = new Point(ImagePan.X, ImagePan.Y);
        ImageViewport.CaptureMouse(); e.Handled = true;
    }
    private void OnImageMove(object sender, MouseEventArgs e)
    {
        if (_start is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
        var end = e.GetPosition(ImageViewport);
        if (Model?.ImageTool == "Recortar")
        {
            CropSelection.Visibility = Visibility.Visible;
            Canvas.SetLeft(CropSelection, Math.Min(start.X, end.X)); Canvas.SetTop(CropSelection, Math.Min(start.Y, end.Y));
            CropSelection.Width = Math.Abs(end.X - start.X); CropSelection.Height = Math.Abs(end.Y - start.Y);
        }
        else { ImagePan.X = _initialPan.X + end.X - start.X; ImagePan.Y = _initialPan.Y + end.Y - start.Y; }
    }
    private void OnImageUp(object sender, MouseButtonEventArgs e)
    {
        if (_start is { } start && Model is { ImageTool: "Recortar" } vm)
        {
            var end = e.GetPosition(ImageViewport);
            var crop = ImageCropGeometry.FromViewport(vm.ImageSourceWidth, vm.ImageSourceHeight,
                ImageViewport.ActualWidth, ImageViewport.ActualHeight, start.X, start.Y, end.X, end.Y);
            if (crop is { } region) vm.ApplyImageCrop(region.X, region.Y, region.Width, region.Height);
            CropSelection.Visibility = Visibility.Collapsed;
        }
        _start = null; ImageViewport.ReleaseMouseCapture();
    }
    private void OnLostCapture(object sender, MouseEventArgs e) => _start = null;
    private void OnImageWheel(object sender, MouseWheelEventArgs e)
    {
        if (Model?.ImageTool == "Recortar") return;
        ImageScale.ScaleX = ImageScale.ScaleY = Math.Clamp(ImageScale.ScaleX * (e.Delta > 0 ? 1.12 : 1 / 1.12), .25, 8); e.Handled = true;
    }
}
