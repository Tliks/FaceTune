namespace com.aoyon.facetune;

[Serializable]
public record struct BlendShape
{
    public string Name;
    public float Weight;

    public BlendShape()
    {
        Name = "";
        Weight = 0.0f;
    }

    public BlendShape(string name, float weight)
    {
        Name = name;
        Weight = weight;
    }    

    public readonly bool Equals(BlendShape other)
    {
        return Name.Equals(other.Name)
            && Mathf.Approximately(Weight, other.Weight);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Weight);
    }
}