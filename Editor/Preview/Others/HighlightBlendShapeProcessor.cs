using System.Threading;
using System.Threading.Tasks;

namespace com.aoyon.facetune.preview;

// とりあえず動く

internal class HighlightBlendShapeProcessor : IDisposable
{
    private readonly SkinnedMeshRenderer _renderer;
    private readonly Mesh _mesh;
    private readonly CancellationTokenSource cancellationTokenSource;
    private Mesh? _bakedMesh;
    private Task<int[][]>? latestTask;
    private HilightBlendShape? hlighter;
    private static readonly int[] emptyArray = new int[0];

    public HighlightBlendShapeProcessor(SkinnedMeshRenderer renderer, Mesh mesh)
    { 
        _renderer = renderer;
        _mesh = mesh;
        cancellationTokenSource = new CancellationTokenSource();
        latestTask = CalculateBlendShapeTriangleIndices(cancellationTokenSource.Token);            
    }

    public void HilightBlendShapeFor(int blendShapeIndex)
    {
        EnsureHilighter();
        var indices = GetTrinangleIndicesFor(blendShapeIndex);
        hlighter!.Mesh.triangles = indices;
    }

    public void ClearHighlight()
    {
        if (hlighter != null)
        {
            hlighter.Mesh.triangles = emptyArray;
        }
    }

    private void EnsureHilighter()
    {
        if (hlighter != null) return;

        hlighter = _renderer.gameObject.AddComponent<HilightBlendShape>();

        _bakedMesh = new Mesh();
        Debug.Log($"BakeMesh: {_renderer.name}");
        _renderer.BakeMesh(_bakedMesh);
        hlighter.Mesh = _bakedMesh;
        hlighter.Position = _renderer.transform.position;
    }

    private int[] GetTrinangleIndicesFor(int blendShapeIndex)
    {
        if (latestTask == null) return emptyArray;
        if (!latestTask.IsCompleted) return emptyArray;

        return latestTask.Result[blendShapeIndex];
    }

    private Task<int[][]> CalculateBlendShapeTriangleIndices(CancellationToken token = default)
    {
        int blendShapeCount = _mesh.blendShapeCount;
        Vector3[] vertices = _mesh.vertices;
        int[] triangles = _mesh.triangles;

        // メインスレッドで必要な情報を取得
        var frameCounts = new int[blendShapeCount];
        var deltaVerticesList = new List<Vector3[]>();

        Vector3[] temp = new Vector3[vertices.Length];

        for (int i = 0; i < blendShapeCount; i++)
        {
            frameCounts[i] = _mesh.GetBlendShapeFrameCount(i);
            // 各フレームの頂点情報を取得してリストに追加
            for (int j = 0; j < frameCounts[i]; j++)
            {
                Vector3[] deltaVertices = new Vector3[vertices.Length];
                _mesh.GetBlendShapeFrameVertices(i, j, deltaVertices, temp, temp);
                deltaVerticesList.Add(deltaVertices);
            }
        }

        return Task.Run(() =>
        {
            int[][] blendShapeTriangleIndices = new int[blendShapeCount][];
            float threshold = 0.00000001f;

            int deltaVerticesIndex = 0;
            for (int blendShapeIndex = 0; blendShapeIndex < blendShapeCount; blendShapeIndex++)
            {
                List<int> indices = new List<int>();

                int frameCount = frameCounts[blendShapeIndex];
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    Vector3[] deltaVertices = deltaVerticesList[deltaVerticesIndex++];

                    HashSet<int> movedVertexIndices = new HashSet<int>();
                    for (int vertexIndex = 0; vertexIndex < deltaVertices.Length; vertexIndex++)
                    {
                        if (deltaVertices[vertexIndex].sqrMagnitude > threshold)
                        {
                            movedVertexIndices.Add(vertexIndex);
                        }
                    }

                    for (int triangleIndex = 0; triangleIndex < triangles.Length; triangleIndex += 3)
                    {
                        int vertexIndex1 = triangles[triangleIndex];
                        int vertexIndex2 = triangles[triangleIndex + 1];
                        int vertexIndex3 = triangles[triangleIndex + 2];

                        if (movedVertexIndices.Contains(vertexIndex1) || movedVertexIndices.Contains(vertexIndex2) || movedVertexIndices.Contains(vertexIndex3))
                        {
                            //重複しないように追加
                            indices.Add(vertexIndex1);
                            indices.Add(vertexIndex2);
                            indices.Add(vertexIndex3);
                        }
                    }
                }
                blendShapeTriangleIndices[blendShapeIndex] = indices.ToArray();
            }

            deltaVerticesList = null;

            return blendShapeTriangleIndices;
        }, token);
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        latestTask = null;

        if (_bakedMesh != null) Object.DestroyImmediate(_bakedMesh);
        _bakedMesh = null;

        if (hlighter != null) Object.DestroyImmediate(hlighter);
        hlighter = null;
    }
}
