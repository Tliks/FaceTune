using nadena.dev.ndmf;

namespace Aoyon.FaceTune;

internal abstract class FaceTuneTagComponent : MonoBehaviour, INDMFEditorOnly, IFaceTuneProvider
{
    Component IFaceTuneProvider.Component => this;

    internal const string ComponentNamePrefix = FaceTuneConstants.Name + " ";
    internal const string MenuPathPrefix = FaceTuneConstants.Name + "/";
    internal const string OptionMenuPathPrefix = MenuPathPrefix + "Option/";
}
