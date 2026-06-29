namespace Aoyon.FaceTune;

// 明示的な適用対象(Binding)を持たずnameのみで適用対象を決定する
// ブレンドシェイプを汎用的に取り扱えるようにするため。似たブレンドシェイプを持つ異なる対象への適用や、キメラ対応などが楽になる。
[Serializable]
internal record BlendShapeWeightAnimation // Immutable
{
    [SerializeField] private string name;
    public string Name { get => name; init => name = value; }
    public const string NamePropName = nameof(name);

    [SerializeField] private AnimationCurve curve; // 可変
    public AnimationCurve Curve { get => CloneCurve(curve); init => curve = CloneCurve(value); }
    public const string CurvePropName = nameof(curve);

    public BlendShapeWeightAnimation()
    {
        name = "";
        curve = new AnimationCurve();
    }

    public BlendShapeWeightAnimation(string name, AnimationCurve other)
    {
        this.name = name;
        curve = CloneCurve(other);
    }

    private static AnimationCurve CloneCurve(AnimationCurve source)
    {
        return new AnimationCurve(source.keys)
        {
            preWrapMode = source.preWrapMode,
            postWrapMode = source.postWrapMode
        };
    }

    internal float Time => curve.keys.Max(k => k.time);
    internal bool IsZero => curve.keys.All(k => k.value == 0);
    internal float Weight(float time) => curve.Evaluate(time);
    internal bool IsMultiFrame => curve.keys.Length > 1;

    internal static BlendShapeWeightAnimation SingleFrame(string name, float weight)
    {
        var curve = new AnimationCurve();
        curve.AddKey(0, weight);
        return new BlendShapeWeightAnimation(name, curve);
    }

    internal BlendShapeWeight ToFirstFrameBlendShape()
    {
        return new BlendShapeWeight(Name, curve.Evaluate(0));
    }

    public virtual bool Equals(BlendShapeWeightAnimation other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return name.Equals(other.name)
            && curve.Equals(other.curve);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(name, curve);
    }
}