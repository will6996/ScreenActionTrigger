using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.UI.ViewModels;

public sealed partial class RulesViewModel : ObservableObject
{
    public ObservableCollection<VisualRule> Rules { get; } = new();

    [ObservableProperty] private VisualRule?  _selectedRule;
    [ObservableProperty] private string       _filterText  = string.Empty;
    [ObservableProperty] private bool         _showDisabled = true;

    private List<MonitoredRegion> _regions = new();
    private List<Template>        _templates = new();

    public IEnumerable<MonitoredRegion> AvailableRegions  => _regions;
    public IEnumerable<Template>        AvailableTemplates => _templates;

    public void SetProfile(ExecutionProfile profile)
    {
        _regions   = profile.Regions;
        _templates = profile.Templates;
        Rules.Clear();
        foreach (var r in profile.Rules) Rules.Add(r);
        OnPropertyChanged(nameof(AvailableRegions));
        OnPropertyChanged(nameof(AvailableTemplates));
    }

    [RelayCommand]
    private void AddRule()
    {
        var region = _regions.FirstOrDefault();
        var rule = new VisualRule
        {
            Name     = $"Regra {Rules.Count + 1}",
            RegionId = region?.Id ?? Guid.Empty,
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

    public IEnumerable<VisualRule> FilteredRules =>
        Rules.Where(r =>
            (ShowDisabled || r.IsEnabled) &&
            (string.IsNullOrEmpty(FilterText) ||
             r.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase)));

    partial void OnFilterTextChanged(string value)  => OnPropertyChanged(nameof(FilteredRules));
    partial void OnShowDisabledChanged(bool value)  => OnPropertyChanged(nameof(FilteredRules));
}
