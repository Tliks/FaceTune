using nadena.dev.modular_avatar.core;

namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class OverrideFaceRendererComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Override Face Renderer";
        internal const string MenuPath = BasePath + "/" + Global + "/" + ComponentName;

        [SerializeField]
        internal AvatarObjectReference m_faceObjectReference = new();
        public GameObject? FaceObject { get => m_faceObjectReference.Get(this).DestroyedAsNull(); set => m_faceObjectReference.Set(value); }

        public void ResolveReferences() => m_faceObjectReference?.Get(this);
    }
}
