namespace com.aoyon.facetune;

[Serializable]
public record GenericAnimation // Immutable
{
    [SerializeField] private SerializableCurveBinding _curveBinding;
    public SerializableCurveBinding CurveBinding { get => _curveBinding; init => _curveBinding = value; }

    [SerializeField] private AnimationCurve _curve;
    public AnimationCurve GetCurve() => _curve.Clone();

    public GenericAnimation()
    {
        _curveBinding = new SerializableCurveBinding();
        _curve = new AnimationCurve();
    }

    public GenericAnimation(SerializableCurveBinding curveBinding, AnimationCurve curve)
    {
        _curveBinding = curveBinding with {};
        _curve = curve.Clone();
    }

#if UNITY_EDITOR
    public static List<GenericAnimation> FromAnimationClip(AnimationClip clip)
    {
        var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
        var animations = new List<GenericAnimation>();
        foreach (var binding in bindings)
        {
            var curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding);
            if (curve != null)
            animations.Add(new GenericAnimation(SerializableCurveBinding.FromEditorCurveBinding(binding), curve));
        }
        return animations;
    }
#endif
}

[Serializable]
public record SerializableCurveBinding // Immutable
{
    [SerializeField] private string _path;
    public string Path { get => _path; init => _path = value; }

    [SerializeField] private Type _type;
    public Type Type { get => _type; init => _type = value; }

    [SerializeField] private string _propertyName;
    public string PropertyName { get => _propertyName; init => _propertyName = value; }

    [SerializeField] private bool _isPPtrCurve;
    public bool IsPPtrCurve { get => _isPPtrCurve; init => _isPPtrCurve = value; }
    
    [SerializeField] private bool _isDiscreteCurve;
    public bool IsDiscreteCurve { get => _isDiscreteCurve; init => _isDiscreteCurve = value; }

    public SerializableCurveBinding()
    {
        _path = "";
        _type = typeof(Transform);
        _propertyName = "";
        _isPPtrCurve = false;
    }

    public SerializableCurveBinding(string path, Type type, string propertyName, bool isPPtrCurve, bool isDiscreteCurve)
    {
        _path = path;
        _type = type;
        _propertyName = propertyName;
        _isPPtrCurve = isPPtrCurve;
        _isDiscreteCurve = isDiscreteCurve;
    }
    
    public static SerializableCurveBinding FloatCurve(string path, Type type, string propertyName)
    {
        return new SerializableCurveBinding(path, type, propertyName, false, false);
    }

    public static SerializableCurveBinding PPtrCurve(string path, Type type, string propertyName)
    {
        return new SerializableCurveBinding(path, type, propertyName, true, true);
    }

    public static SerializableCurveBinding DiscreteCurve(string path, Type type, string propertyName)
    {
        return new SerializableCurveBinding(path, type, propertyName, false, true);
    }
    
#if UNITY_EDITOR
    public static SerializableCurveBinding FromEditorCurveBinding(UnityEditor.EditorCurveBinding binding)
    {
        return new SerializableCurveBinding(
            binding.path, 
            binding.type, 
            binding.propertyName, 
            binding.isPPtrCurve, 
            binding.isDiscreteCurve
        );
    }

    public UnityEditor.EditorCurveBinding ToEditorCurveBinding()
    {   
        if (IsDiscreteCurve)
        {
            return UnityEditor.EditorCurveBinding.DiscreteCurve(Path, Type, PropertyName);
        }
        else if (IsPPtrCurve)
        {
            return UnityEditor.EditorCurveBinding.PPtrCurve(Path, Type, PropertyName);
        }
        else
        {
            return UnityEditor.EditorCurveBinding.FloatCurve(Path, Type, PropertyName);
        }
    }
#endif
}

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

    [SerializeField] private AnimationCurve _curve;
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

    internal GenericAnimation GetGeneric(string path)
    {
        var binding = SerializableCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), "blendShape." + Name);
        return new GenericAnimation(binding, _curve);
    }
}