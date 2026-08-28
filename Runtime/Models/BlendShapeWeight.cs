namespace Aoyon.FaceTune;

/// <summary>Serialized shape value shared by current and legacy data.</summary>
[Serializable]
internal record struct BlendShapeWeight
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

}