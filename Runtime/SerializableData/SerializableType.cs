namespace com.aoyon.facetune;

[Serializable]
public struct SerializableType // Immutable
{
    [SerializeField] private string _name;
    public string Name { get => _name; init => _name = value; }

    public SerializableType()
    {
        _name = "";
        _targetType = null;
    }

    public SerializableType(string name)
    {
        _name = name;
        _targetType = null;
    }

    public SerializableType(Type type)
    {
        _name = type.AssemblyQualifiedName;
        _targetType = null;
    }

    [NonSerialized] private Type? _targetType;
    public Type? TargetType
    {
        get
        {
            if (_targetType != null)
            {
                return _targetType;
            }

            if (!string.IsNullOrEmpty(_name))
            {
                _targetType = Type.GetType(_name);
            }

            return _targetType;
        }
    }
}