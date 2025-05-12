namespace com.aoyon.facetune;

[Serializable]
public struct Parameter : IEqualityComparer<Parameter>
{
    public string Name = string.Empty;
    public ParameterType ParameterType = default;

    public int IntValue = default;
    public float FloatValue = default;
    public bool BoolValue = default;
    public bool TriggerValue = default;

    public Parameter(string name, int intValue)
    {
        Name = name;
        ParameterType = ParameterType.Int;
        IntValue = intValue;
    }

    public Parameter(string name, float floatValue)
    {
        Name = name;
        ParameterType = ParameterType.Float;
        FloatValue = floatValue;
    }

    public Parameter(string name, bool value, bool isTrigger)
    {
        Name = name;
        if (isTrigger)
        {
            ParameterType = ParameterType.Trigger;
            TriggerValue = value;
        }
        else
        {
            ParameterType = ParameterType.Bool;
            BoolValue = value;
        }
    }

    public readonly bool Equals(Parameter other)
    {
        return other is Parameter paramater && Equals(paramater);
    }

    public readonly bool Equals(Parameter x, Parameter y)
    {
        return x.Name == y.Name &&
            x.ParameterType == y.ParameterType &&
            x.IntValue.Equals(y.IntValue) &&
            x.FloatValue.Equals(y.FloatValue) &&
            x.BoolValue.Equals(y.BoolValue) &&
            x.TriggerValue.Equals(y.TriggerValue);
    }

    public readonly int GetHashCode(Parameter obj)
    {
        return HashCode.Combine(obj.Name, obj.ParameterType, obj.IntValue, obj.FloatValue, obj.BoolValue, obj.TriggerValue);
    }
}
