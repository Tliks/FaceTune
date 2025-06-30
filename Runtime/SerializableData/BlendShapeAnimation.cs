namespace aoyon.facetune;

// 明示的な適用対象(Binding)を持たずnameのみで適用対象を決定する
// ブレンドシェイプを汎用的に取り扱えるようにするため。似たブレンドシェイプを持つ異なる対象への適用や、キメラ対応などが楽になる。
// Todo: 明示的な適用対象を持っていて後で書き換えるようにしてもいいかも？
// <= 本来不要なプロパティをセーブデータとして公開することになる。
// <= 仮の値の場合、そのフラグが必要になる。
[Serializable]
public record BlendShapeAnimation // Immutable
{
    [SerializeField] private string name;
    public string Name { get => name; init => name = value; }
    public const string NamePropName = nameof(name);

    [SerializeField] private AnimationCurve curve; // 可変
    public AnimationCurve Curve { get => curve.Clone(); init => curve = value.Clone(); }
    public const string CurvePropName = nameof(curve);

    public BlendShapeAnimation()
    {
        name = "";
        curve = new AnimationCurve();
    }

    public BlendShapeAnimation(string name, AnimationCurve other)
    {
        this.name = name;
        curve = other.Clone();
    }

    internal float Time => curve.keys.Max(k => k.time);

    internal static BlendShapeAnimation SingleFrame(string name, float weight)
    {
        var curve = new AnimationCurve();
        curve.AddKey(0, weight);
        return new BlendShapeAnimation(name, curve);
    }

    internal BlendShapeAnimation ToSingleFrame()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0, this.curve.Evaluate(0));
        return new BlendShapeAnimation(Name, curve);
    }

    internal GenericAnimation ToGeneric(string path)
    {
        var binding = SerializableCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), FaceTuneConsts.AnimatedBlendShapePrefix + Name);
        return new GenericAnimation(binding, curve);
    }

    internal BlendShape ToFirstFrameBlendShape()
    {
        return new BlendShape(Name, curve.Evaluate(0));
    }
    public virtual bool Equals(BlendShapeAnimation other)
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