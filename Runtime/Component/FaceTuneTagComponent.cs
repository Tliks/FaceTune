using nadena.dev.ndmf;

namespace Aoyon.FaceTune;

internal abstract class FaceTuneTagComponent : MonoBehaviour, INDMFEditorOnly
{
    internal const string ComponentNamePrefix = FaceTuneConstants.Name + " ";
    internal const string MenuPathPrefix = FaceTuneConstants.Name + "/";
    internal const string LegacyMenuPathPrefix = MenuPathPrefix + "Legacy/";
    internal const string OptionMenuPathPrefix = MenuPathPrefix + "Option/";
}
