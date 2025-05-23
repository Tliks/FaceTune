namespace com.aoyon.facetune;

internal class HilightBlendShape : FaceTuneTagComponent
{
    public Mesh Mesh = null!;
    public Vector3 Position = default;
    public Color highlightColor = Color.red;

    public void Init(Mesh mesh)
    {
        Mesh = mesh;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = highlightColor;
        if (Mesh == null || Mesh.triangles == null) return;
        Gizmos.DrawWireMesh(Mesh, Position);
    }
}
