using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Metanoia.Infrastructure.FFmpeg;

namespace Metanoia.App.ViewModels;

public partial class MainViewModel
{
    public ObservableCollection<string> AvailableFilters { get; } = [];
    public ObservableCollection<FilterParameterViewModel> FilterParameters { get; } = [];
    [ObservableProperty] private string _selectedEngineFilter = "";
    [ObservableProperty] private string _filterHelp = "Selecciona un filtro del motor instalado.";
    [ObservableProperty] private string _customFilterParameters = "";
    [ObservableProperty] private string _activeImageFilter = "";
    [ObservableProperty] private string _activeVideoFilter = "";
    [ObservableProperty] private string _activeAudioFilter = "";
    [ObservableProperty] private string _filterStatus = "Sin filtro adicional";

    private void RefreshAvailableFilters()
    {
        AvailableFilters.Clear();
        var signature = IsAudioLoaded ? "A->A" : "V->V";
        foreach (Match match in Regex.Matches(InstalledFilters, @"(?m)^\s+[TSC.]+\s+(\w+)\s+(\S+)\s+(.+)$"))
            if (match.Groups[2].Value == signature) AvailableFilters.Add(match.Groups[1].Value);
    }

    [RelayCommand]
    private async Task InspectFilterAsync()
    {
        if (_settings.FFmpegPath is null || !AvailableFilters.Contains(SelectedEngineFilter)) return;
        var selected = SelectedEngineFilter;
        try
        {
            var help = await new FFmpegCapabilities(_settings.FFmpegPath).GetFilterHelpAsync(selected);
            if (selected != SelectedEngineFilter) return;
            FilterHelp = help; FilterParameters.Clear(); CustomFilterParameters = "";
            foreach (Match option in Regex.Matches(help, @"(?m)^\s+(\w+)\s+<(int|float|double)>.*?\(from ([^ ]+) to ([^)]+)\).*?\(default ([^)]+)\)"))
            {
                if (!double.TryParse(option.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var min) ||
                    !double.TryParse(option.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var max) ||
                    !double.TryParse(option.Groups[5].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var initial) || max <= min) continue;
                // Extremely broad symbolic ranges remain editable in the expression field below.
                if (min < -100000 || max > 100000 || !double.IsFinite(initial)) continue;
                FilterParameters.Add(new FilterParameterViewModel { Name = option.Groups[1].Value, Minimum = min, Maximum = max, Default = initial, Value = initial, Step = option.Groups[2].Value == "int" ? 1 : .01, Integral = option.Groups[2].Value == "int" });
            }
            FilterStatus = "Configura y aplica el filtro";
        }
        catch (Exception ex) { FilterStatus = ex.Message; }
    }
    partial void OnSelectedEngineFilterChanged(string value) { _ = InspectFilterAsync(); }

    [RelayCommand]
    private void ApplyEngineFilter()
    {
        try
        {
            if (!AvailableFilters.Contains(SelectedEngineFilter)) return;
            var parameters = string.IsNullOrWhiteSpace(CustomFilterParameters)
                ? string.Join(':', FilterParameters.Where(p => Math.Abs(p.Value - p.Default) > .00001).Select(p => p.Name + "=" + p.Value.ToString("0.#####", CultureInfo.InvariantCulture)))
                : CustomFilterParameters.Trim();
            // A single filter only. No graph sources, external files or new graph branches.
            if (!Regex.IsMatch(parameters, @"^[a-zA-Z0-9_=:.'+*/() -]*$")) throw new ArgumentException("Usa parámetros simples del filtro, separados por dos puntos.");
            if (parameters.Contains("http", StringComparison.OrdinalIgnoreCase) || parameters.Contains("file", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("No se admiten fuentes externas en este control.");
            var expression = SelectedEngineFilter + (parameters.Length > 0 ? "=" + parameters : "");
            if (IsImageLoaded) { ActiveImageFilter = expression; ScheduleImagePreview(); }
            else if (IsAudioLoaded) { ActiveAudioFilter = expression; ScheduleAudioPreview(); }
            else if (HasMedia) { ActiveVideoFilter = expression; ScheduleVideoPreview(); }
            RefreshAllCommandPreviews(); FilterStatus = expression;
        }
        catch (Exception ex) { FilterStatus = ex.Message; }
    }

    [RelayCommand]
    private void ClearEngineFilter()
    {
        if (IsImageLoaded) { ActiveImageFilter = ""; ScheduleImagePreview(); }
        else if (IsAudioLoaded) { ActiveAudioFilter = ""; ScheduleAudioPreview(); }
        else { ActiveVideoFilter = ""; ScheduleVideoPreview(); }
        FilterStatus = "Sin filtro adicional"; RefreshAllCommandPreviews();
    }
}
