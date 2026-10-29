using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Metanoia.Core.Models;
using Metanoia.Core.Services;

namespace Metanoia.App.ViewModels;

public partial class MainViewModel
{
    private readonly AdvancedArgumentParser _advancedArgumentParser = new();

    public ObservableCollection<AdvancedPresetViewModel> AdvancedPresets { get; } = [];

    public ObservableCollection<QueueItemViewModel> HistoryItems { get; } = [];

    [ObservableProperty]
    private bool _useAdvancedArguments;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveAdvancedPresetCommand))]
    private string _advancedArgumentsText = string.Empty;

    [ObservableProperty]
    private string _advancedValidationMessage = "Escribe opciones adicionales y valida antes de activarlas.";

    [ObservableProperty]
    private bool _areAdvancedArgumentsValid = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveAdvancedPresetCommand))]
    private string _advancedPresetName = string.Empty;

    [ObservableProperty]
    private string _sessionLogText = "METANOIA v0.6 · registro listo.\nLos eventos de la cola aparecerán aquí.";

    [ObservableProperty]
    private string _historySummary = "Sin trabajos registrados.";

    private void InitializeAdvancedState()
    {
        RefreshAdvancedPresets();
        RefreshAdvancedHistory();
    }

    [RelayCommand]
    private void ValidateAdvancedArguments()
    {
        try
        {
            var arguments = _advancedArgumentParser.Parse(AdvancedArgumentsText);
            AreAdvancedArgumentsValid = true;
            UseAdvancedArguments = arguments.Count > 0;
            AdvancedValidationMessage = arguments.Count == 0
                ? "No hay opciones adicionales activas."
                : $"Validación aprobada · {arguments.Count} argumento(s) se insertarán antes de la salida protegida.";
            RefreshAllCommandPreviews();
        }
        catch (ArgumentException exception)
        {
            AreAdvancedArgumentsValid = false;
            UseAdvancedArguments = false;
            AdvancedValidationMessage = exception.Message;
            RefreshAllCommandPreviews();
        }
    }

    [RelayCommand]
    private void ClearAdvancedArguments()
    {
        AdvancedArgumentsText = string.Empty;
        UseAdvancedArguments = false;
        AreAdvancedArgumentsValid = true;
        AdvancedValidationMessage = "Opciones avanzadas desactivadas.";
        RefreshAllCommandPreviews();
    }

    [RelayCommand(CanExecute = nameof(CanSaveAdvancedPreset))]
    private void SaveAdvancedPreset()
    {
        ValidateAdvancedArguments();
        if (!AreAdvancedArgumentsValid || string.IsNullOrWhiteSpace(AdvancedArgumentsText))
        {
            return;
        }

        var preset = new AdvancedPreset(
            Guid.NewGuid(),
            AdvancedPresetName.Trim(),
            AdvancedArgumentsText.Trim(),
            DateTimeOffset.UtcNow);
        _presetService.Save(preset);
        AdvancedPresetName = string.Empty;
        RefreshAdvancedPresets();
        AdvancedValidationMessage = $"Preset '{preset.Name}' guardado localmente.";
    }

    private bool CanSaveAdvancedPreset() =>
        !string.IsNullOrWhiteSpace(AdvancedPresetName) &&
        !string.IsNullOrWhiteSpace(AdvancedArgumentsText);

    [RelayCommand]
    private void LoadAdvancedPreset(AdvancedPresetViewModel? preset)
    {
        if (preset is null)
        {
            return;
        }

        AdvancedArgumentsText = preset.Arguments;
        ValidateAdvancedArguments();
        AdvancedValidationMessage = $"Preset '{preset.Name}' cargado y validado.";
    }

    [RelayCommand]
    private void DeleteAdvancedPreset(AdvancedPresetViewModel? preset)
    {
        if (preset is null)
        {
            return;
        }

        _presetService.Delete(preset.Id);
        RefreshAdvancedPresets();
        AdvancedValidationMessage = $"Preset '{preset.Name}' eliminado.";
    }

    [RelayCommand]
    private void RemoveHistoryItem(QueueItemViewModel? item)
    {
        if (item is not null)
        {
            _queue.Remove(item.Id);
        }
    }

    [RelayCommand]
    private void ClearCompletedHistory() => _queue.ClearCompleted();

    [RelayCommand]
    private void CopySessionLog() => _clipboard.SetText(SessionLogText);

    private FFmpegCommand ApplyAdvancedArguments(FFmpegCommand command)
    {
        if (!UseAdvancedArguments)
        {
            return command;
        }

        return _advancedArgumentParser.Apply(command, AdvancedArgumentsText);
    }

    private void RefreshAdvancedPresets()
    {
        AdvancedPresets.Clear();
        foreach (var preset in _presetService.Load().OrderByDescending(item => item.CreatedAtUtc))
        {
            AdvancedPresets.Add(AdvancedPresetViewModel.FromModel(preset));
        }
    }

    private void RefreshAdvancedHistory()
    {
        HistoryItems.Clear();
        foreach (var item in _queue.Jobs
                     .OrderByDescending(job => job.CreatedAtUtc)
                     .Take(40)
                     .Select(QueueItemViewModel.FromJob))
        {
            HistoryItems.Add(item);
        }

        var completed = _queue.Jobs.Count(job => job.Status == JobStatus.Completed);
        var errors = _queue.Jobs.Count(job => job.Status == JobStatus.Error);
        HistorySummary = $"{_queue.Jobs.Count} trabajo(s) · {completed} completado(s) · {errors} error(es)";
        SessionLogText = string.Join(Environment.NewLine, _queue.Jobs
            .OrderByDescending(job => job.CreatedAtUtc)
            .Take(80)
            .Select(job =>
                $"[{job.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}] {job.Status} · " +
                $"{Path.GetFileName(job.InputPath)} → {Path.GetFileName(job.OutputPath)}" +
                (string.IsNullOrWhiteSpace(job.ErrorMessage) ? string.Empty : $" · {job.ErrorMessage}")));
        if (string.IsNullOrWhiteSpace(SessionLogText))
        {
            SessionLogText = "METANOIA v0.6 · todavía no hay eventos de procesamiento.";
        }
    }

    private void RefreshAllCommandPreviews()
    {
        RefreshCommandPreview();
        RefreshImageCommandPreview();
        RefreshAudioCommandPreview();
    }

    partial void OnAdvancedArgumentsTextChanged(string value)
    {
        AreAdvancedArgumentsValid = false;
        UseAdvancedArguments = false;
        AdvancedValidationMessage = "Cambios pendientes de validación.";
    }
}
