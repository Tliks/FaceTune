namespace Aoyon.FaceTune;

internal interface ICondition // Immutable
{
    ICondition ToNegation();
    void Accept(IConditionVisitor visitor);
}

/// <summary>
/// FloatConditionやTrueConditionなど単一で基本的な条件
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
internal interface IConditionVisitor
{
    void Visit(FloatCondition condition);
    void Visit(IntCondition condition);
    void Visit(BoolCondition condition);
    void Visit(TrueCondition condition);
    void Visit(FalseCondition condition);
    void Visit(AndCondition condition);
    void Visit(OrCondition condition);
    void Visit(AndClause condition);
    void Visit(NormalizedCondition condition);
}
