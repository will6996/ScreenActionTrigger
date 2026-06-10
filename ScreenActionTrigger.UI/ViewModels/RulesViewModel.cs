using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenActionTrigger.Core;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;
using ScreenActionTrigger.Vision.Detectors;

namespace ScreenActionTrigger.UI.ViewModels;

public sealed partial class RulesViewModel : ObservableObject
{
    private readonly RegionsViewModel        _regionsVm;
    private readonly TemplatesViewModel      _templatesVm;
    private readonly IScreenCaptureService     _capture;

    public ObservableCollection<VisualRule> Rules { get; } = new();
    public ICollectionView RulesView { get; }

    [ObservableProperty] private VisualRule?  _selectedRule;
    [ObservableProperty] private string       _filterText  = string.Empty;
    [ObservableProperty] private bool         _showDisabled = true;
    [ObservableProperty] private string       _newExtraColor = "#FF0000";
    [ObservableProperty] private string?      _extraColorError;

    public IEnumerable<MonitoredRegion> AvailableRegions   => _regionsVm.Regions;
    public IEnumerable<Template>        AvailableTemplates => _templatesVm.Templates;
    public IReadOnlyList<ColorPreset>   PresetColors       => Core.Models.ColorPresets.All;

    public RulesViewModel(
        RegionsViewModel regionsVm,
        TemplatesViewModel templatesVm,
        IScreenCaptureService capture)
    {
        _regionsVm   = regionsVm;
        _templatesVm = templatesVm;
        _capture     = capture;

        _regionsVm.Regions.CollectionChanged   += OnRegionsChanged;
        _templatesVm.Templates.CollectionChanged += OnTemplatesChanged;

        RulesView = CollectionViewSource.GetDefaultView(Rules);
        RulesView.Filter = FilterRule;
    }

    private bool FilterRule(object obj)
    {
        if (obj is not VisualRule r) return false;
        return (ShowDisabled || r.IsEnabled) &&
               (string.IsNullOrEmpty(FilterText) ||
                r.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshRulesView() => RulesView.Refresh();

    public bool HasRegions => _regionsVm.Regions.Count > 0;

    private void OnRegionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(AvailableRegions));
        OnPropertyChanged(nameof(HasRegions));
    }

    private void OnTemplatesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => OnPropertyChanged(nameof(AvailableTemplates));

    public void SetProfile(ExecutionProfile profile)
    {
        Rules.Clear();
        foreach (var r in profile.Rules)
        {
            ProfileRepair.EnsureRuleCollections(r);
            Rules.Add(r);
        }
        OnPropertyChanged(nameof(AvailableRegions));
        OnPropertyChanged(nameof(AvailableTemplates));
        RefreshRulesView();
    }

    [RelayCommand]
    private void AddRule()
    {
        var region = _regionsVm.Regions.FirstOrDefault();
        var rule = new VisualRule
        {
            Name      = $"Regra {Rules.Count + 1}",
            RegionId  = region?.Id ?? Guid.Empty,
            Condition = new RuleCondition
            {
                Type               = ConditionType.ColorDetection,
                MinMatchingPixels  = 8,
                MinColorPercentage = 0.03,
                ColorTolerance     = 28,
                ExcludeDarkPixels  = true
            }
        };
        Rules.Add(rule);
        SelectedRule = rule;
    }

    [RelayCommand]
    private void DuplicateRule(VisualRule? rule)
    {
        if (rule is null) return;
        var clone = rule.Clone();
        Rules.Add(clone);
        SelectedRule = clone;
    }

    [RelayCommand]
    private void RemoveRule(VisualRule? rule) => RemoveRuleCore(rule);

    [RelayCommand(CanExecute = nameof(HasSelectedRule))]
    private void RemoveSelectedRule() => RemoveRuleCore(SelectedRule);

    private bool HasSelectedRule() => SelectedRule is not null;

    private void RemoveRuleCore(VisualRule? rule)
    {
        if (rule is null) return;

        var target = Rules.Contains(rule) ? rule : Rules.FirstOrDefault(r => r.Id == rule.Id);
        if (target is null) return;

        Rules.Remove(target);
        if (SelectedRule == target || SelectedRule?.Id == target.Id)
            SelectedRule = null;

        RefreshRulesView();
    }

    partial void OnSelectedRuleChanged(VisualRule? value)
        => RemoveSelectedRuleCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void ToggleRule(VisualRule? rule)
    {
        if (rule is null) return;
        rule.IsEnabled = !rule.IsEnabled;
        OnPropertyChanged(nameof(Rules));
    }

    [RelayCommand]
    private void ResetRule(VisualRule? rule) => rule?.Reset();

    [RelayCommand]
    private void ResetAllRules()
    {
        foreach (var r in Rules) r.Reset();
    }

    [RelayCommand]
    private void AddActionToRule(object? parameter)
    {
        var rule = parameter as VisualRule ?? SelectedRule;
        if (rule is null) return;
        rule.Actions.Add(new TriggerAction { Type = ActionType.MouseLeftClick });
        OnPropertyChanged(nameof(SelectedRule));
    }

    [RelayCommand]
    private void RemoveActionFromRule(object? parameter)
    {
        if (parameter is not (VisualRule rule, TriggerAction action)) return;
        rule.Actions.Remove(action);
        OnPropertyChanged(nameof(SelectedRule));
    }

    [RelayCommand]
    private void ApplyColorPreset(ColorPreset? preset)
    {
        if (preset is null || SelectedRule is null) return;
        SelectedRule.Condition.TargetColor = preset.Hex;
        OnPropertyChanged(nameof(SelectedRule));
    }

    [RelayCommand]
    private void PickColorFromScreen()
    {
        if (SelectedRule is null) return;
        ColorPickRequested?.Invoke(this, SelectedRule.Condition);
    }

    [RelayCommand]
    private async Task SampleColorFromRegionAsync()
    {
        if (SelectedRule is null) return;

        var region = _regionsVm.Regions.FirstOrDefault(r => r.Id == SelectedRule.RegionId);
        if (region is null)
        {
            ExtraColorError = "Selecione uma região válida para amostrar a cor.";
            return;
        }

        ExtraColorError = null;
        var frame = await _capture.CaptureRegionAsync(region);
        if (frame is null || frame.Length == 0)
        {
            ExtraColorError = "Não foi possível capturar a região. Verifique posição/tamanho.";
            return;
        }

        var colors = ColorDetector.FindTopColors(frame, 3, SelectedRule.Condition.DarkPixelThreshold);
        if (colors.Count == 0)
        {
            ExtraColorError = "Nenhuma cor encontrada — região muito escura ou vazia.";
            return;
        }

        SelectedRule.Condition.TargetColor = colors[0];
        foreach (var hex in colors.Skip(1))
        {
            if (!SelectedRule.Condition.TargetColors.Any(c =>
                    string.Equals(c, hex, StringComparison.OrdinalIgnoreCase)))
                SelectedRule.Condition.TargetColors.Add(hex);
        }

        ProfileRepair.RepairColorDetectionDefaults(SelectedRule.Condition);
        OnPropertyChanged(nameof(SelectedRule));
    }

    [RelayCommand]
    private void PickExtraColorFromScreen()
    {
        ExtraColorPickRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyPickedExtraColor(string hex)
    {
        NewExtraColor = hex;
        OnPropertyChanged(nameof(NewExtraColor));
    }

    [RelayCommand]
    private void AddExtraTargetColor()
    {
        ExtraColorError = null;
        if (SelectedRule is null || string.IsNullOrWhiteSpace(NewExtraColor))
            return;

        if (!ColorHexHelper.TryNormalize(NewExtraColor, out var hex))
        {
            ExtraColorError = "Cor inválida. Use #RRGGBB (ex: #FF0000)";
            return;
        }

        var colors = SelectedRule.Condition.TargetColors;
        if (!colors.Any(c => string.Equals(c, hex, StringComparison.OrdinalIgnoreCase)))
            colors.Add(hex);

        NewExtraColor = "#FF0000";
    }

    [RelayCommand]
    private void RemoveExtraTargetColor(string? color)
    {
        if (SelectedRule is null || color is null) return;
        SelectedRule.Condition.TargetColors.Remove(color);
    }

    [RelayCommand]
    private void PickClickPoint(TriggerAction? action)
    {
        if (action is null) return;
        ClickPointPickRequested?.Invoke(this, action);
    }

    public event EventHandler? OverlayPreviewRequested;

    public void ApplyPickedClickPoint(TriggerAction action, int x, int y)
    {
        action.TargetX = x;
        action.TargetY = y;
        action.UseDetectionCoordinates = false;
        OnPropertyChanged(nameof(SelectedRule));
        OverlayPreviewRequested?.Invoke(this, EventArgs.Empty);
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
        OnPropertyChanged(nameof(SelectedRule));
    }

    [RelayCommand]
    private void SetCompositeOperator(object? parameter)
    {
        if (parameter is not (VisualRule rule, LogicalOperator op)) return;
        if (rule.Condition.Type != ConditionType.Composite)
        {
            var old = rule.Condition;
            rule.Condition = new RuleCondition
            {
                Type = ConditionType.Composite,
                Operator = op,
                SubConditions = new List<RuleCondition> { old }
            };
        }
        else
        {
            rule.Condition.Operator = op;
        }
        OnPropertyChanged(nameof(SelectedRule));
    }

    public void ApplyPickedColor(RuleCondition condition, string hex)
    {
        condition.TargetColor = hex;
        OnPropertyChanged(nameof(SelectedRule));
    }

    public void ApplyRecordedPath(TriggerAction action, IReadOnlyList<PathPoint> points)
    {
        action.PathPoints = points.ToList();
        action.Type = ActionType.MouseFollowPath;
        OnPropertyChanged(nameof(SelectedRule));
    }

    public event EventHandler<RuleCondition>? ColorPickRequested;
    public event EventHandler? ExtraColorPickRequested;
    public event EventHandler<TriggerAction>? ClickPointPickRequested;
    public event EventHandler<TriggerAction>? PathRecordingRequested;

    partial void OnFilterTextChanged(string value)  => RefreshRulesView();
    partial void OnShowDisabledChanged(bool value)  => RefreshRulesView();
}
