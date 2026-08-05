namespace Aoyon.FaceTune;

[Serializable]
internal struct FloatRange
{
    [SerializeField] private float min;
    [SerializeField] private float max;

    public float Min { get => min; init => min = value; }
    public float Max { get => max; init => max = value; }

    public FloatRange(float min, float max)
    {
        this.min = min;
        this.max = max;
    }
}
