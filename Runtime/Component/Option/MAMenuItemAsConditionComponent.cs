using nadena.dev.modular_avatar.core;

namespace com.aoyon.facetune
{   
    [RequireComponent(typeof(ModularAvatarMenuItem))]
    [AddComponentMenu(MenuPath)]
    public class MAMenuItemAsConditionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT MA Menu Item as Condition";
        internal const string MenuPath = FaceTune + "/" + Option + "/" + ComponentName;
    }
}
