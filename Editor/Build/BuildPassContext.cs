using nadena.dev.ndmf;
using aoyon.facetune.platform;

namespace aoyon.facetune.build;

internal class BuildPassState
{
    public bool Enabled { get; }
    public BuildPassContext? BuildPassContext { get; }

    public BuildPassState()
    {
        throw new Exception("BuildPassState is not initialized");
    }

    public BuildPassState(BuildContext buildContext) : this(buildContext.AvatarRootObject)
    {
    }

    public BuildPassState(GameObject root)
    {
        Enabled = SessionContextBuilder.TryBuild(root, out var sessionContext);
        if (!Enabled) return;

        var platformSupport = platform.PlatformSupport.GetSupport(root.transform);
        BuildPassContext = new BuildPassContext(sessionContext!, platformSupport);
    }

    public bool TryGetBuildPassContext([NotNullWhen(true)] out BuildPassContext? buildPassContext)
    {
        if (Enabled)
        {
            buildPassContext = BuildPassContext!;
            return true;
        }
        buildPassContext = null;
        return false;
    }
}

internal class BuildPassContext
{
    public SessionContext SessionContext { get; }
    public IPlatformSupport PlatformSupport { get; }
    private readonly HashSet<string> _trackedShapes;
    private readonly Dictionary<string, string> _cloneMapping;

    public BuildPassContext(SessionContext sessionContext, IPlatformSupport platformSupport)
    {
        SessionContext = sessionContext;
        PlatformSupport = platformSupport;
        _trackedShapes = platformSupport.GetTrackedBlendShape().ToHashSet();
        _cloneMapping = new();
    }

    public bool IsTrackedShape(string shapeName) => _trackedShapes.Contains(shapeName);
    public bool IsClonedShape(string shapeName) => _cloneMapping.ContainsKey(shapeName);

    public Dictionary<string, BlendShapeStatus> ClassifyBlendShapes(HashSet<string> shapes)
    {
        var ret = new Dictionary<string, BlendShapeStatus>();

        var renderer = SessionContext.FaceRenderer;
        var allShapes = renderer.GetBlendShapes(SessionContext.FaceMesh).Select(b => b.Name).ToHashSet();

        foreach (var shape in shapes)
        {
            if (!allShapes.Contains(shape))
            {
                ret[shape] = BlendShapeStatus.NotExist;
            }
            else if (IsClonedShape(shape))
            {
                ret[shape] = BlendShapeStatus.Cloned;
            }
            else if (IsTrackedShape(shape))
            {
                ret[shape] = BlendShapeStatus.Tracked;
            }
            else
            {
                ret[shape] = BlendShapeStatus.Safe;
            }
        }
        return ret;
    }

    public Dictionary<string, string> GetSafeBlendShapes(HashSet<string> shapes)
    {
        var ret = new Dictionary<string, string>();
        var shapesToClone = new HashSet<string>();
        foreach (var shape in shapes)
        {
            if (!allShapes.Contains(shape)) continue;
            if (!IsTrackedShape(shape))
            {
                ret[shape] = shape;
            }
            else if (IsClonedShape(shape))
            {
                ret[shape] = _cloneMapping[shape];
            }
            else
            {
                shapesToClone.Add(shape);
            }
        }

        var oldMesh = renderer.sharedMesh;
        var newMesh = Object.Instantiate(oldMesh);
        ObjectRegistry.RegisterReplacedObject(oldMesh, newMesh);
        var mapping = MeshHelper.CloneShapes(newMesh, shapesToClone);
        renderer.sharedMesh = newMesh;

        foreach (var (originalName, cloneName) in mapping)
        {
            ret[originalName] = cloneName;
        }
        RegisterCloneMapping(mapping);

        return ret;
    }

    public void RegisterCloneMapping(Dictionary<string, string> mapping)
    {
        foreach (var (originalName, cloneName) in mapping)
        {
            RegisterCloneMapping(originalName, cloneName);
        }
    }

    public void RegisterCloneMapping(string originalName, string cloneName)
    {
        _cloneMapping[originalName] = cloneName;
    }
}

internal enum BlendShapeStatus
{
    NotExist,
    Cloned,
    Tracked,
    Safe,
}