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

    public abstract void ReplaceBlendShapeName(string oldName, string newName);
}

internal record class FacialExpression : Expression
{
    public BlendShapeSet BlendShapes { get; private set; }
    public FacialExpression(BlendShapeSet blendShapes, TrackingPermission allowEyeBlink, TrackingPermission allowLipSync, string name) : base(name, allowEyeBlink, allowLipSync)
    {
        BlendShapes = blendShapes;
    }

    public override IEnumerable<string> BlendShapeNames => BlendShapes.Names();

    public override void ReplaceBlendShapeName(string oldName, string newName)
    {
        BlendShapes.ReplaceName(oldName, newName);
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

    public override void ReplaceBlendShapeName(string oldName, string newName)
    {
        return; // Todo
    }
}