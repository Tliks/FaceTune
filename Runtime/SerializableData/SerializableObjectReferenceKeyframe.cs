namespace com.aoyon.facetune;

[Serializable]
public record struct SerializableObjectReferenceKeyframe // Immutable
{
    [SerializeField] private float _time;
    public float Time { get => _time; init => _time = value; }

    [SerializeField] private Object? _value;
    public Object? Value { get => _value; init => _value = value; }

    public SerializableObjectReferenceKeyframe(float time, Object value)
    {
        _time = time;
        _value = value;
    }


#if UNITY_EDITOR
    public static SerializableObjectReferenceKeyframe FromEditorObjectReferenceKeyframe(UnityEditor.ObjectReferenceKeyframe keyframe)
    {
        return new SerializableObjectReferenceKeyframe(keyframe.time, keyframe.value);
    }

    public UnityEditor.ObjectReferenceKeyframe ToEditorObjectReferenceKeyframe()
    {
        return new UnityEditor.ObjectReferenceKeyframe()
        {
            time = Time,
            value = Value
        };
    }
#endif

    public bool Equals(SerializableObjectReferenceKeyframe other)
    {
        return Mathf.Approximately(_time, other._time) && _value == other._value;
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(_time, _value);
    }
}