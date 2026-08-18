namespace Aoyon.FaceTune;

internal interface IHasObjectReferences
{
    void ResolveReferences();
}

/// <summary>このComponentで有効になっている条件。</summary>
internal interface IHasConditions
{
    IEnumerable<Condition> Conditions { get; }
}

/// <summary>シリアライズ構造から独立した、参照可能な設定の読み取りモデル。</summary>
internal readonly record struct ReferenceableExpressionSettings<TValue>(
    bool Enabled,
    SettingsReferenceMode Mode,
    Transform? Source,
    TValue Direct)
    where TValue : class;

/// <summary>
/// Transformから参照できるExpression設定。
/// Scoped / Unscopedの収集規則は設定種別ごとのResolverが担う。
/// </summary>
internal interface IReferenceableExpressionSettings<TValue>
    where TValue : class
{
    ReferenceableExpressionSettings<TValue> Settings { get; }
}

internal static class ReferenceableExpressionSettingsExtensions
{
    public static ReferenceableExpressionSettings<TValue> GetReferenceableSettings<TValue>(
        this IReferenceableExpressionSettings<TValue> source)
        where TValue : class
        => source.Settings;
}
