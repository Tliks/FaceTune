using nadena.dev.ndmf;

namespace Aoyon.FaceTune;

public abstract class FaceTuneTagComponent : MonoBehaviour, INDMFEditorOnly
{
    internal const string BasePath = FaceTuneConstants.Name;
    internal const string Legacy = "Legacy";
}

internal interface IHasObjectReferences
{
    void ResolveReferences();
}
