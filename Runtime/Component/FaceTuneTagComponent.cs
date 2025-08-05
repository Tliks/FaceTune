using nadena.dev.ndmf;

namespace Aoyon.FaceTune;

public abstract class FaceTuneTagComponent : MonoBehaviour, INDMFEditorOnly
{
    internal const string BasePath = FaceTuneConsts.Name;
    internal const string Expression = "Expression";
    internal const string ExpressionPattern = "ExpressionPattern";
    internal const string Global = "Global";
    internal const string Option = "Option";
    internal const string Preview = "Preview";
    internal const string EditorOnly = "EditorOnly";
    internal const string Tracking = "Tracking";
}