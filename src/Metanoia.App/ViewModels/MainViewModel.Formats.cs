using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Metanoia.Core.Models;
using Metanoia.Core.Services;
using Metanoia.Infrastructure.FFmpeg;
using Microsoft.Win32;

namespace Metanoia.App.ViewModels;

public partial class MainViewModel
{
    private IReadOnlyList<ExportContainer> _allContainers = [];
    private Task? _capabilityTask;
    public ObservableCollection<ExportContainer> CompatibleContainers { get; } = [];
    [ObservableProperty] private ExportContainer? _selectedContainer;
    [ObservableProperty] private string _catalogOutputPath = "";
    [ObservableProperty] private string _capabilitySummary = "Detectando formatos del motor instalado…";
    [ObservableProperty] private string _installedFilters = "";
    [ObservableProperty] private string _catalogCommandDisplay = "";
    [ObservableProperty] private bool _catalogResultAvailable;

    private async Task EnsureCapabilitiesAsync()
    {
        if (_settings.FFmpegPath is null) return;
        _capabilityTask ??= DiscoverAsync();
        try { await _capabilityTask; RefreshCompatibleContainers(); }
        catch (Exception ex) { CapabilitySummary = "No se pudo consultar el catálogo: " + ex.Message; }
        async Task DiscoverAsync()
        {
            var service = new FFmpegCapabilities(_settings.FFmpegPath);
            InstalledFilters = await service.DiscoverFiltersAsync();
            _allContainers = await service.DiscoverContainersAsync();
        }
    }
    public Task LoadFormatCatalogAsync() => EnsureCapabilitiesAsync();

    private void RefreshCompatibleContainers()
    {
        RefreshAvailableFilters();
        CompatibleContainers.Clear();
        foreach (var format in _allContainers.Where(f => IsAudioLoaded ? f.Audio : IsImageLoaded ? f.Video : f.Video || f.Audio)) CompatibleContainers.Add(format);
        CapabilitySummary = $"{CompatibleContainers.Count} contenedores detectados · códec automático del formato";
        SelectedContainer = CompatibleContainers.FirstOrDefault(f => f.Name == (IsAudioLoaded ? "flac" : IsImageLoaded ? "image2" : "matroska")) ?? CompatibleContainers.FirstOrDefault();
    }

    partial void OnSelectedContainerChanged(ExportContainer? value)
    {
        if (value is null || string.IsNullOrEmpty(SelectedFilePath)) return;
        CatalogOutputPath = Path.Combine(Path.GetDirectoryName(SelectedFilePath)!, Path.GetFileNameWithoutExtension(SelectedFilePath) + "-metanoia." + value.Extension);
        RefreshCatalogCommand();
    }
    partial void OnCatalogOutputPathChanged(string value) => RefreshCatalogCommand();
    private void RefreshCatalogCommand()
    {
        try { CatalogCommandDisplay = BuildCatalogCommand().ToDisplayString(); }
        catch (Exception ex) { CatalogCommandDisplay = ex.Message; }
    }

    [RelayCommand]
    private void SelectCatalogOutput()
    {
        if (SelectedContainer is not { } format) return;
        var dialog = new SaveFileDialog { FileName = Path.GetFileName(CatalogOutputPath), InitialDirectory = Path.GetDirectoryName(CatalogOutputPath), DefaultExt = "." + format.Extension, Filter = "Archivo de salida|*." + format.Extension, AddExtension = true };
        if (dialog.ShowDialog() == true) CatalogOutputPath = dialog.FileName;
    }

    private FFmpegCommand BuildCatalogCommand(AudioLoudnessMeasurement? measurement = null)
    {
        if (_probeResult is null || _commandBuilder is null || SelectedContainer is not { } format) throw new InvalidOperationException("Selecciona un archivo y un formato.");
        if (!Path.IsPathFullyQualified(CatalogOutputPath) || !Directory.Exists(Path.GetDirectoryName(CatalogOutputPath))) throw new ArgumentException("Elige una carpeta de salida válida.");
        if (string.Equals(Path.GetFullPath(CatalogOutputPath), SelectedFilePath, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("La salida no puede sobrescribir el original.");
        // Reuse typed transforms and two-pass audio rules; only replace encoding and container settings.
        var command = IsAudioLoaded
            ? _commandBuilder.BuildAudioConversion(_probeResult, CreateAudioOptions(measurement) with { OutputPath = Path.ChangeExtension(CatalogOutputPath, ".wav"), Format = AudioOutputFormat.Wav })
            : IsImageLoaded
                ? _commandBuilder.BuildImageConversion(_probeResult, CreateImageOptions(Path.ChangeExtension(CatalogOutputPath, ".png"), ImageOutputFormat.Png))
                : _commandBuilder.BuildVideoConversion(_probeResult, CreateOptions() with { OutputPath = Path.ChangeExtension(CatalogOutputPath, format.Video ? ".mp4" : ".mp3"), OutputMode = format.Video ? VideoOutputMode.Mp4 : VideoOutputMode.AudioMp3 });
        return ContainerCommandAdapter.Adapt(command, CatalogOutputPath, format.Name, IsAudioLoaded || !format.Audio, IsImageLoaded);
    }

    [RelayCommand]
    private async Task ExportCatalogAsync()
    {
        if (IsBusy || _runner is null || _probeResult is null) return;
        ProcessingJob? job = null;
        CatalogResultAvailable = false;
        try
        {
            // Freeze options during export, matching the main exporter.
            IsExporting = IsBusy = IsProcessing = true;
            _processingCancellation = new CancellationTokenSource();
            AudioLoudnessMeasurement? measurement = null;
            if (IsAudioLoaded && NormalizeAudio)
            {
                var analysis = _commandBuilder!.BuildAudioLoudnessAnalysis(_probeResult, CreateAudioOptions(null));
                var measured = await _runner.RunAsync(analysis, GetExpectedAudioDuration(), cancellationToken: _processingCancellation.Token);
                if (measured.WasCancelled) throw new OperationCanceledException();
                if (!measured.Succeeded) throw new InvalidOperationException(measured.StandardError);
                measurement = new LoudnessAnalysisParser().Parse(measured.StandardError);
            }
            var command = BuildCatalogCommand(measurement);
            CatalogCommandDisplay = command.ToDisplayString();
            job = new ProcessingJob(SelectedFilePath, CatalogOutputPath); _queue.Enqueue(job); job.UpdateStatus(JobStatus.Processing); _queue.Update(job);
            var result = await _runner.RunAsync(command, _probeResult.Duration, new Progress<JobProgress>(p => ProgressPercent = p.Percent ?? 0), _processingCancellation.Token);
            if (result.WasCancelled) throw new OperationCanceledException();
            if (!result.Succeeded) throw new InvalidOperationException(Truncate(result.StandardError, 1800));
            await _probeService!.ProbeAsync(CatalogOutputPath);
            CatalogResultAvailable = true;
            job.UpdateStatus(JobStatus.Completed); _queue.Update(job); ProgressPercent = 100; StatusText = "Exportación completada: " + CatalogOutputPath;
        }
        catch (OperationCanceledException)
        {
            job?.UpdateStatus(JobStatus.Cancelled); if (job is not null) _queue.Update(job);
            if (job is not null) DeleteIncompleteOutput(job.OutputPath); StatusText = "Exportación cancelada";
        }
        catch (Exception ex)
        {
            job?.UpdateStatus(JobStatus.Error, ex.Message); if (job is not null) _queue.Update(job);
            StatusText = "El formato no pudo exportarse: " + ex.Message;
        }
        finally { IsExporting = IsBusy = IsProcessing = false; _processingCancellation?.Dispose(); _processingCancellation = null; }
    }
}

