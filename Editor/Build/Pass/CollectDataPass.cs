using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Build;

internal class CollectDataPass : Pass<CollectDataPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.collect-data";
    public override string DisplayName => "Collect Data";

    protected override void Execute(BuildContext context)
    {
        if (context.GetState<BuildPassState>().TryGetBuildPassContext(out var buildPassContext) is false) return;

        var rawGroups = CollectRawGroups(buildPassContext.AvatarContext);
        var resultGroups = new List<ExpressionWithConditionGroup>();

        foreach (var rawGroup in rawGroups)
        {
            if (!rawGroup.IsBlending)
            {
                rawGroup.PrioritizeLatterExpressions();
            }
            resultGroups.Add(rawGroup.ToOptimizedGroup());
        }

        var patternData = new PatternData(resultGroups);
        context.GetState<PatternDataState>(c => new PatternDataState(patternData));
    }

    private static List<RawGroup> CollectRawGroups(AvatarContext context)
    {
        var groups = new List<RawGroup>();
        var processedGameObjects = new HashSet<GameObject>();

        var expressionWithConditions = context.Root.GetComponentsInChildren<ExpressionComponent>(true)
            .Select(c => c.GetExpressionWithConditions(context))
            .ToList();
        
        var firstExpressionWithCondition = expressionWithConditions.First();
        var isBlending = firstExpressionWithCondition.Expression.FacialSettings.EnableBlending;
        groups.Add(new RawGroup(isBlending, new List<ExpressionWithCondition>(){{firstExpressionWithCondition}}));

        foreach (var expressionWithCondition in expressionWithConditions.Skip(1))
        {
            var newIsBlending = expressionWithCondition.Expression.FacialSettings.EnableBlending;
            if (newIsBlending == isBlending) // 同じ blending の場合は同じグループに追加
            {
                groups.Last().ExpressionWithConditions.Add(expressionWithCondition);
            }
            else // 異なる blending の場合は新しいグループを作成
            {
                groups.Add(new RawGroup(newIsBlending, new List<ExpressionWithCondition>(){{expressionWithCondition}}));
                isBlending = newIsBlending;
            }
        }
        return groups;
    }

    class RawGroup
    {
        public bool IsBlending { get; private set; }
        public List<ExpressionWithCondition> ExpressionWithConditions { get; private set; }

        public RawGroup(bool isBlending, List<ExpressionWithCondition> expressionWithConditions)
        {
            IsBlending = isBlending;
            ExpressionWithConditions = expressionWithConditions;
        }

        public void PrioritizeLatterExpressions()
        {
            var originalConditions = ExpressionWithConditions.Select(e => e.Condition).ToList();
            originalConditions.Reverse();

            var prioritizedConditions = new List<ICondition>(originalConditions.Count);

            prioritizedConditions.Add(originalConditions.First());
            ICondition negatedSuffix = originalConditions.First().Not();

            foreach (var currentOriginal in originalConditions.Skip(1))
            {
                var newCondition = currentOriginal.And(negatedSuffix);
                prioritizedConditions.Add(newCondition);
                negatedSuffix = negatedSuffix.And(currentOriginal.Not());
            }

            prioritizedConditions.Reverse();

            for (int i = 0; i < prioritizedConditions.Count; i++)
            {
                ExpressionWithConditions[i] = ExpressionWithConditions[i] with { Condition = prioritizedConditions[i] };
            }
        }

        public ExpressionWithConditionGroup ToOptimizedGroup()
        {
            var dnfExpressionWithConditions = new List<ExpressionWithNormalizedCondition>();
            foreach (var expressionWithCondition in ExpressionWithConditions)
            {
                dnfExpressionWithConditions.Add(expressionWithCondition.NormalizeAndOptimize());
            }
            return new ExpressionWithConditionGroup(IsBlending, dnfExpressionWithConditions);
        }
    }
}