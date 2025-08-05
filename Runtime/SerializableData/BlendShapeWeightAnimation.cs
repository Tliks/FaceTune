namespace Aoyon.FaceTune;

// 明示的な適用対象(Binding)を持たずnameのみで適用対象を決定する
// ブレンドシェイプを汎用的に取り扱えるようにするため。似たブレンドシェイプを持つ異なる対象への適用や、キメラ対応などが楽になる。
// Todo: 明示的な適用対象を持っていて後で書き換えるようにしてもいいかも？
// <= 本来不要なプロパティをセーブデータとして公開することになる。
// <= 仮の値の場合、そのフラグが必要になる。
[Serializable]
public record BlendShapeWeightAnimation // Immutable
{
    [SerializeField] private string name;
    public string Name { get => name; init => name = value; }
    public const string NamePropName = nameof(name);

    [SerializeField] private AnimationCurve curve; // 可変
    public AnimationCurve Curve { get => curve.Clone(); init => curve = value.Clone(); }
    public const string CurvePropName = nameof(curve);

    public BlendShapeWeightAnimation()
    {
        name = "";
        curve = new AnimationCurve();
    }

    public BlendShapeWeightAnimation(string name, AnimationCurve other)
    {
        this.name = name;
        curve = other.Clone();
    }

    internal float Time => curve.keys.Max(k => k.time);

    internal static BlendShapeWeightAnimation SingleFrame(string name, float weight)
    {
        var curve = new AnimationCurve();
        curve.AddKey(0, weight);
        return new BlendShapeWeightAnimation(name, curve);
    }

    internal BlendShapeWeightAnimation ToFirstFrame()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0, this.curve.Evaluate(0));
        return new BlendShapeWeightAnimation(Name, curve);
    }

    internal BlendShapeWeightAnimation ToLastFrame()
    {
        var curve = new AnimationCurve();
        curve.AddKey(Time, this.curve.Evaluate(Time));
        return new BlendShapeWeightAnimation(Name, curve);
    }

    internal GenericAnimation ToGeneric(string path)
    {
        var binding = SerializableCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), FaceTuneConstants.AnimatedBlendShapePrefix + Name);
        return new GenericAnimation(binding, curve);
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