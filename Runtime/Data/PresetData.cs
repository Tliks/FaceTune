namespace com.aoyon.facetune;

internal record class ExpressionWithCondition
{
    public List<Condition> Conditions { get; private set; }
    public List<Expression> Expressions { get; private set; }

    public ExpressionWithCondition(List<Condition> conditions, List<Expression> expressions)
    {
        Conditions = conditions;
        Expressions = expressions;
    }
}

internal interface IPatternElement {
    public string Name { get; }
}

internal record class ExpressionPattern
{
    public List<ExpressionWithCondition> ExpressionWithConditions { get; private set; }

    public ExpressionPattern(List<ExpressionWithCondition> expressionWithConditions)
    {
        ExpressionWithConditions = expressionWithConditions;
    }
}

internal record class SingleExpressionPattern : IPatternElement
{
    public string Name { get; private set; }
    public ExpressionPattern ExpressionPattern { get; private set; }

    public SingleExpressionPattern(string name, ExpressionPattern expressionPattern)
    {
        Name = name;
        ExpressionPattern = expressionPattern;
    }
}

internal record class Preset : IPatternElement
{
    public string Name { get; private set; }
    public List<ExpressionPattern> Patterns { get; private set; }

    public Preset(string presetName, List<ExpressionPattern> patterns)
    {
        Name = presetName;
        Patterns = patterns;
    }
}

internal record PatternData
{
    public IReadOnlyList<IPatternElement> OrderedItems { get; }

    public PatternData(IReadOnlyList<IPatternElement> orderedItems)
    {
        OrderedItems = orderedItems;
    }

    public static PatternData? CollectPresetData(SessionContext context)
    {
        var orderedItems = new List<IPatternElement>();
        var processedGameObjects = new HashSet<GameObject>();

        var allComponents = context.Root.GetComponentsInChildren<Component>(false);

        foreach (var component in allComponents)
        {
            if (component == null) continue;
            if (processedGameObjects.Contains(component.gameObject)) continue;

            if (component is PresetComponent presetComponent)
            {
                var patterns = presetComponent.gameObject.GetComponentsInChildren<PatternComponent>(false);
                var preset = presetComponent.GetPreset(context);
                if (preset == null) continue;
                orderedItems.Add(preset);
                
                processedGameObjects.Add(presetComponent.gameObject);
                foreach (var p in patterns)
                {
                    processedGameObjects.Add(p.gameObject);
                }
            }
            else if (component is PatternComponent patternComponent)
            {
                var pattern = patternComponent.GetPattern(context);
                if (pattern == null) continue;
                orderedItems.Add(new SingleExpressionPattern(patternComponent.gameObject.name, pattern));
                processedGameObjects.Add(patternComponent.gameObject);
            }
        }

        if (orderedItems.Count == 0) return null;
        return new PatternData(orderedItems);
    }

    // OrderedItems から、同じ型の要素が連続しているシーケンスと、その型を取得。
    public IEnumerable<(Type Type, IReadOnlyList<IPatternElement> Group)> GetConsecutiveTypeGroups()
    {
        if (OrderedItems == null || OrderedItems.Count == 0)
        {
            yield break;
        }

        var currentGroup = new List<IPatternElement>();
        Type? groupType = null; // 現在のグループの型

        foreach (var item in OrderedItems)
        {
            var currentItemType = item.GetType();

            if (groupType != null && currentItemType != groupType)
            {
                // 型が変わったので、現在のグループを返し、新しいグループを開始
                if (currentGroup.Count > 0)
                {
                    yield return (groupType, currentGroup.AsReadOnly());
                }
                currentGroup = new List<IPatternElement>();
            }

            currentGroup.Add(item);
            groupType = currentItemType; // グループの型を更新 (または最初の要素で設定)
        }

        // 最後のグループを返す
        if (currentGroup.Count > 0 && groupType != null)
        {
            yield return (groupType, currentGroup.AsReadOnly());
        }
    }

    public IEnumerable<Expression> GetAllExpressions()
    {
        var expressions = new List<Expression>();
        foreach (var orderedItem in OrderedItems)
        {
            if (orderedItem is Preset preset)
            {
                expressions.AddRange(preset.Patterns.SelectMany(p => p.ExpressionWithConditions.SelectMany(e => e.Expressions)));
            }
            else if (orderedItem is SingleExpressionPattern singleExpressionPattern)
            {
                expressions.AddRange(singleExpressionPattern.ExpressionPattern.ExpressionWithConditions.SelectMany(e => e.Expressions));
            }
        }
        return expressions;
    }
}