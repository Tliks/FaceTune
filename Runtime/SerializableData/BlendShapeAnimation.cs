namespace com.aoyon.facetune;

// 明示的な適用対象(Binding)を持たずnameのみで適用対象を決定する
// ブレンドシェイプを汎用的に取り扱えるようにするため。似たブレンドシェイプを持つ異なる対象への適用や、キメラ対応などが楽になる。
// Todo: 明示的な適用対象を持っていて後で書き換えるようにしてもいいかも？
// <= 本来不要なプロパティをセーブデータとして公開することになる。
// <= 仮の値の場合、そのフラグが必要になる。
[Serializable]
public record BlendShapeAnimation // Immutable
{
    [SerializeField] private string _name;
    public string Name { get => _name; init => _name = value; }

    [SerializeField] private AnimationCurve _curve; // 可変
    public AnimationCurve GetCurve() => _curve.Clone();

    public BlendShapeAnimation()
    {
        _name = "";
        _curve = new AnimationCurve();
    }

    public BlendShapeAnimation(string name, AnimationCurve other)
    {
        _name = name;
        _curve = other.Clone();
    }

    internal static BlendShapeAnimation SingleFrame(string name, float weight)
    {
        var curve = new AnimationCurve();
        curve.AddKey(0, weight);
        return new BlendShapeAnimation(name, curve);
    }

    internal BlendShapeAnimation ToSingleFrame()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0, _curve.Evaluate(0));
        return new BlendShapeAnimation(Name, curve);
    }

    internal GenericAnimation ToGeneric(string path)
    {
        var binding = SerializableCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), "blendShape." + Name);
        return new GenericAnimation(binding, _curve.Clone());
    }

    public virtual bool Equals(BlendShapeAnimation other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _name.Equals(other._name)
            && _curve.Equals(other._curve);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_name, _curve);
    }
}