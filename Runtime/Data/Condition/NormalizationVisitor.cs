namespace Aoyon.FaceTune;

internal class NormalizationVisitor : IConditionVisitor, IDisposable
{
    private Stack<List<AndClause>> _stack = new();

    public static NormalizedCondition Normalize(ICondition condition)
    {
        using var visitor = new NormalizationVisitor();
        condition.Accept(visitor);
        return new NormalizedCondition(visitor.TakeResult());
    }

    private List<AndClause> TakeResult()
    {
        if (_stack.Count == 0)
        {
            return new List<AndClause>();
        }
        var resultPooled = _stack.Pop();
        // 残りは返却
        ReleaseAll(_stack);
        // 非プールのListへコピーしてから返却
        var result = new List<AndClause>(resultPooled);
        ListPool<AndClause>.Release(resultPooled);
        return result;
    }

    private static void ReleaseAll(Stack<List<AndClause>> stack)
    {
        while (stack.Count > 0)
        {
            var list = stack.Pop();
            ListPool<AndClause>.Release(list);
        }
    }

    public void Visit(FloatCondition condition)
    {
        var list = ListPool<AndClause>.Get();
        list.Add(new AndClause(condition));
        _stack.Push(list);
    }

    public void Visit(IntCondition condition)
    {
        var list = ListPool<AndClause>.Get();
        list.Add(new AndClause(condition));
        _stack.Push(list);
    }

    public void Visit(BoolCondition condition)
    {
        var list = ListPool<AndClause>.Get();
        list.Add(new AndClause(condition));
        _stack.Push(list);
    }

    public void Visit(TrueCondition condition)
    {
        var list = ListPool<AndClause>.Get();
        list.Add(new AndClause(condition));
        _stack.Push(list);
    }

    public void Visit(FalseCondition condition)
    {
        var list = ListPool<AndClause>.Get();
        list.Add(new AndClause(condition));
        _stack.Push(list);
    }

    public void Visit(OrCondition condition)
    {
        if (condition.Conditions.Count == 0)
        {
            Visit(FalseCondition.Instance);
            return;
        }

        var result = ListPool<AndClause>.Get();

        foreach (var child in condition.Conditions)
        {
            child.Accept(this);
            var childList = _stack.Pop();
            result.AddRange(childList);
            // childList内のAndClauseは引き続き使用するため、Listのみプールに返す
            ListPool<AndClause>.Release(childList);
        }

        _stack.Push(result);
    }

    public void Visit(AndCondition condition)
    {
        if (condition.Conditions.Count == 0)
        {
            Visit(TrueCondition.Instance);
            return;
        }

        var resultClauses = ListPool<AndClause>.Get();
        resultClauses.Add(new AndClause(TrueCondition.Instance));

        foreach (var child in condition.Conditions)
        {
            child.Accept(this);
            var nextChildClauses = _stack.Pop();

            // 組み合わせ爆発を防ぐための制限を追加
            const int MaxClauses = 1000;
            if (resultClauses.Count * nextChildClauses.Count > MaxClauses)
            {
                // childのリストはもう使わないので返却
                ListPool<AndClause>.Release(nextChildClauses);
                ListPool<AndClause>.Release(resultClauses);
                throw new InvalidOperationException($"条件の組み合わせが複雑すぎます。制限値: {MaxClauses}");
            }

            var combined = ListPool<AndClause>.Get();

            foreach (var clause1 in resultClauses)
            {
                foreach (var clause2 in nextChildClauses)
                {
                    var merged = new List<IBaseCondition>(clause1.Conditions.Count + clause2.Conditions.Count);
                    foreach (var c in clause1.Conditions) merged.Add(c);
                    foreach (var c in clause2.Conditions) merged.Add(c);
                    combined.Add(new AndClause(merged));
                }
            }

            // 中間リストを返却
            ListPool<AndClause>.Release(resultClauses);
            ListPool<AndClause>.Release(nextChildClauses);
            resultClauses = combined;
        }

        _stack.Push(resultClauses);
    }

    public void Visit(AndClause condition)
    {
        var list = ListPool<AndClause>.Get();
        list.Add(condition);
        _stack.Push(list);
    }

    public void Visit(NormalizedCondition condition)
    {
        var list = ListPool<AndClause>.Get();
        list.AddRange(condition.Clauses);
        _stack.Push(list);
    }

    public void Dispose()
    {
        ReleaseAll(_stack);
    }
}