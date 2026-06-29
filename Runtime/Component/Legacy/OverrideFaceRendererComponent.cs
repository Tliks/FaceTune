using nadena.dev.modular_avatar.core;

namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    [Obsolete]
    internal class OverrideFaceRendererComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Override Face Renderer";
        internal const string MenuPath = BaseMenuPath + "/" + LegacyMenuName + "/" + ComponentName;

        [SerializeField]
        internal AvatarObjectReference m_faceObjectReference = new();
        public GameObject? FaceObject
        {
            get
            {
                var obj = m_faceObjectReference.Get(this);
                return obj == null ? null : obj;
            }
            set => m_faceObjectReference.Set(value);
        }

        public void ResolveReferences() => m_faceObjectReference?.Get(this);
    }
}
