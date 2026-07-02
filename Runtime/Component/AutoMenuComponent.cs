using nadena.dev.modular_avatar.core;

namespace Aoyon.FaceTune;

[DisallowMultipleComponent]
[AddComponentMenu(BaseMenuPath + "/" + ComponentName)]
internal class AutoMenuComponent : FaceTuneTagComponent, IHasObjectReferences
{
    internal const string ComponentName = FaceTuneConstants.ComponentPrefix + " Auto Menu";

    public MenuIconSettings Icon = new();
    public MenuInstallSettings InstallSettings = new();

    public List<AvatarObjectReference> ExcludeFromMenuTargets = new();
    public List<AvatarObjectReference> AllowDuringManualLockTargets = new();

    public void ResolveReferences()
    {
        InstallSettings.ResolveReferences(this);
        foreach (var target in ExcludeFromMenuTargets)
        {
            target.Get(this);
        }
        foreach (var target in AllowDuringManualLockTargets)
        {
            target.Get(this);
        }
    }
}
