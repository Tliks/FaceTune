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

/// <summary>値を直接持つか、指定Transform上の同種設定を参照する設定。</summary>
internal interface ISettingsSource<out TValue>
    where TValue : class
{
    SettingsSourceMode SourceMode { get; }
    Transform? Source { get; }
    TValue Direct { get; }
}

/// <summary>Transformから参照できる同種のExpression設定。</summary>
internal interface IReferenceableExpressionSettings<TSource>
    where TSource : class
{
    TSource? SettingsSource { get; }
}
