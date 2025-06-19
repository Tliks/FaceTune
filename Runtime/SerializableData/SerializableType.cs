namespace com.aoyon.facetune;

[Serializable]
public record struct SerializableType // Immutable
{
    [SerializeField] private string _name;
    public const string NamePropName = "_name";
    public string Name { readonly get => _name; init => _name = value; }

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

    private Type? _targetType;
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

    public readonly bool Equals(SerializableType other)
    {
        return Name == other.Name;
    }
    
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(Name);
    }
}