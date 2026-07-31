
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    [Obsolete]
    internal class OverrideFaceRendererComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = ComponentNamePrefix + "Override Face Renderer";
        internal const string MenuPath = LegacyMenuPathPrefix + ComponentName;

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
