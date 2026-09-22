namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class BlendShapeCatalog
{
    private readonly string[] _names;
    private readonly Dictionary<string, int> _indices;

    public IReadOnlyList<string> Names => _names;

    public BlendShapeCatalog(SkinnedMeshRenderer? renderer)
    {
        var mesh = renderer == null ? null : renderer.sharedMesh;
        _names = mesh == null
            ? Array.Empty<string>()
            : Enumerable.Range(0, mesh.blendShapeCount)
                .Select(mesh.GetBlendShapeName)
                .ToArray();
        _indices = _names
            .Select((name, index) => (name, index))
            .ToDictionary(entry => entry.name, entry => entry.index, StringComparer.Ordinal);
    }

    public bool Contains(string name) => _indices.ContainsKey(name);

    public int IndexOf(string name)
        => _indices.TryGetValue(name, out var index) ? index : -1;
}
