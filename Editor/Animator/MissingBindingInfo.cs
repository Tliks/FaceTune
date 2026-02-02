namespace Aoyon.FaceTune.Animator;

public class MissingBindingInfo
{
    public enum Reason
    {
        Missing,
        MultipleValues,
    }

    public EditorCurveBinding Binding { get; }
    public Reason MissingReason { get; }
    public MissingBindingInfo(EditorCurveBinding binding, Reason reason)
    {
        Binding = binding;
        MissingReason = reason;
    }
}
