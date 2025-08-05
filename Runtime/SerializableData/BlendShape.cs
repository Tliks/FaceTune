namespace Aoyon.FaceTune;

[Serializable]
public record struct BlendShape
{
    [SerializeField] private string name;
    public string Name { readonly get => name; init => name = value; }
    public const string NamePropName = nameof(name);

    [SerializeField] private float weight;
    public float Weight { readonly get => weight; init => weight = value; }
    public const string WeightPropName = nameof(weight);

    public BlendShape()
    {
        name = "";
        weight = 0.0f;
    }

    public BlendShape(string name, float weight)
    {
        this.name = name;
        this.weight = weight;
    }    

    public readonly bool Equals(BlendShape other)
    {
        return name.Equals(other.name)
            && weight.Equals(other.weight);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(name, weight);
    }
}