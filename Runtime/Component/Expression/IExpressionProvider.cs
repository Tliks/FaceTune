namespace com.aoyon.facetune;

internal interface IExpressionProvider
{
    Expression? ToExpression(SessionContext context);
}

public abstract class ExpressionComponentBase : FaceTuneTagComponent
{
}

public abstract class FacialExpressionComponentBase : ExpressionComponentBase
{
    public bool AddDefault = true;
    
    public bool AllowEyeBlink = false;
    public bool AllowLipSync = true;
}

internal abstract class Expression
{
    public string Name;

    public Expression(string name)
    {
        Name = name;
    }
}

internal class FacialExpression : Expression
{
    public BlendShapeSet BlendShapes;
    public bool AllowEyeBlink;
    public bool AllowLipSync;

    public FacialExpression(BlendShapeSet blendShapes, bool allowEyeBlink, bool allowLipSync, string name) : base(name)
    {
        BlendShapes = blendShapes;
        AllowEyeBlink = allowEyeBlink;
        AllowLipSync = allowLipSync;
    }
}

internal class AnimationExpression : Expression
{
    public AnimationClip Clip;

    public AnimationExpression(AnimationClip clip, string name) : base(name)
    {
        Clip = clip;
    }
}

