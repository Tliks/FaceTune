namespace com.aoyon.facetune;

internal abstract record class Expression
{
    public string Name { get; private set; }
    public TrackingPermission AllowEyeBlink { get; private set; }
    public TrackingPermission AllowLipSync { get; private set; }

    public Expression(string name, TrackingPermission allowEyeBlink, TrackingPermission allowLipSync)
    {
        Name = name;
        AllowEyeBlink = allowEyeBlink;
        AllowLipSync = allowLipSync;
    }

    public abstract IEnumerable<string> BlendShapeNames { get; }
    public abstract void ReplaceBlendShapeNames(Dictionary<string, string> mapping);
    public abstract void RemoveShapes(IEnumerable<string> names);
}

internal record class FacialExpression : Expression
{
    public BlendShapeSet BlendShapeSet { get; private set; }
    public FacialExpression(BlendShapeSet blendShapes, TrackingPermission allowEyeBlink, TrackingPermission allowLipSync, string name) : base(name, allowEyeBlink, allowLipSync)
    {
        BlendShapeSet = blendShapes;
    }

    public override IEnumerable<string> BlendShapeNames => BlendShapeSet.Names;

    public override void ReplaceBlendShapeNames(Dictionary<string, string> mapping)
    {
        BlendShapeSet.ReplaceNames(mapping);
    }

    public override void RemoveShapes(IEnumerable<string> names)
    {
        BlendShapeSet.Remove(names);
    }
}

internal record class AnimationExpression : Expression
{
    public AnimationClip Clip { get; private set; }

    public AnimationExpression(AnimationClip clip, TrackingPermission allowEyeBlink, TrackingPermission allowLipSync, string name) : base(name, allowEyeBlink, allowLipSync)
    {
        Clip = clip;
    }

    public override IEnumerable<string> BlendShapeNames => new string[0]; // Todo

    public override void ReplaceBlendShapeNames(Dictionary<string, string> mapping)
    {
        return; // Todo
    }

    public override void RemoveShapes(IEnumerable<string> names)
    {
        return; // Todo
    }
}