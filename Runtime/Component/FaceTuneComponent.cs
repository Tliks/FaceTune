using nadena.dev.ndmf.runtime;
using nadena.dev.modular_avatar.core;

namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public sealed class FaceTuneComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = "FT FaceTune";
        internal const string MenuPath = FaceTune + "/" + ComponentName;

        [SerializeField]
        internal AvatarObjectReference m_faceObjectReference = new();
        public GameObject FaceObject { get => m_faceObjectReference.Get(this); set => m_faceObjectReference.Set(value); }

        public FacialExpressionComponent? DefaultExpressionComponent = null;

        public void ResolveReferences() => m_faceObjectReference?.Get(this);

        internal bool CanBuild()
        {
            if (!gameObject.activeInHierarchy) return false;
            if (RuntimeUtil.FindAvatarInParents(transform) is null) return false;
            if (FaceObject == null) return false;
            if (!FaceObject.TryGetComponent<SkinnedMeshRenderer>(out var faceRenderer)) return false;
            if (faceRenderer.sharedMesh == null) return false;
            return true;
        }

        internal SkinnedMeshRenderer? GetFaceRenderer() => FaceObject.NullCast()?.GetComponentNullable<SkinnedMeshRenderer>();
        internal BlendShape[] GetDefaultBlendShapes(SkinnedMeshRenderer faceRenderer, Mesh faceMesh)
        {
            if (DefaultExpressionComponent != null)
            {
                return DefaultExpressionComponent.BlendShapes.ToArray();
            }
            else
            {
                return faceRenderer.GetBlendShapes(faceMesh);
            }
        }

        internal bool TryGetSessionContext(out SessionContext context)
        {
            context = null!;

            if (CanBuild() is false) return false;

            var root = RuntimeUtil.FindAvatarInParents(transform)!.gameObject;
            var faceRenderer = FaceObject.GetComponent<SkinnedMeshRenderer>()!;
            var mesh = faceRenderer.sharedMesh!;

            var blendShapes = GetDefaultBlendShapes(faceRenderer, mesh);

            FacialExpression defaultExpression;
            if (DefaultExpressionComponent != null)
            {
                defaultExpression = new FacialExpression(new BlendShapeSet(DefaultExpressionComponent.BlendShapes), DefaultExpressionComponent.AllowEyeBlink, DefaultExpressionComponent.AllowLipSync, DefaultExpressionComponent.name);
            }
            else
            {
                defaultExpression = new FacialExpression(new BlendShapeSet(blendShapes), TrackingPermission.Allow, TrackingPermission.Allow, "Default");
            }

            context = new SessionContext(root, this, faceRenderer, mesh, defaultExpression, blendShapes);
            return true;
        }
    }

    internal class SessionContext
    {
        public readonly GameObject Root;
        public readonly FaceTuneComponent FaceTuneComponent;
        public readonly SkinnedMeshRenderer FaceRenderer;
        public readonly Mesh FaceMesh;
        public readonly FacialExpression DefaultExpression;
        public readonly BlendShape[] DefaultBlendShapes;

        public SessionContext(GameObject root, FaceTuneComponent faceTuneComponent, SkinnedMeshRenderer faceRenderer, Mesh faceMesh, FacialExpression defaultExpression, BlendShape[] defaultBlendShapes)
        {
            Root = root;
            FaceTuneComponent = faceTuneComponent;
            FaceRenderer = faceRenderer;
            FaceMesh = faceMesh;
            DefaultExpression = defaultExpression;
            DefaultBlendShapes = defaultBlendShapes;
        }
    }
}
