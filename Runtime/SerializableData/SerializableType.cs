namespace aoyon.facetune;

[Serializable]
public record struct SerializableType // Immutable
{
    [SerializeField] private string name;
    public string Name { readonly get => name; init => name = value; }
    public const string NamePropName = nameof(name);

    public SerializableType()
    {
        name = "";
        _targetType = null;
    }

    public SerializableType(string name)
    {
        this.name = name;
        _targetType = null;
    }

    public SerializableType(Type type)
    {
        name = type.AssemblyQualifiedName;
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

            if (!string.IsNullOrEmpty(name))
            {
                _targetType = Type.GetType(name);
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