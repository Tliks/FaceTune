namespace com.aoyon.facetune;

[Serializable]
public record SerializableCurveBinding // Immutable
{
    [SerializeField] private string _path;
    public string Path { get => _path; init => _path = value; }

    [SerializeField] private SerializableType _type;
    public Type? Type { get => _type.TargetType; init => _type = value != null ? new SerializableType(value) : _type; }

    [SerializeField] private string _propertyName;
    public string PropertyName { get => _propertyName; init => _propertyName = value; }

    [SerializeField] private bool _isPPtrCurve;
    public bool IsPPtrCurve { get => _isPPtrCurve; init => _isPPtrCurve = value; }
    
    [SerializeField] private bool _isDiscreteCurve;
    public bool IsDiscreteCurve { get => _isDiscreteCurve; init => _isDiscreteCurve = value; }

    public SerializableCurveBinding()
    {
        _path = "";
        _type = new SerializableType();
        _propertyName = "";
        _isPPtrCurve = false;
    }

    public SerializableCurveBinding(string path, Type type, string propertyName, bool isPPtrCurve, bool isDiscreteCurve)
    {
        _path = path;
        _type = new SerializableType(type);
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