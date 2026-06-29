using nadena.dev.ndmf;

namespace Aoyon.FaceTune;

internal abstract class FaceTuneTagComponent : MonoBehaviour, INDMFEditorOnly
{
    internal const string BaseMenuPath = FaceTuneConstants.Name;
    internal const string LegacyMenuName = "Legacy";
}

internal interface IHasObjectReferences
{
    void ResolveReferences();
}
