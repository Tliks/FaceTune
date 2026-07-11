using UnityEngine.SceneManagement;

namespace Aoyon.FaceTune;

internal sealed class BlendShapeThumbnailCapture : IDisposable
{
    private const int TextureSize = 128;
    private const float FramingPadding = 1.15f;

    private readonly SkinnedMeshRenderer _renderer;
    private readonly Mesh _mesh;
    private readonly BlendShapeWeightSet _initialWeights;
    private readonly IReadOnlyDictionary<GameObject, int> _originalLayers;
    private readonly Mesh _bakedMesh;
    private readonly GameObject _cameraRoot;
    private readonly Camera _camera;
    private readonly RenderTexture _target;
    private bool _disposed;

    public BlendShapeThumbnailCapture(SkinnedMeshRenderer renderer)
    {
        using var _ = new Utils.ProfilingSampleScope("FaceTune.Thumbnail.Initialize");
        _renderer = renderer;
        _mesh = renderer.sharedMesh ?? throw new ArgumentException("Renderer has no mesh.", nameof(renderer));
        _initialWeights = new BlendShapeWeightSet(renderer.GetBlendShapeWeights(_mesh));
        var avatarRoot = Utils.FindAvatarInParents(renderer.transform) ?? renderer.transform.root;
        _originalLayers = avatarRoot.GetComponentsInChildren<Renderer>(true)
            .Select(component => component.gameObject)
            .Distinct()
            .ToDictionary(gameObject => gameObject, gameObject => gameObject.layer);
        _bakedMesh = new Mesh();

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

        ConfigureCamera(CalculateWorldBounds());
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
        Object.DestroyImmediate(_bakedMesh);
    }

    private void SetCaptureLayers()
    {
        try
        {
            foreach (var gameObject in _originalLayers.Keys)
            {
                if (gameObject != null) gameObject.layer = 31;
            }
            _renderer.gameObject.layer = 30;
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

    private GameObject CreateRenderObject(IReadOnlyBlendShapeSet blendShapes)
    {
        var sourceTransform = _renderer.transform;
        var renderObject = new GameObject($"{FaceTuneConstants.Name} Thumbnail Face")
        {
            layer = 31
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

    private Bounds CalculateWorldBounds()
    {
        _renderer.BakeMesh(_bakedMesh, true);
        var vertices = _bakedMesh.vertices;
        if (vertices.Length == 0) throw new InvalidOperationException("Renderer mesh has no vertices.");

        var transform = _renderer.transform;
        Vector3 ToWorld(Vector3 vertex) => transform.position + transform.rotation * vertex;

        var bounds = new Bounds(ToWorld(vertices[0]), Vector3.zero);
        for (var index = 1; index < vertices.Length; index++)
        {
            bounds.Encapsulate(ToWorld(vertices[index]));
        }
        return bounds;
    }

    private void ConfigureCamera(Bounds bounds)
    {
        var transform = _camera.transform;
        var forward = _renderer.transform.forward;
        var up = _renderer.transform.up;
        var halfHeight = Mathf.Max(bounds.extents.y, bounds.extents.x);
        var distance = Mathf.Max(1f, bounds.extents.z * 2f + 0.1f);

        transform.position = bounds.center + forward * distance;
        transform.rotation = Quaternion.LookRotation(bounds.center - transform.position, up);
        _camera.orthographic = true;
        _camera.orthographicSize = Mathf.Max(0.01f, halfHeight * FramingPadding);
        _camera.cullingMask = 1 << 31;
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
