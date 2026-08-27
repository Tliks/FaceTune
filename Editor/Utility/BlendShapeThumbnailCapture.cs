using nadena.dev.ndmf.runtime;
using UnityEngine.SceneManagement;

namespace Aoyon.FaceTune;

internal readonly record struct ThumbnailFraming(
    Vector3 Center,
    Vector3 Forward,
    Vector3 Up,
    float OrthographicSize)
{
    private const float FaceBias = 0.2f;

    public static ThumbnailFraming FromRenderer(
        SkinnedMeshRenderer renderer,
        Transform avatarRoot,
        Animator animator)
    {
        var mesh = renderer.sharedMesh.DestroyedAsNull()
            ?? throw new InvalidOperationException("Face renderer has no mesh.");
        var vertices = mesh.vertices;
        if (vertices.Length == 0)
            throw new InvalidOperationException("Face renderer mesh has no vertices.");

        var boneWeights = mesh.boneWeights;
        var bindPoses = mesh.bindposes;
        var bones = renderer.bones;
        var boneCount = Mathf.Min(bones.Length, bindPoses.Length);
        var validBones = new bool[boneCount];
        var skinMatrices = new Matrix4x4[boneCount];
        for (var index = 0; index < boneCount; index++)
        {
            var bone = bones[index];
            if (bone == null) continue;

            validBones[index] = true;
            skinMatrices[index] = bone.localToWorldMatrix * bindPoses[index];
        }

        var head = animator.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.Head)
            : null;
        if (head != null && TryCreateFraming(head, out var humanoidFraming))
        {
            return humanoidFraming;
        }

        if (TryCreateFraming(null, out var rendererFraming))
        {
            return rendererFraming;
        }

        throw new InvalidOperationException("Face renderer has no vertices that can be framed.");

        bool TryCreateFraming(Transform? headBone, out ThumbnailFraming framing)
        {
            var headBones = new bool[boneCount];
            if (headBone != null)
            {
                for (var index = 0; index < boneCount; index++)
                {
                    var bone = bones[index];
                    headBones[index] = bone == headBone || bone != null && bone.IsChildOf(headBone);
                }
            }

            var right = avatarRoot.right;
            var up = avatarRoot.up;
            var forward = avatarRoot.forward;
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (var index = 0; index < vertices.Length; index++)
            {
                var weight = index < boneWeights.Length ? boneWeights[index] : default;
                var hasSkinning = HasSkinning(weight, validBones);
                var dominantBoneIndex = GetDominantBoneIndex(weight);
                if (headBone != null
                    && (!hasSkinning
                        || dominantBoneIndex < 0
                        || dominantBoneIndex >= headBones.Length
                        || !headBones[dominantBoneIndex])) continue;

                var world = hasSkinning
                    ? SkinVertex(vertices[index], weight, skinMatrices)
                    : renderer.transform.localToWorldMatrix.MultiplyPoint3x4(vertices[index]);
                var relative = world - avatarRoot.position;
                var projected = new Vector3(
                    Vector3.Dot(relative, right),
                    Vector3.Dot(relative, up),
                    Vector3.Dot(relative, forward));
                min = Vector3.Min(min, projected);
                max = Vector3.Max(max, projected);
            }

            if (float.IsPositiveInfinity(min.x))
            {
                framing = default;
                return false;
            }

            var halfWidth = (max.x - min.x) * 0.5f;
            var halfHeight = (max.y - min.y) * 0.5f;
            var center = avatarRoot.position
                + right * ((min.x + max.x) * 0.5f)
                + up * ((min.y + max.y) * 0.5f - halfHeight * FaceBias)
                + forward * ((min.z + max.z) * 0.5f);
            var size = Mathf.Max(halfWidth, halfHeight);
            framing = new ThumbnailFraming(center, forward, up, size);
            return true;
        }
    }

    private static int GetDominantBoneIndex(BoneWeight weight)
    {
        var index = weight.boneIndex0;
        var value = weight.weight0;
        if (weight.weight1 > value) (index, value) = (weight.boneIndex1, weight.weight1);
        if (weight.weight2 > value) (index, value) = (weight.boneIndex2, weight.weight2);
        if (weight.weight3 > value) index = weight.boneIndex3;
        return index;
    }

    private static bool HasSkinning(BoneWeight weight, IReadOnlyList<bool> validBones)
    {
        return HasWeight(weight.boneIndex0, weight.weight0)
            || HasWeight(weight.boneIndex1, weight.weight1)
            || HasWeight(weight.boneIndex2, weight.weight2)
            || HasWeight(weight.boneIndex3, weight.weight3);

        bool HasWeight(int boneIndex, float boneWeight)
        {
            return boneWeight != 0f
                && boneIndex >= 0
                && boneIndex < validBones.Count
                && validBones[boneIndex];
        }
    }

    private static Vector3 SkinVertex(
        Vector3 vertex,
        BoneWeight weight,
        IReadOnlyList<Matrix4x4> skinMatrices)
    {
        var result = Vector3.zero;
        Add(weight.boneIndex0, weight.weight0);
        Add(weight.boneIndex1, weight.weight1);
        Add(weight.boneIndex2, weight.weight2);
        Add(weight.boneIndex3, weight.weight3);
        return result;

        void Add(int boneIndex, float boneWeight)
        {
            if (boneWeight == 0f || boneIndex < 0 || boneIndex >= skinMatrices.Count) return;
            result += skinMatrices[boneIndex].MultiplyPoint3x4(vertex) * boneWeight;
        }
    }
}

internal sealed class BlendShapeThumbnailCapture : IDisposable
{
    // プロジェクトのユーザー定義レイヤーとの重複を避けて選んだ値。
    private const int CaptureLayer = 31;

    private const int TextureSize = 128;

    private readonly SkinnedMeshRenderer _renderer;
    private readonly Mesh _mesh;
    private readonly BlendShapeWeightSet _initialWeights;
    private readonly IReadOnlyDictionary<GameObject, int> _originalLayers;
    private readonly GameObject _cameraRoot;
    private readonly Camera _camera;
    private readonly RenderTexture _target;
    private bool _disposed;

    public BlendShapeThumbnailCapture(SkinnedMeshRenderer renderer, ThumbnailFraming framing)
    {
        using var _ = new Utils.ProfilingSampleScope("FaceTune.Thumbnail.Initialize");
        _renderer = renderer;
        _mesh = renderer.sharedMesh.DestroyedAsNull()
            ?? throw new ArgumentException("Renderer has no mesh.", nameof(renderer));
        _initialWeights = new BlendShapeWeightSet(renderer.GetBlendShapeWeights(_mesh));
        var avatarRoot = RuntimeUtil.FindAvatarInParents(renderer.transform).DestroyedAsNull()
            ?? renderer.transform.root;
        _originalLayers = avatarRoot.GetComponentsInChildren<Renderer>(true)
            .Select(component => component.gameObject)
            .Distinct()
            .ToDictionary(gameObject => gameObject, gameObject => gameObject.layer);

        _cameraRoot = new GameObject($"{FaceTuneConstants.Name} Thumbnail Camera Root");
        SceneManager.MoveGameObjectToScene(_cameraRoot, renderer.gameObject.scene);
        var cameraObject = new GameObject($"{FaceTuneConstants.Name} Thumbnail Camera");
        cameraObject.transform.SetParent(_cameraRoot.transform, false);
        _camera = cameraObject.AddComponent<Camera>();
        _camera.enabled = false;
        _camera.scene = renderer.gameObject.scene;

        var light = cameraObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Color.white;
        light.intensity = 1f;
        light.renderMode = LightRenderMode.ForceVertex;

        _target = new RenderTexture(TextureSize, TextureSize, 24, RenderTextureFormat.ARGB32)
        {
            useMipMap = false,
            autoGenerateMips = false
        };
        _target.Create();

        ConfigureCamera(framing);
        SetCaptureLayers();
    }

    public Texture2D Capture(IReadOnlyBlendShapeSet blendShapes)
    {
        using var _ = new Utils.ProfilingSampleScope("FaceTune.Thumbnail.Capture");
        ThrowIfDisposed();
        GameObject? renderObject = null;
        try
        {
            renderObject = CreateRenderObject(blendShapes);
            _camera.Render();
            return ReadTexture();
        }
        finally
        {
            if (renderObject != null) Object.DestroyImmediate(renderObject);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RestoreLayers();
        _camera.targetTexture = null;
        Object.DestroyImmediate(_target);
        Object.DestroyImmediate(_cameraRoot);
    }

    private void SetCaptureLayers()
    {
        try
        {
            foreach (var gameObject in _originalLayers.Keys)
            {
                if (gameObject != null) gameObject.layer = CaptureLayer;
            }
        }
        catch
        {
            RestoreLayers();
            throw;
        }
    }

    private void RestoreLayers()
    {
        foreach (var (gameObject, layer) in _originalLayers)
        {
            if (gameObject != null) gameObject.layer = layer;
        }
    }

    // UnityのSkinnedMeshRendererは元Rendererへ設定したBlendShapeが撮影用描画へ反映されないことがあるため、顔だけ複製して撮影する。
    private GameObject CreateRenderObject(IReadOnlyBlendShapeSet blendShapes)
    {
        var sourceTransform = _renderer.transform;
        var renderObject = new GameObject($"{FaceTuneConstants.Name} Thumbnail Face")
        {
            layer = CaptureLayer
        };
        try
        {
            SceneManager.MoveGameObjectToScene(renderObject, _renderer.gameObject.scene);
            renderObject.transform.SetParent(sourceTransform.parent, false);
            renderObject.transform.localPosition = sourceTransform.localPosition;
            renderObject.transform.localRotation = sourceTransform.localRotation;
            renderObject.transform.localScale = sourceTransform.localScale;

            var renderRenderer = renderObject.AddComponent<SkinnedMeshRenderer>();
            renderRenderer.sharedMesh = _mesh;
            renderRenderer.bones = _renderer.bones;
            renderRenderer.rootBone = _renderer.rootBone;
            renderRenderer.sharedMaterials = _renderer.sharedMaterials;
            renderRenderer.quality = _renderer.quality;
            renderRenderer.updateWhenOffscreen = _renderer.updateWhenOffscreen;
            renderRenderer.localBounds = _renderer.localBounds;
            renderRenderer.shadowCastingMode = _renderer.shadowCastingMode;
            renderRenderer.receiveShadows = _renderer.receiveShadows;
            renderRenderer.lightProbeUsage = _renderer.lightProbeUsage;
            renderRenderer.reflectionProbeUsage = _renderer.reflectionProbeUsage;
            renderRenderer.probeAnchor = _renderer.probeAnchor;
            renderRenderer.ApplyBlendShapes(_mesh, _initialWeights);
            renderRenderer.ApplyBlendShapes(_mesh, blendShapes);
            return renderObject;
        }
        catch
        {
            Object.DestroyImmediate(renderObject);
            throw;
        }
    }

    private void ConfigureCamera(ThumbnailFraming framing)
    {
        var transform = _camera.transform;
        var forward = framing.Forward.normalized;
        var up = framing.Up.normalized;
        var distance = Mathf.Max(1f, framing.OrthographicSize * 2f);

        transform.position = framing.Center + forward * distance;
        transform.rotation = Quaternion.LookRotation(framing.Center - transform.position, up);
        _camera.orthographic = true;
        _camera.orthographicSize = Mathf.Max(0.01f, framing.OrthographicSize);
        _camera.cullingMask = 1 << CaptureLayer;
        _camera.nearClipPlane = 0.01f;
        _camera.farClipPlane = 100f;
        _camera.allowHDR = false;
        _camera.allowMSAA = false;
        _camera.useOcclusionCulling = false;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = Color.clear;
        _camera.targetTexture = _target;
    }

    private Texture2D ReadTexture()
    {
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = _target;
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, false);
            texture.ReadPixels(new Rect(0, 0, TextureSize, TextureSize), 0, 0, false);
            texture.Apply(false, false);
            texture.Compress(false);
            texture.Apply(false, true);
            return texture;
        }
        finally
        {
            RenderTexture.active = previous;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BlendShapeThumbnailCapture));
    }
}
