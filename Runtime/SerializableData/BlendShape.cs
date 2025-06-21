namespace com.aoyon.facetune;

[Serializable]
public record struct BlendShape
{
    [SerializeField] private string _name;
    public const string NamePropName = "_name";
    public string Name { readonly get => _name; init => _name = value; }

    [SerializeField] private float _weight;
    public const string WeightPropName = "_weight";
    public float Weight { readonly get => _weight; init => _weight = value; }

    public BlendShape()
    {
        _name = "";
        _weight = 0.0f;
    }

    public BlendShape(string name, float weight)
    {
        _name = name;
        _weight = weight;
    }    

    public readonly bool Equals(BlendShape other)
    {
        return _name.Equals(other._name)
            && _weight.Equals(other._weight);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(_name, _weight);
    }
}