namespace Aoyon.FaceTune;

internal interface ICondition // Immutable
{
    ICondition ToNegation();
    TResult Accept<TResult>(IConditionVisitor<TResult> visitor);
}

/// <summary>
/// FloatConditionやIntConditionなど単一で基本的な条件
/// </summary>
internal interface IBaseCondition : ICondition
{
}

/// <summary>
/// AndConditionなど、複合条件のマーカー
/// </summary>
internal interface ICompositeCondition : ICondition
{
}

// <summary>
/// Conditionの各具象クラスを訪問するインターフェース
/// </summary>
internal interface IConditionVisitor<TResult>
{
    TResult Visit(FloatCondition condition);
    TResult Visit(IntCondition condition);
    TResult Visit(BoolCondition condition);
    TResult Visit(AndCondition condition);
    TResult Visit(OrCondition condition);
    TResult Visit(TrueCondition condition);
    TResult Visit(FalseCondition condition);
}
