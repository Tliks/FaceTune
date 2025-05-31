namespace com.aoyon.facetune;

internal record class ExpressionWithCondition
{
    public IReadOnlyList<Condition> Conditions { get; private set; }
    public IReadOnlyList<Expression> Expressions { get; private set; }

    public ExpressionWithCondition(IReadOnlyList<Condition> conditions, IReadOnlyList<Expression> expressions)
    {
        Conditions = conditions;
        Expressions = expressions;
    }

    public void SetExpressions(IReadOnlyList<Expression> expressions)
    {
        Expressions = expressions;
    }

    public void SetConditions(IReadOnlyList<Condition> conditions)
    {
        Conditions = conditions;
    }
}

internal interface IPatternElement {
    public string Name { get; }
    public IEnumerable<ExpressionWithCondition> AllExpressionWithConditions { get; }
}

internal record class ExpressionPattern
{
    public IReadOnlyList<ExpressionWithCondition> ExpressionWithConditions { get; private set; }

    public ExpressionPattern(IReadOnlyList<ExpressionWithCondition> expressionWithConditions)
    {
        ExpressionWithConditions = expressionWithConditions;
    }

    public IEnumerable<ExpressionWithCondition> AllExpressionWithConditions => ExpressionWithConditions;
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

    public IEnumerable<ExpressionWithCondition> AllExpressionWithConditions => ExpressionPattern.AllExpressionWithConditions;
}

internal record class Preset : IPatternElement
{
    public string Name { get; private set; }
    public readonly IReadOnlyList<ExpressionPattern> Patterns;
    public readonly FacialExpression DefaultExpression;

    public readonly ParameterCondition PresetCondition;
    public GameObject? MenuTarget { get; private set; }

    public Preset(string presetName, IReadOnlyList<ExpressionPattern> patterns, FacialExpression defaultExpression, ParameterCondition presetCondition)
    {
        Name = presetName;
        Patterns = patterns;
        DefaultExpression = defaultExpression;
        PresetCondition = presetCondition;
    }

    public void SetMenuTarget(GameObject menuTarget)
    {
        MenuTarget = menuTarget;
    }

    public IEnumerable<ExpressionWithCondition> AllExpressionWithConditions => Patterns.SelectMany(p => p.AllExpressionWithConditions);
}

internal record PatternData
{
    public IReadOnlyList<IPatternElement> OrderedItems { get; private set; }
    internal const string Peset_Index_Parameter = "FaceTune_PresetIndex";
    public bool IsEmpty => OrderedItems.Count == 0;

    public PatternData(IReadOnlyList<IPatternElement> orderedItems)
    {
        OrderedItems = orderedItems;
    }

    public static PatternData Collect(SessionContext context)
    {
        var orderedItems = new List<IPatternElement>();
        var processedGameObjects = new HashSet<GameObject>();

        var allComponents = context.Root.GetComponentsInChildren<Component>(true);

        var presetIndex = 0;    
        foreach (var component in allComponents)
        {
            if (component == null) continue;
            if (processedGameObjects.Contains(component.gameObject)) continue;

            if (component is PresetComponent presetComponent)
            {
                var presetCondition = new ParameterCondition(Peset_Index_Parameter, IntComparisonType.Equal, presetIndex++);
                var preset = presetComponent.GetPreset(context.DEC.GetPresetDefaultExpression(presetComponent), presetCondition);
                if (preset == null) continue;
                preset.SetMenuTarget(presetComponent.GetMenuTarget());
                orderedItems.Add(preset);
                processedGameObjects.Add(presetComponent.gameObject);
                var childPatterns = presetComponent.gameObject.GetComponentsInChildren<PatternComponent>(true);
                foreach (var pattern in childPatterns)
                {
                    processedGameObjects.Add(pattern.gameObject);
                }
            }
            else if (component is PatternComponent patternComponent)
            {
                var pattern = patternComponent.GetPattern(context.DEC.GetGlobalDefaultExpression());
                if (pattern == null) continue;
                orderedItems.Add(new SingleExpressionPattern(patternComponent.gameObject.name, pattern));
                processedGameObjects.Add(patternComponent.gameObject);
                var nestedPatterns = patternComponent.gameObject.GetComponentsInChildren<PatternComponent>(true);
                foreach (var nestedPattern in nestedPatterns)
                {
                    processedGameObjects.Add(nestedPattern.gameObject);
                }
            }
        }

        return new PatternData(orderedItems);
    }

    public IEnumerable<Preset> GetAllPresets()
    {
        return OrderedItems.OfType<Preset>();
    }

    public IEnumerable<SingleExpressionPattern> GetAllSingleExpressionPatterns()
    {
        return OrderedItems.OfType<SingleExpressionPattern>();
    }

    public IEnumerable<Expression> GetAllExpressions()
    {
        var expressions = new List<Expression>();
        foreach (var orderedItem in OrderedItems)
        {
            foreach (var expressionWithCondition in orderedItem.AllExpressionWithConditions)
            {
                expressions.AddRange(expressionWithCondition.Expressions);
            }
        }
        return expressions;
    }

    public IEnumerable<Condition> GetAllConditions()
    {
        var conditions = new List<Condition>();
        foreach (var orderedItem in OrderedItems)
        {
            foreach (var expressionWithCondition in orderedItem.AllExpressionWithConditions)
            {
                conditions.AddRange(expressionWithCondition.Conditions);
            }
        }
        return conditions;
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

    // Presetが一つしかない場合Presetは意味を為さないので、その中の各PatternをSingleExpressionPatternに変換する
    public void ConvertSinglePresetToSingleExpressionPattern()
    {
        var presets = OrderedItems.OfType<Preset>().ToList();
        if (presets.Count == 1)
        {
            var singlePreset = presets.First();
            var newOrderedItems = new List<IPatternElement>();

            foreach (var item in OrderedItems)
            {
                if (ReferenceEquals(item, singlePreset))
                {
                    for (int i = 0; i < singlePreset.Patterns.Count; i++)
                    {
                        var expressionPattern = singlePreset.Patterns[i];
                        string newName = singlePreset.Name;
                        if (singlePreset.Patterns.Count > 1)
                        {
                            newName = $"{singlePreset.Name}_{i}";
                        }
                        var newSingleExpressionPattern = new SingleExpressionPattern(newName, expressionPattern);
                        newOrderedItems.Add(newSingleExpressionPattern);
                    }
                }
                else
                {
                    newOrderedItems.Add(item);
                }
            }
            OrderedItems = newOrderedItems.AsReadOnly();
        }
    }
}