using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class NormalizeDataPass : Pass<NormalizeDataPass>
{
    public override string QualifiedName => "com.aoyon.facetune.normalize-data";
    public override string DisplayName => "Normalize Data";

    protected override void Execute(BuildContext context)
    {
        // 単一の条件をPatternとして扱うことでデータを正規化する
        var conditionComponents = context.AvatarRootObject.GetComponentsInChildren<ConditionComponent>(false);
        foreach (var conditionComponent in conditionComponents)
        {
            if (conditionComponent.GetComponentInParentNullable<PatternComponent>() == null)
            {
                conditionComponent.gameObject.EnsureComponent<PatternComponent>();
            }
        }
    }
}
