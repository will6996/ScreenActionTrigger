namespace ScreenActionTrigger.Core.Models;

public static class RuleConditionExtensions
{
    public static bool UsesColorDetection(this RuleCondition condition) =>
        condition.Type switch
        {
            ConditionType.ColorDetection => true,
            ConditionType.Composite      => condition.SubConditions.Any(UsesColorDetection),
            _                            => false
        };
}
