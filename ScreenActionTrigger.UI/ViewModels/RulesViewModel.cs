using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.UI.ViewModels;

public sealed partial class RulesViewModel : ObservableObject
{
    private readonly RegionsViewModel   _regionsVm;
    private readonly TemplatesViewModel _templatesVm;

    public ObservableCollection<VisualRule> Rules { get; } = new();

    [ObservableProperty] private VisualRule?  _selectedRule;
    [ObservableProperty] private string       _filterText  = string.Empty;
    [ObservableProperty] private bool         _showDisabled = true;
    [ObservableProperty] private string       _newExtraColor = "#FF0000";

    public IEnumerable<MonitoredRegion> AvailableRegions   => _regionsVm.Regions;
    public IEnumerable<Template>        AvailableTemplates => _templatesVm.Templates;
    public IReadOnlyList<ColorPreset>   PresetColors       => Core.Models.ColorPresets.All;

    public RulesViewModel(RegionsViewModel regionsVm, TemplatesViewModel templatesVm)
    {
        _regionsVm   = regionsVm;
        _templatesVm = templatesVm;

        _regionsVm.Regions.CollectionChanged   += OnRegionsChanged;
        _templatesVm.Templates.CollectionChanged += OnTemplatesChanged;
    }

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
        foreach (var r in profile.Rules) Rules.Add(r);
        OnPropertyChanged(nameof(AvailableRegions));
        OnPropertyChanged(nameof(AvailableTemplates));
    }

    [RelayCommand]
    private void AddRule()
    {
        var region = _regionsVm.Regions.FirstOrDefault();
        var rule = new VisualRule
        {
            Name      = $"Regra {Rules.Count + 1}",
            RegionId  = region?.Id ?? Guid.Empty,
            Condition = new RuleCondition { Type = ConditionType.ColorDetection }
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
    private void RemoveRule(VisualRule? rule)
    {
        if (rule is null) return;
        Rules.Remove(rule);
        if (SelectedRule == rule) SelectedRule = null;
    }

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
    private void AddExtraTargetColor()
    {
        if (SelectedRule is null || string.IsNullOrWhiteSpace(NewExtraColor)) return;
        SelectedRule.Condition.TargetColors.Add(NewExtraColor.Trim());
        OnPropertyChanged(nameof(SelectedRule));
    }

    [RelayCommand]
    private void RemoveExtraTargetColor(string? color)
    {
        if (SelectedRule is null || color is null) return;
        SelectedRule.Condition.TargetColors.Remove(color);
        OnPropertyChanged(nameof(SelectedRule));
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
    public event EventHandler<TriggerAction>? PathRecordingRequested;

    public IEnumerable<VisualRule> FilteredRules =>
        Rules.Where(r =>
            (ShowDisabled || r.IsEnabled) &&
            (string.IsNullOrEmpty(FilterText) ||
             r.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase)));

    partial void OnFilterTextChanged(string value)  => OnPropertyChanged(nameof(FilteredRules));
    partial void OnShowDisabledChanged(bool value)  => OnPropertyChanged(nameof(FilteredRules));
}
