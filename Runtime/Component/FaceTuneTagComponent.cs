using nadena.dev.ndmf;

namespace com.aoyon.facetune;

public abstract class FaceTuneTagComponent : MonoBehaviour, INDMFEditorOnly
{
    internal const string FaceTune = "FaceTune";
    internal const string Expression = "Expression";
    internal const string ExpressionPattern = "ExpressionPattern";
    internal const string Global = "Global";
    internal const string Option = "Option";
    internal const string Preview = "Preview";
    internal const string EditorOnly = "EditorOnly";
}