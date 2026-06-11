using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenActionTrigger.Core;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;
using ScreenActionTrigger.Vision.Detectors;

namespace ScreenActionTrigger.UI.ViewModels;

public sealed partial class SequencesViewModel : ObservableObject
{
    private readonly RegionsViewModel   _regionsVm;
    private readonly TemplatesViewModel _templatesVm;
    private readonly IScreenCaptureService _capture;

    public ObservableCollection<RuleSequence> Sequences { get; } = new();
    public ICollectionView SequencesView { get; }

    [ObservableProperty] private RuleSequence?  _selectedSequence;
    [ObservableProperty] private SequenceStep?  _selectedStep;
    [ObservableProperty] private string         _filterText = string.Empty;
    [ObservableProperty] private bool           _showDisabled = true;
    [ObservableProperty] private bool           _isSequencePanelExpanded = true;
    [ObservableProperty] private MonitoredRegion? _pendingInventoryRegion;
    [ObservableProperty] private string         _newExtraColor = "#FF0000";
    [ObservableProperty] private string?        _extraColorError;

    public IEnumerable<MonitoredRegion> AvailableRegions   => _regionsVm.Regions;
    public IEnumerable<Template>        AvailableTemplates => _templatesVm.Templates;
    public IReadOnlyList<ColorPreset>   PresetColors       => ColorPresets.All;

    public IEnumerable<SequenceStep> AvailableStepsForBranch =>
        SelectedSequence?.OrderedSteps.Where(s => s.Id != SelectedStep?.Id) ?? Enumerable.Empty<SequenceStep>();

    public SequencesViewModel(
        RegionsViewModel regionsVm,
        TemplatesViewModel templatesVm,
        IScreenCaptureService capture)
    {
        _regionsVm   = regionsVm;
        _templatesVm = templatesVm;
        _capture     = capture;

        _regionsVm.Regions.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(AvailableRegions));
            OnPropertyChanged(nameof(HasRegions));
        };
        _templatesVm.Templates.CollectionChanged += (_, _) =>
            OnPropertyChanged(nameof(AvailableTemplates));

        SequencesView = CollectionViewSource.GetDefaultView(Sequences);
        SequencesView.Filter = FilterSequence;
    }

    public bool HasRegions => _regionsVm.Regions.Count > 0;

    private bool FilterSequence(object obj)
    {
        if (obj is not RuleSequence s) return false;
        return (ShowDisabled || s.IsEnabled) &&
               (string.IsNullOrEmpty(FilterText) ||
                s.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshView() => SequencesView.Refresh();

    public void SetProfile(ExecutionProfile profile)
    {
        Sequences.Clear();
        foreach (var seq in profile.Sequences)
        {
            ProfileRepair.EnsureSequenceCollections(seq);
            ReindexSteps(seq);
            Sequences.Add(seq);
        }
        OnPropertyChanged(nameof(AvailableRegions));
        OnPropertyChanged(nameof(AvailableTemplates));
        RefreshView();
    }

    [RelayCommand]
    private void AddSequence()
    {
        var region = _regionsVm.Regions.FirstOrDefault();
        var seq = new RuleSequence
        {
            Name = $"Sequência {Sequences.Count + 1}",
            Steps =
            {
                CreateDefaultStep(region?.Id ?? Guid.Empty, 0)
            }
        };
        Sequences.Add(seq);
        SelectedSequence = seq;
        SelectedStep = seq.Steps.FirstOrDefault();
    }

    [RelayCommand]
    private void DuplicateSequence(RuleSequence? sequence)
    {
        if (sequence is null) return;
        var clone = sequence.Clone();
        ReindexSteps(clone);
        Sequences.Add(clone);
        SelectedSequence = clone;
    }

    [RelayCommand]
    private void RemoveSequence(RuleSequence? sequence)
    {
        if (sequence is null) return;
        Sequences.Remove(sequence);
        if (SelectedSequence == sequence)
        {
            SelectedSequence = null;
            SelectedStep = null;
        }
        RefreshView();
    }

    [RelayCommand]
    private void AddStep()
    {
        if (SelectedSequence is null) return;
        var region = _regionsVm.Regions.FirstOrDefault();
        var step = CreateDefaultStep(
            region?.Id ?? SelectedSequence.Steps.LastOrDefault()?.RegionId ?? Guid.Empty,
            SelectedSequence.Steps.Count);
        SelectedSequence.Steps.Add(step);
        ReindexSteps(SelectedSequence);
        SelectedStep = step;
        OnPropertyChanged(nameof(SelectedSequence));
    }

    [RelayCommand]
    private void RemoveStep(SequenceStep? step)
    {
        if (SelectedSequence is null || step is null) return;
        SelectedSequence.Steps.Remove(step);
        ReindexSteps(SelectedSequence);
        if (SelectedStep == step)
            SelectedStep = SelectedSequence.Steps.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedSequence));
    }

    [RelayCommand]
    private void MoveStepUp(SequenceStep? step) => MoveStep(step, -1);

    [RelayCommand]
    private void MoveStepDown(SequenceStep? step) => MoveStep(step, 1);

    private void MoveStep(SequenceStep? step, int delta)
    {
        if (SelectedSequence is null || step is null) return;
        var ordered = SelectedSequence.OrderedSteps.ToList();
        var idx = ordered.FindIndex(s => s.Id == step.Id);
        var newIdx = idx + delta;
        if (idx < 0 || newIdx < 0 || newIdx >= ordered.Count) return;

        (ordered[idx].Order, ordered[newIdx].Order) = (ordered[newIdx].Order, ordered[idx].Order);
        ReindexSteps(SelectedSequence);
        SelectedStep = step;
        OnPropertyChanged(nameof(SelectedSequence));
    }

    [RelayCommand]
    private void ToggleSequencePanel() => IsSequencePanelExpanded = !IsSequencePanelExpanded;

    [RelayCommand]
    private void AddBranchSlot()
    {
        if (SelectedStep is null) return;
        SelectedStep.BranchSlots.Add(new SequenceBranchSlot
        {
            Label = $"Se {SelectedStep.BranchSlots.Count(s => !s.IsElse) + 1}",
            Condition = new RuleCondition
            {
                Type = ConditionType.ColorDetection,
                MinMatchingPixels = 8,
                MinColorPercentage = 0.03,
                ColorTolerance = 28,
                ExcludeDarkPixels = true
            }
        });
        SelectedStep.AdvanceMode = SequenceAdvanceMode.Branch;
        NotifyStepChanged();
    }

    [RelayCommand]
    private void AddElseBranchSlot()
    {
        if (SelectedStep is null) return;
        if (SelectedStep.BranchSlots.Any(s => s.IsElse)) return;

        SelectedStep.BranchSlots.Add(new SequenceBranchSlot
        {
            Label = "Senão",
            IsElse = true
        });
        SelectedStep.AdvanceMode = SequenceAdvanceMode.Branch;
        NotifyStepChanged();
    }

    [RelayCommand]
    private void RemoveBranchSlot(SequenceBranchSlot? slot)
    {
        if (SelectedStep is null || slot is null) return;
        SelectedStep.BranchSlots.Remove(slot);
        if (SelectedStep.BranchSlots.Count == 0)
            SelectedStep.AdvanceMode = SequenceAdvanceMode.Next;
        NotifyStepChanged();
    }

    [RelayCommand]
    private void AddInventorySlot(MonitoredRegion? region)
    {
        region ??= PendingInventoryRegion;
        if (SelectedStep is null || region is null) return;
        var ids = SelectedStep.Condition.InventorySlotRegionIds;
        if (ids.Contains(region.Id)) return;
        ids.Add(region.Id);
        PendingInventoryRegion = null;
        NotifyStepChanged();
    }

    [RelayCommand]
    private void RemoveInventorySlot(Guid regionId)
    {
        if (SelectedStep is null) return;
        SelectedStep.Condition.InventorySlotRegionIds.Remove(regionId);
        NotifyStepChanged();
    }

    private void NotifyStepChanged()
    {
        OnPropertyChanged(nameof(SelectedStep));
        OnPropertyChanged(nameof(AvailableStepsForBranch));
    }

    [RelayCommand]
    private void AddActionToStep(object? parameter)
    {
        var step = parameter as SequenceStep ?? SelectedStep;
        if (step is null) return;
        step.Actions.Add(new TriggerAction
        {
            Type = ActionType.MouseLeftClick,
            UseDetectionCoordinates = false
        });
        OnPropertyChanged(nameof(SelectedStep));
    }

    [RelayCommand]
    private void RemoveActionFromStep(object? parameter)
    {
        if (parameter is not (SequenceStep step, TriggerAction action)) return;
        step.Actions.Remove(action);
        OnPropertyChanged(nameof(SelectedStep));
    }

    [RelayCommand]
    private void PickClickPoint(TriggerAction? action)
    {
        if (action is null) return;
        ClickPointPickRequested?.Invoke(this, action);
    }

    [RelayCommand]
    private void ApplyColorPreset(ColorPreset? preset)
    {
        if (preset is null || SelectedStep is null) return;
        SelectedStep.Condition.TargetColor = preset.Hex;
        OnPropertyChanged(nameof(SelectedStep));
    }

    [RelayCommand]
    private void PickColorFromScreen()
    {
        if (SelectedStep is null) return;
        ColorPickRequested?.Invoke(this, SelectedStep.Condition);
    }

    [RelayCommand]
    private async Task SampleColorFromRegionAsync()
    {
        if (SelectedStep is null) return;

        var region = _regionsVm.Regions.FirstOrDefault(r => r.Id == SelectedStep.RegionId);
        if (region is null)
        {
            ExtraColorError = "Selecione uma região válida para amostrar a cor.";
            return;
        }

        ExtraColorError = null;
        var frame = await _capture.CaptureRegionAsync(region);
        if (frame is null || frame.Length == 0)
        {
            ExtraColorError = "Não foi possível capturar a região.";
            return;
        }

        var colors = ColorDetector.FindTopColors(frame, 3, SelectedStep.Condition.DarkPixelThreshold);
        if (colors.Count == 0)
        {
            ExtraColorError = "Nenhuma cor encontrada na região.";
            return;
        }

        SelectedStep.Condition.TargetColor = colors[0];
        ProfileRepair.RepairColorDetectionDefaults(SelectedStep.Condition);
        OnPropertyChanged(nameof(SelectedStep));
    }

    [RelayCommand]
    private void RequestPathRecording(TriggerAction? action)
    {
        if (action is null) return;
        PathRecordingRequested?.Invoke(this, action);
    }

    [RelayCommand]
    private void ClearPath(TriggerAction? action)
    {
        if (action is null) return;
        action.PathPoints.Clear();
        OnPropertyChanged(nameof(SelectedStep));
    }

    public void ApplyPickedColor(RuleCondition condition, string hex)
    {
        condition.TargetColor = hex;
        OnPropertyChanged(nameof(SelectedStep));
    }

    public event EventHandler? OverlayPreviewRequested;

    public void ApplyPickedClickPoint(TriggerAction action, int x, int y)
    {
        action.TargetX = x;
        action.TargetY = y;
        action.UseDetectionCoordinates = false;
        OnPropertyChanged(nameof(SelectedStep));
        OverlayPreviewRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyRecordedPath(TriggerAction action, IReadOnlyList<PathPoint> points)
    {
        action.PathPoints = points.ToList();
        action.Type = ActionType.MouseFollowPath;
        OnPropertyChanged(nameof(SelectedStep));
    }

    partial void OnSelectedSequenceChanged(RuleSequence? value)
    {
        SelectedStep = value?.Steps.OrderBy(s => s.Order).FirstOrDefault();
        OnPropertyChanged(nameof(AvailableStepsForBranch));
    }

    partial void OnSelectedStepChanged(SequenceStep? value) =>
        OnPropertyChanged(nameof(AvailableStepsForBranch));

    partial void OnFilterTextChanged(string value) => RefreshView();
    partial void OnShowDisabledChanged(bool value) => RefreshView();

    public event EventHandler<RuleCondition>? ColorPickRequested;
    public event EventHandler<TriggerAction>? ClickPointPickRequested;
    public event EventHandler<TriggerAction>? PathRecordingRequested;

    private static SequenceStep CreateDefaultStep(Guid regionId, int order) => new()
    {
        Name = $"Passo {order + 1}",
        Order = order,
        RegionId = regionId,
        Condition = new RuleCondition
        {
            Type = ConditionType.ColorDetection,
            MinMatchingPixels = 8,
            MinColorPercentage = 0.03,
            ColorTolerance = 28,
            ExcludeDarkPixels = true
        },
        Actions =
        {
            new TriggerAction
            {
                Type = ActionType.MouseLeftClick,
                UseDetectionCoordinates = false
            }
        }
    };

    private static void ReindexSteps(RuleSequence sequence)
    {
        var ordered = sequence.Steps.OrderBy(s => s.Order).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Order = i;
            ordered[i].Name = string.IsNullOrWhiteSpace(ordered[i].Name)
                                     || ordered[i].Name.StartsWith("Passo ")
                ? $"Passo {i + 1}"
                : ordered[i].Name;
        }

        var needsRebuild = ordered.Count != sequence.Steps.Count;
        if (!needsRebuild)
        {
            for (var i = 0; i < ordered.Count; i++)
            {
                if (!ReferenceEquals(sequence.Steps[i], ordered[i]))
                {
                    needsRebuild = true;
                    break;
                }
            }
        }

        if (!needsRebuild) return;

        sequence.Steps.Clear();
        foreach (var step in ordered)
            sequence.Steps.Add(step);
    }
}
