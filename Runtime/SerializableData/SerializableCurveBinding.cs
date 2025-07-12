namespace aoyon.facetune;

[Serializable]
public record SerializableCurveBinding // Immutable
{
    [SerializeField] private string path;
    public string Path { get => path; init => path = value; }
    public const string PathPropName = nameof(path);

    [SerializeField] private SerializableType type;
    public Type? Type { get => type.TargetType; init => type = value != null ? new SerializableType(value) : type; }
    public const string TypePropName = nameof(type);

    [SerializeField] private string propertyName;
    public string PropertyName { get => propertyName; init => propertyName = value; }
    public const string PropertyNamePropName = nameof(propertyName);

    [SerializeField] private bool isPPtrCurve;
    public bool IsPPtrCurve { get => isPPtrCurve; init => isPPtrCurve = value; }
    public const string IsPPtrCurvePropName = nameof(isPPtrCurve);

    [SerializeField] private bool isDiscreteCurve;
    public bool IsDiscreteCurve { get => isDiscreteCurve; init => isDiscreteCurve = value; }
    public const string IsDiscreteCurvePropName = nameof(isDiscreteCurve);

    public SerializableCurveBinding()
    {
        path = "";
        type = new SerializableType();
        propertyName = "";
        isPPtrCurve = false;
    }

    public SerializableCurveBinding(string path, Type type, string propertyName, bool isPPtrCurve, bool isDiscreteCurve)
    {
        this.path = path;
        this.type = new SerializableType(type);
        this.propertyName = propertyName;
        this.isPPtrCurve = isPPtrCurve;
        this.isDiscreteCurve = isDiscreteCurve;
    }
    
    internal static SerializableCurveBinding FloatCurve(string path, Type type, string propertyName)
    {
        return new SerializableCurveBinding(path, type, propertyName, false, false);
    }

    internal static SerializableCurveBinding PPtrCurve(string path, Type type, string propertyName)
    {
        return new SerializableCurveBinding(path, type, propertyName, true, true);
    }

    internal static SerializableCurveBinding DiscreteCurve(string path, Type type, string propertyName)
    {
        return new SerializableCurveBinding(path, type, propertyName, false, true);
    }
    
#if UNITY_EDITOR
    internal static SerializableCurveBinding FromEditorCurveBinding(UnityEditor.EditorCurveBinding binding)
    {
        return new SerializableCurveBinding(
            binding.path, 
            binding.type, 
            binding.propertyName, 
            binding.isPPtrCurve, 
            binding.isDiscreteCurve
        );
    }

    internal UnityEditor.EditorCurveBinding ToEditorCurveBinding()
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

    public virtual bool Equals(SerializableCurveBinding other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return path.Equals(other.path)
            && type.Equals(other.type)
            && propertyName.Equals(other.propertyName)
            && isPPtrCurve.Equals(other.isPPtrCurve)
            && isDiscreteCurve.Equals(other.isDiscreteCurve);
    }
    public override int GetHashCode()
    {
        return HashCode.Combine(path, type, propertyName, isPPtrCurve, isDiscreteCurve);
    }
}