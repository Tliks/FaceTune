namespace Aoyon.FaceTune;

internal class DnfVisitor : IConditionVisitor<IReadOnlyList<AndClause>>
{
    public IReadOnlyList<AndClause> Visit(FloatCondition condition)
    {
        return new[] { new AndClause(condition) };
    }

    public IReadOnlyList<AndClause> Visit(IntCondition condition)
    {
        return new[] { new AndClause(condition) };
    }

    public IReadOnlyList<AndClause> Visit(BoolCondition condition)
    {
        return new[] { new AndClause(condition) };
    }

    public IReadOnlyList<AndClause> Visit(TrueCondition condition)
    {
        return new[] { new AndClause(condition) };
    }

    public IReadOnlyList<AndClause> Visit(FalseCondition condition)
    {
        return new[] { new AndClause(condition) };
    }

    public IReadOnlyList<AndClause> Visit(OrCondition condition)
    {
        if (!condition.Conditions.Any())
        {
            return Visit(new FalseCondition()); // 空のORはfalse
        }

        var allClauses = new List<AndClause>();
        foreach (var child in condition.Conditions)
        {
            allClauses.AddRange(child.Accept(this));
        }
        return allClauses;
    }

    public IReadOnlyList<AndClause> Visit(AndCondition condition)
    {
        // 空のANDはtrue
        if (!condition.Conditions.Any())
        {
            return Visit(new TrueCondition());
        }

        // trueで初期化し、子の結果を掛け合わせていく
        var resultClauses = new List<AndClause>(Visit(new TrueCondition()));

        foreach (var child in condition.Conditions)
        {
            var nextChildClauses = child.Accept(this);
            
            // 子が一つでもfalseなら、AND全体が即座にfalseになる
            if (!nextChildClauses.Any())
            {
                return Visit(new FalseCondition());
            }
            
            // デカルト積を計算して畳み込む
            var combined = new List<AndClause>();
            foreach (var clause1 in resultClauses)
            {
                foreach (var clause2 in nextChildClauses)
                {
                    var merged = new List<IBaseCondition>(clause1.Conditions.Concat(clause2.Conditions));
                    combined.Add(new AndClause(merged));
                }
            }
            resultClauses = combined;
        }
        return resultClauses;
    }
}