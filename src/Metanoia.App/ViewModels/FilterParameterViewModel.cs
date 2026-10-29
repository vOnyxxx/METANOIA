using CommunityToolkit.Mvvm.ComponentModel;

namespace Metanoia.App.ViewModels;

public partial class FilterParameterViewModel : ObservableObject
{
    public required string Name { get; init; }
    public double Minimum { get; init; }
    public double Maximum { get; init; }
    public double Step { get; init; } = .01;
    public bool Integral { get; init; }
    public double Default { get; init; }
    [ObservableProperty] private double _value;
}
