namespace Aoyon.FaceTune;

[Serializable]
public record struct BlendShapeWeight
{
    [SerializeField] private string name;
    public string Name { readonly get => name; init => name = value; }
    public const string NamePropName = nameof(name);

    [SerializeField] private float weight;
    public float Weight { readonly get => weight; init => weight = value; }
    public const string WeightPropName = nameof(weight);

    public BlendShapeWeight()
    {
        name = "";
        weight = 0.0f;
    }

    public BlendShapeWeight(string name, float weight)
    {
        this.name = name;
        this.weight = weight;
    }    

    public readonly bool Equals(BlendShapeWeight other)
    {
        return name == other.name
            && weight.Equals(other.weight);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(name, weight);
    }
}