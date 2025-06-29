namespace aoyon.facetune;

[Serializable]
public record struct SerializableObjectReferenceKeyframe // Immutable
{
    [SerializeField] private float time;
    public float Time { readonly get => time; init => time = value; }
    public const string TimePropName = nameof(time);

    [SerializeField] private Object? value;
    // 可変ではあるけどクローンする訳にもいかないので無視
    // そもそもここで取得したObject(eg. Material)に対する編集は破壊的
    public Object? Value { readonly get => value; init => this.value = value; } 
    public const string ValuePropName = nameof(value);

    public SerializableObjectReferenceKeyframe(float time, Object value)
    {
        this.time = time;
        this.value = value;
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

    public readonly bool Equals(SerializableObjectReferenceKeyframe other)
    {
        return time.Equals(other.time) && value == other.value;
    }
    
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(time, value);
    }
}