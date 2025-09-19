namespace Aoyon.FaceTune;

internal static class ConditionOptimizer
{
    public static NormalizedCondition Optimize(NormalizedCondition dnf)
    {
        // 無限ループを防ぐための制限を追加
        const int MaxClauses = 10000; // 適切な制限値を設定
        if (dnf.Clauses.Count > MaxClauses)
        {
            throw new InvalidOperationException($"処理可能な条件数を超えています。制限値: {MaxClauses}, 実際: {dnf.Clauses.Count}");
        }

        // Step 1: 各AndClauseの内部を単純化し、矛盾するものを除去する
        var simplifiedClauses = new HashSet<AndClause>(dnf.Clauses.Count);
        foreach (var clause in dnf.Clauses)
        {
            var simplified = SimplifyClause(clause);
            if (simplified != null) // 矛盾する節 (null) を除去
            {
                simplifiedClauses.Add(simplified);
            }
        }

        // Step 2: 連続するNotEqual条件をOR節に変換する最適化
        var expandedClauses = ExpandConsecutiveNotEqualClauses(simplifiedClauses);

        // Step 3: 節同士を比較し、包含されるものを除去する（吸収の法則）
        var absorbedClauses = AbsorbClauses(expandedClauses);

        return new NormalizedCondition(absorbedClauses);
    }

    /// <summary>
    /// 連続するNotEqual条件を持つAND節をOR節に展開します。
    /// 例: (GestureRight != 1 AND GestureRight != 2 AND ... AND GestureRight != 7)
    /// -> (GestureRight <= 0) OR (GestureRight >= 8)
    /// </summary>
    private static HashSet<AndClause> ExpandConsecutiveNotEqualClauses(HashSet<AndClause> clauses)
    {
        if (clauses.Count == 0) return clauses;
        
        var result = new HashSet<AndClause>(clauses.Count * 2); // 容量を事前確保
        
        foreach (var clause in clauses)
        {
            var expandedClauses = TryExpandConsecutiveNotEquals(clause);
            result.UnionWith(expandedClauses); // AddRangeの代わりにUnionWithを使用
            
            // 結果が制限を超えた場合の安全策
            const int MaxExpandedClauses = 50000;
            if (result.Count > MaxExpandedClauses)
            {
                throw new InvalidOperationException($"展開後の条件数が制限を超えました。制限値: {MaxExpandedClauses}");
            }
        }
        
        return result;
    }

    /// <summary>
    /// 単一のAND節で連続するNotEqual条件をOR節に展開を試みます。
    /// </summary>
    private static IEnumerable<AndClause> TryExpandConsecutiveNotEquals(AndClause clause)
    {
        // パラメータごとに条件をグループ化
        var groupedConditions = clause.Conditions
            .GroupBy(c => c switch
            {
                IntCondition ic => ic.ParameterName,
                FloatCondition fc => fc.ParameterName,
                BoolCondition bc => bc.ParameterName,
                _ => string.Empty // GUIDの代わりに空文字列を使用（グループ化されない）
            });

        foreach (var group in groupedConditions)
        {
            if (group.First() is IntCondition)
            {
                var intConditions = group.Cast<IntCondition>().ToList();
                var consecutiveNotEquals = FindConsecutiveNotEquals(intConditions);
                
                if (consecutiveNotEquals != null && consecutiveNotEquals.Count >= 3) // 3個以上で最適化対象
                {
                    // 連続するNotEqual条件を範囲条件に変換
                    var otherConditions = clause.Conditions.Except(consecutiveNotEquals.Cast<IBaseCondition>()).ToList();
                    var rangeConditions = ConvertConsecutiveNotEqualsToRanges(consecutiveNotEquals, group.Key);
                    
                    // 各範囲条件に対して新しいAND節を作成
                    foreach (var rangeCondition in rangeConditions)
                    {
                        var newConditions = new List<IBaseCondition>(otherConditions) { rangeCondition };
                        yield return new AndClause(newConditions);
                    }
                    yield break; // この節は展開されたので、元の節は返さない
                }
            }
        }
        
        // 最適化対象がなければ元の節をそのまま返す
        yield return clause;
    }

    /// <summary>
    /// IntConditionのリストから連続するNotEqual条件を見つけます。
    /// </summary>
    private static List<IntCondition>? FindConsecutiveNotEquals(List<IntCondition> conditions)
    {
        var notEqualConditions = conditions.Where(c => c.ComparisonType == ComparisonType.NotEqual).ToList();
        if (notEqualConditions.Count < 3) return null; // 3個未満は対象外
        
        var sortedValues = notEqualConditions.Select(c => c.Value).OrderBy(x => x).ToList();
        
        // 連続性をチェック
        for (int i = 1; i < sortedValues.Count; i++)
        {
            if (sortedValues[i] != sortedValues[i - 1] + 1)
            {
                return null; // 連続していない
            }
        }
        
        return notEqualConditions;
    }

    /// <summary>
    /// 連続するNotEqual条件を範囲条件に変換します。
    /// </summary>
    private static IEnumerable<IBaseCondition> ConvertConsecutiveNotEqualsToRanges(List<IntCondition> consecutiveNotEquals, string parameterName)
    {
        var values = consecutiveNotEquals.Select(c => c.Value).OrderBy(x => x).ToList();
        var min = values.First();
        var max = values.Last();
        
        // != [min..max] -> <= (min-1) OR >= (max+1)
        if (min > int.MinValue)
        {
            yield return new IntCondition(parameterName, min - 1, ComparisonType.LessThan);
        }
        if (max < int.MaxValue)
        {
            yield return new IntCondition(parameterName, max + 1, ComparisonType.GreaterThan);
        }
    }

    /// <summary>
    /// 単一のAndClauseの内部を単純化します。
    /// </summary>
    private static AndClause SimplifyClause(AndClause clause)
    {
        var finalConditions = new List<IBaseCondition>();

        // 1. 条件をパラメータ名でグループ化する
        var groupedConditions = clause.Conditions
            .Where(c => c is not TrueCondition) // trueは最初から無視
            .GroupBy(c => c switch
            {
                IntCondition ic => ic.ParameterName,
                FloatCondition fc => fc.ParameterName,
                BoolCondition bc => bc.ParameterName,
                _ => string.Empty // グループ化されないその他（FalseConditionなど）
            });

        foreach (var group in groupedConditions)
        {
            var conditionsInGroup = group.ToList();
            IEnumerable<IBaseCondition> optimizedGroup;

            // 2. グループごとに特化した最適化を実行する
            if (conditionsInGroup.First() is IntCondition)
            {
                optimizedGroup = OptimizeIntGroup(conditionsInGroup.Cast<IntCondition>());
            }
            else if (conditionsInGroup.First() is BoolCondition)
            {
                optimizedGroup = OptimizeBoolGroup(conditionsInGroup.Cast<BoolCondition>());
            }
            else if (conditionsInGroup.First() is FloatCondition)
            {
                optimizedGroup = OptimizeFloatGroup(conditionsInGroup.Cast<FloatCondition>());
            }
            else
            {
                // 最適化ロジックがなければ、重複だけ削除する
                optimizedGroup = conditionsInGroup.ToHashSet();
            }

            // 3. 最適化結果を評価する
            if (optimizedGroup.Any(c => c is FalseCondition))
            {
                return null!; // グループが矛盾(false)する場合、節全体がfalseになる
            }
            finalConditions.AddRange(optimizedGroup);
        }
        
        // 4. 節全体の矛盾を最終チェック（異なるパラメータ間での矛盾など）
        if (finalConditions.OfType<FalseCondition>().Any()) return null!;
        
        // 効率的な否定条件チェック（ToNegation()の頻繁な呼び出しを避ける）
        var conditionSet = finalConditions.ToHashSet();
        foreach (var condition in finalConditions)
        {
            if (conditionSet.Contains(condition.ToNegation()))
            {
                return null!; // 矛盾を発見
            }
        }

        return new AndClause(finalConditions);
    }

    /// <summary>
    /// 同じパラメータ名を持つIntConditionのリストを最適化します。
    /// </summary>
    private static IEnumerable<IBaseCondition> OptimizeIntGroup(IEnumerable<IntCondition> conditions)
    {
        // パラメータが取りうる値の範囲を定義
        long min = long.MinValue; // "> value" は min を value + 1 に更新
        long max = long.MaxValue; // "< value" は max を value - 1 に更新
        var notEquals = new HashSet<int>();
        int? equals = null;

        foreach (var cond in conditions)
        {
            switch (cond.ComparisonType)
            {
                case ComparisonType.Equal:
                    // 既に別の等式条件があれば、値が違う時点で矛盾
                    if (equals.HasValue && equals.Value != cond.Value) return new[] { FalseCondition.Instance };
                    equals = cond.Value;
                    break;
                case ComparisonType.NotEqual:
                    notEquals.Add(cond.Value);
                    break;
                case ComparisonType.GreaterThan:
                    min = Math.Max(min, (long)cond.Value + 1);
                    break;
                case ComparisonType.LessThan:
                    max = Math.Min(max, (long)cond.Value - 1);
                    break;
            }
        }
        
        // 等式(==)制約で他の条件を評価
        if (equals.HasValue)
        {
            // (a == 5) and (a > 10) -> false
            if (equals.Value < min || equals.Value > max || notEquals.Contains(equals.Value))
            {
                return new[] { FalseCondition.Instance };
            }
            // 全ての条件がこの等式で満たされるため、これだけが残る
            return new[] { new IntCondition(conditions.First().ParameterName, equals.Value, ComparisonType.Equal) };
        }

        // 範囲の矛盾をチェック (e.g., a > 10 and a < 5)
        if (min > max)
        {
            return new[] { FalseCondition.Instance };
        }
        
        // 範囲から最小限の条件セットを再構築
        var result = new List<IBaseCondition>();
        var paramName = conditions.First().ParameterName;

        if (min != long.MinValue) result.Add(new IntCondition(paramName, (int)(min - 1), ComparisonType.GreaterThan));
        if (max != long.MaxValue) result.Add(new IntCondition(paramName, (int)(max + 1), ComparisonType.LessThan));
        
        // NotEqual条件を最適化: 連続する値の範囲を見つけて範囲条件に変換
        var optimizedNotEquals = OptimizeConsecutiveNotEquals(notEquals, min, max);
        foreach (var condition in optimizedNotEquals)
        {
            if (condition is IntCondition intCond)
            {
                result.Add(new IntCondition(paramName, intCond.Value, intCond.ComparisonType));
            }
        }
        
        return result;
    }

    /// <summary>
    /// NotEqual条件を最適化します。
    /// 現在の実装では、範囲内のNotEqual条件のみを保持します。
    /// 将来的には、連続する値の否定をより効率的に表現する方法を検討できます。
    /// </summary>
    private static IEnumerable<IBaseCondition> OptimizeConsecutiveNotEquals(HashSet<int> notEquals, long min, long max)
    {
        if (!notEquals.Any()) return Enumerable.Empty<IBaseCondition>();
        
        // 範囲内のNotEqual条件のみを保持
        var validNotEquals = notEquals.Where(ne => ne >= min && ne <= max);
        return validNotEquals.Select(ne => new IntCondition(string.Empty, ne, ComparisonType.NotEqual));
    }

    /// <summary>
    /// 同じパラメータ名を持つFloatConditionのリストを最適化します。
    /// </summary>
    private static IEnumerable<IBaseCondition> OptimizeFloatGroup(IEnumerable<FloatCondition> conditions)
    {
        // パラメータが取りうる値の範囲を定義
        double min = double.NegativeInfinity; // "> value" は min を value に更新（浮動小数点では厳密な境界）
        double max = double.PositiveInfinity; // "< value" は max を value に更新
        var notEquals = new HashSet<float>();
        float? equals = null;

        foreach (var cond in conditions)
        {
            switch (cond.ComparisonType)
            {
                case ComparisonType.Equal:
                    // 既に別の等式条件があれば、値が違う時点で矛盾
                    if (equals.HasValue && !Mathf.Approximately(equals.Value, cond.Value))
                        return new[] { FalseCondition.Instance };
                    equals = cond.Value;
                    break;
                case ComparisonType.NotEqual:
                    notEquals.Add(cond.Value);
                    break;
                case ComparisonType.GreaterThan:
                    min = Math.Max(min, cond.Value);
                    break;
                case ComparisonType.LessThan:
                    max = Math.Min(max, cond.Value);
                    break;
            }
        }
        
        // 等式(==)制約で他の条件を評価
        if (equals.HasValue)
        {
            // (a == 5.0) and (a > 10.0) -> false
            if (equals.Value <= min || equals.Value >= max || notEquals.Any(ne => Mathf.Approximately(equals.Value, ne)))
            {
                return new[] { FalseCondition.Instance };
            }
            // 全ての条件がこの等式で満たされるため、これだけが残る
            return new[] { new FloatCondition(conditions.First().ParameterName, equals.Value, ComparisonType.Equal) };
        }

        // 範囲の矛盾をチェック (e.g., a > 10.0 and a < 5.0)
        if (min >= max)
        {
            return new[] { FalseCondition.Instance };
        }
        
        // 範囲から最小限の条件セットを再構築
        var result = new List<IBaseCondition>();
        var paramName = conditions.First().ParameterName;

        if (!double.IsNegativeInfinity(min)) result.Add(new FloatCondition(paramName, (float)min, ComparisonType.GreaterThan));
        if (!double.IsPositiveInfinity(max)) result.Add(new FloatCondition(paramName, (float)max, ComparisonType.LessThan));
        
        foreach (var ne in notEquals)
        {
            // 範囲に影響しないNotEqualは冗長なので追加しない (e.g., a > 10.0 and a != 5.0)
            if (ne > min && ne < max)
            {
                result.Add(new FloatCondition(paramName, ne, ComparisonType.NotEqual));
            }
        }
        return result;
    }

    /// <summary>
    /// 同じパラメータ名を持つBoolConditionのリストを最適化します。
    /// </summary>
    private static IEnumerable<IBaseCondition> OptimizeBoolGroup(IEnumerable<BoolCondition> conditions)
    {
        // trueを要求する条件とfalseを要求する条件があるかチェック
        bool? requiredValue = null;
        foreach (var cond in conditions)
        {
            if (requiredValue.HasValue && requiredValue.Value != cond.Value)
            {
                // (Param == true) AND (Param == false) -> 矛盾
                return new[] { FalseCondition.Instance };
            }
            requiredValue = cond.Value;
        }

        // 矛盾がなければ、単一の条件にまとめられる
        return new[] { new BoolCondition(conditions.First().ParameterName, requiredValue!.Value) };
    }    
    
    /// <summary>
    /// 節のリストに吸収の法則を適用し、冗長な節を除去します。
    /// O(n log n) の効率的な実装。
    /// </summary>
    private static List<AndClause> AbsorbClauses(HashSet<AndClause> clauses)
    {
        if (clauses.Count <= 1) return clauses.ToList();

        // 条件数でソート（短い節が長い節を吸収する可能性が高い）
        var sortedClauses = clauses.OrderBy(c => c.Conditions.Count).ToArray();
        var result = new List<AndClause>();
        
        for (int i = 0; i < sortedClauses.Length; i++)
        {
            var currentClause = sortedClauses[i];
            bool isSubsumed = false;
            
            // より短い既存の節による包含チェック（早期終了）
            for (int j = 0; j < result.Count; j++)
            {
                var existingClause = result[j];
                if (existingClause.Conditions.Count > currentClause.Conditions.Count)
                    break; // これ以降の節は全て長いので、包含の可能性なし
                    
                if (IsSubsumedBy(currentClause, existingClause))
                {
                    isSubsumed = true;
                    break;
                }
            }
            
            if (!isSubsumed)
            {
                // 現在の節が既存の節を吸収するかチェック（逆方向から削除）
                for (int j = result.Count - 1; j >= 0; j--)
                {
                    if (IsSubsumedBy(result[j], currentClause))
                    {
                        result.RemoveAt(j);
                    }
                }
                result.Add(currentClause);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// clause1がclause2に包含されるかを効率的にチェックします。
    /// </summary>
    private static bool IsSubsumedBy(AndClause clause1, AndClause clause2)
    {
        if (clause1.Conditions.Count < clause2.Conditions.Count)
            return false;
            
        // HashSetを使用してO(1)の包含チェック
        var clause2Set = clause2.Conditions.ToHashSet();
        return clause1.Conditions.All(c => clause2Set.Contains(c));
    }
}