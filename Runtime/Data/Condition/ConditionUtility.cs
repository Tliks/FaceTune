namespace Aoyon.FaceTune;

internal static class ConditionUtility
{

    // Todo: Refactorと常にtrue/falseとなる際のハンドリング
    // 条件の簡略化の処理も欲しい
    public static List<List<ICondition>> AddNegation(ICollection<ICondition> conditions, ICollection<ICondition> conditionsToNegate)
    {
        // A: conditions, B: ConditionsToNegate
        // A∧¬B
        // 結果は (Aの全条件) AND NOT (Bの全条件)。
        // ド・モルガンの法則よりNOT (B1 AND B2 AND ...) = (NOT B1) OR (NOT B2) OR ...
        // したがって、Aの全条件 + 否定したBの各条件を1つずつ加えたリストをORでまとめて返す。

        var result = new List<List<ICondition>>();

        foreach (var negateCondition in conditionsToNegate)
        {
            var andList = new List<ICondition>(conditions)
            {
                negateCondition.ToNegation()
            };
            result.Add(andList);
        }

        return result;
    }   

    // Todo: 上に同じく
    public static List<List<ICondition>> AddNegation(ICollection<ICondition> conditions, List<List<ICondition>> conditionsToNegate)
    {
        // A ∧ ¬(B1 ∨ B2 ∨ ...)
        // = A ∧ (¬B1 ∧ ¬B2 ∧ ...)
        // ただし各 Bi は AND のまとまり（List<Condition>）。
        // ¬(B1 AND B2 AND ...) = (¬B1) OR (¬B2) OR ... を利用して、
        // DNF としては A と各 Bi の否定からの直積展開になる。

        // 初期は「A の AND 条件」の単一節
        var clauses = new List<List<ICondition>> { new List<ICondition>(conditions) };

        foreach (var group in conditionsToNegate)
        {
            var expanded = new List<List<ICondition>>();
            foreach (var clause in clauses)
            {
                foreach (var cond in group)
                {
                    var newClause = new List<ICondition>(clause) { cond.ToNegation() };
                    expanded.Add(newClause);
                }
            }
            clauses = expanded;
        }

        return clauses;
    }
}