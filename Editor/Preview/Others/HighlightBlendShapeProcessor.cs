using System.Threading;
using System.Threading.Tasks;

namespace aoyon.facetune.preview;

// とりあえず動く

internal class HighlightBlendShapeProcessor : IDisposable
{
    private SkinnedMeshRenderer? _renderer;
    private CancellationTokenSource? cancellationTokenSource;
    private Mesh? _bakedMesh;
    private Task<int[][]>? _latestTask;
    private HilightBlendShape? _hlighter;
    private static readonly int[] emptyArray = new int[0];

    public HighlightBlendShapeProcessor()
    { 
    }

    public void RefreshTarget(SkinnedMeshRenderer? renderer, Mesh? mesh)
    {
        Dispose();
        if (renderer == null || mesh == null) return;
        _renderer = renderer;
        cancellationTokenSource = new CancellationTokenSource();
        _latestTask = CalculateBlendShapeTriangleIndices(mesh, cancellationTokenSource.Token);
    }

    public void HilightBlendShapeFor(int blendShapeIndex)
    {
        EnsureHilighter();
        var indices = GetTrinangleIndicesFor(blendShapeIndex);
        _hlighter!.Mesh.triangles = indices;
    }

    public void ClearHighlight()
    {
        if (_hlighter != null)
        {
            _hlighter.Mesh.triangles = emptyArray;
        }
    }

    private void EnsureHilighter()
    {
        if (_hlighter != null || _renderer == null) return;

        _hlighter = _renderer.gameObject.AddComponent<HilightBlendShape>();

        _bakedMesh = new Mesh();
        Debug.Log($"BakeMesh: {_renderer.name}");
        _renderer.BakeMesh(_bakedMesh);
        _hlighter.Mesh = _bakedMesh;
        _hlighter.Position = _renderer.transform.position;
    }

    private int[] GetTrinangleIndicesFor(int blendShapeIndex)
    {
        if (_latestTask == null) return emptyArray;
        if (!_latestTask.IsCompleted) return emptyArray;

        return _latestTask.Result[blendShapeIndex];
    }

    private static Task<int[][]> CalculateBlendShapeTriangleIndices(Mesh mesh, CancellationToken token = default)
    {
        int blendShapeCount = mesh.blendShapeCount;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        // メインスレッドで必要な情報を取得
        var frameCounts = new int[blendShapeCount];
        var deltaVerticesList = new List<Vector3[]>();

        Vector3[] temp = new Vector3[vertices.Length];

        for (int i = 0; i < blendShapeCount; i++)
        {
            frameCounts[i] = mesh.GetBlendShapeFrameCount(i);
            // 各フレームの頂点情報を取得してリストに追加
            for (int j = 0; j < frameCounts[i]; j++)
            {
                Vector3[] deltaVertices = new Vector3[vertices.Length];
                mesh.GetBlendShapeFrameVertices(i, j, deltaVertices, temp, temp);
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
        cancellationTokenSource?.Cancel();
        _latestTask = null;

        if (_bakedMesh != null) Object.DestroyImmediate(_bakedMesh);
        _bakedMesh = null;

        if (_hlighter != null) Object.DestroyImmediate(_hlighter);
        _hlighter = null;
    }
}
