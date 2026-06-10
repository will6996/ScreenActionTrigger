namespace ScreenActionTrigger.Core.Models;

/// <summary>XAML-accessible static list of ConditionType values.</summary>
public static class ConditionTypeValues
{
    public static IReadOnlyList<ConditionType> All { get; } =
        Enum.GetValues<ConditionType>();
}

/// <summary>XAML-accessible static list of ActionType values.</summary>
public static class ActionTypeValues
{
    public static IReadOnlyList<ActionType> All { get; } =
        Enum.GetValues<ActionType>();
}

/// <summary>XAML-accessible static list of MatchingMethod values.</summary>
public static class MatchingMethodValues
{
    public static IReadOnlyList<MatchingMethod> All { get; } =
        Enum.GetValues<MatchingMethod>();
}

/// <summary>XAML-accessible static list of LogicalOperator values.</summary>
public static class LogicalOperatorValues
{
    public static IReadOnlyList<LogicalOperator> All { get; } =
        Enum.GetValues<LogicalOperator>();
}
