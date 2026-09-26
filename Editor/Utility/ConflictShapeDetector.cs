namespace Aoyon.FaceTune.Gui;

/// <summary>閉眼時の目の形状に干渉するBlendShapeを検出する。</summary>
internal static class ConflictShapeDetector
{
    // 変形量の半分以上が閉眼周辺に集中する候補だけを残す。
    private const float MinimumRegionFraction = 0.5f;
    // 共通移動を除いた形状変化が2%未満なら、目全体の移動とみなす。
    private const float MinimumResidualFraction = 0.02f;
    // 最大Blink移動量の1%未満の頂点移動や、Blinkに比べ微小な変形を無視する。
    private const float MinimumRelativeMotion = 0.01f;
    // 閉眼位置は頂点間隔の2倍まで探す。
    private const float SpacingWidth = 2f;
    // 移動方向に直交する範囲にも移動量の2割を確保する。
    private const float PerpendicularMotionWidth = 0.2f;
    // Blinkと候補の変形量はウェイト100に換算して比較する。
    private const float FullWeight = 100f;
    // 少数の頂点だけでは閉眼範囲とみなさない。
    private const int MinimumRegionVertices = 4;
    // ほぼゼロのフレームウェイトでは外挿しない。
    private const float MinimumFrameWeight = 0.001f;

    // 有効な閉眼範囲を作れない場合はnullを返し、設定を上書きしない。
    public static IReadOnlyList<string>? DetectBlink(
        Mesh mesh,
        IReadOnlyCollection<BlendShapeWeight> blinkShapes,
        ISet<string> unavailableNames)
    {
        if (!mesh.isReadable || mesh.vertexCount == 0) return null;
        var count = mesh.vertexCount;
        var deltas = new Vector3[count];
        var normals = new Vector3[count];
        var tangents = new Vector3[count];
        var region = new bool[count];
        var referenceEnergy = BuildBlinkRegion(mesh, blinkShapes, region, deltas, normals, tangents);
        var regionIndices = Enumerable.Range(0, count).Where(vertex => region[vertex]).ToArray();
        if (regionIndices.Length < MinimumRegionVertices || referenceEnergy <= 0f) return null;

        var minimumEnergy = referenceEnergy / regionIndices.Length
                            * Mathf.Pow(MinimumRelativeMotion, 2f);
        var references = blinkShapes.Select(shape => shape.Name).ToHashSet(StringComparer.Ordinal);
        return FindConflicts(mesh, regionIndices, references, unavailableNames,
            minimumEnergy, deltas, normals, tangents);
    }

    // リップシンク検出は未実装。現在の設定を維持する。
    public static IReadOnlyList<string>? Detect(
        Mesh mesh,
        IReadOnlyCollection<string> referenceNames,
        ISet<string> unavailableNames) => null;

    private static float BuildBlinkRegion(
        Mesh mesh, IReadOnlyCollection<BlendShapeWeight> blinkShapes, bool[] region,
        Vector3[] deltas, Vector3[] normals, Vector3[] tangents)
    {
        var count = mesh.vertexCount;
        var vertices = mesh.vertices;
        var closed = new Vector3[count];
        var next = new Vector3[count];
        var spacing = VertexSpacing(mesh, vertices);
        var referenceEnergy = 0f;
        foreach (var shape in blinkShapes)
        {
            var index = mesh.GetBlendShapeIndex(shape.Name);
            if (index < 0 || Mathf.Approximately(shape.Weight, 0f)
                || !ReadBlinkDelta(mesh, index, shape.Weight, deltas, next, normals, tangents))
                continue;
                
            var maximum = 0f;
            for (var vertex = 0; vertex < count; vertex++)
            {
                closed[vertex] = vertices[vertex] + deltas[vertex];
                maximum = Mathf.Max(maximum, deltas[vertex].magnitude);
            }
            if (maximum <= 0f) continue;
            var threshold2 = Mathf.Pow(maximum * MinimumRelativeMotion, 2f);
            var moving = new List<int>();
            var distances = new List<float>();
            for (var vertex = 0; vertex < count; vertex++)
            {
                if (deltas[vertex].sqrMagnitude <= threshold2) continue;
                moving.Add(vertex);
                if (spacing[vertex] > 0f) distances.Add(spacing[vertex]);
            }
            if (moving.Count == 0) continue;
            distances.Sort();
            var localSpacing = distances.Count > 0 ? distances[distances.Count / 2] : maximum;
            ExpandClosedRegion(closed, deltas, moving, localSpacing, maximum, region);
            var scale = FullWeight / shape.Weight;
            foreach (var vertex in moving)
                referenceEnergy += deltas[vertex].sqrMagnitude * scale * scale;
        }
        return referenceEnergy;
    }

    private static void ExpandClosedRegion(
        Vector3[] closed, Vector3[] deltas, List<int> moving,
        float spacing, float maximum, bool[] region)
    {
        // 探索範囲が隣のセルまでに収まる大きさで空間を区切る。
        var cellSize = Mathf.Max(maximum, spacing * SpacingWidth);
        var cells = new Dictionary<Vector3Int, List<int>>();
        for (var vertex = 0; vertex < closed.Length; vertex++)
        {
            var cell = Cell(closed[vertex], cellSize);
            if (!cells.TryGetValue(cell, out var contents))
                cells[cell] = contents = new List<int>();
            contents.Add(vertex);
        }
        foreach (var vertex in moving)
        {
            region[vertex] = true;
            var direction = deltas[vertex].normalized;
            var along = Mathf.Max(deltas[vertex].magnitude, spacing);
            var across = Mathf.Max(spacing * SpacingWidth,
                deltas[vertex].magnitude * PerpendicularMotionWidth);
            var cell = Cell(closed[vertex], cellSize);
            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
            for (var z = -1; z <= 1; z++)
            {
                if (!cells.TryGetValue(cell + new Vector3Int(x, y, z), out var contents)) continue;
                foreach (var other in contents)
                {
                    var offset = closed[other] - closed[vertex];
                    var parallel = Vector3.Dot(offset, direction);
                    var perpendicular2 = Mathf.Max(0f, offset.sqrMagnitude - parallel * parallel);
                    if (parallel * parallel / (along * along)
                        + perpendicular2 / (across * across) <= 1f)
                        region[other] = true;
                }
            }
        }
    }

    private static IReadOnlyList<string> FindConflicts(
        Mesh mesh, int[] regionIndices, ISet<string> references,
        ISet<string> unavailableNames, float minimumEnergy,
        Vector3[] deltas, Vector3[] normals, Vector3[] tangents)
    {
        var result = new List<string>();
        for (var index = 0; index < mesh.blendShapeCount; index++)
        {
            var name = mesh.GetBlendShapeName(index);
            if (references.Contains(name) || unavailableNames.Contains(name)) continue;
            for (var frame = 0; frame < mesh.GetBlendShapeFrameCount(index); frame++)
            {
                var weight = mesh.GetBlendShapeFrameWeight(index, frame);
                if (Mathf.Approximately(weight, 0f)) continue;
                mesh.GetBlendShapeFrameVertices(index, frame, deltas, normals, tangents);
                var total = 0f;
                foreach (var delta in deltas) total += delta.sqrMagnitude;
                if (total <= 0f) continue;
                var energy = 0f;
                var sum = Vector3.zero;
                foreach (var vertex in regionIndices)
                {
                    energy += deltas[vertex].sqrMagnitude;
                    sum += deltas[vertex];
                }
                if (energy / total < MinimumRegionFraction) continue;
                var residual = Mathf.Max(0f, energy - sum.sqrMagnitude / regionIndices.Length);
                var scale = FullWeight / weight;
                if (residual / energy < MinimumResidualFraction
                    || residual * scale * scale / regionIndices.Length < minimumEnergy) continue;
                result.Add(name);
                break;
            }
        }
        return result;
    }

    private static float[] VertexSpacing(Mesh mesh, Vector3[] vertices)
    {
        var spacing = new float[vertices.Length];
        for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
        {
            if (mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;
            var triangles = mesh.GetTriangles(submesh);
            for (var i = 0; i < triangles.Length; i += 3)
            {
                Measure(triangles[i], triangles[i + 1]);
                Measure(triangles[i + 1], triangles[i + 2]);
                Measure(triangles[i + 2], triangles[i]);
            }
        }
        return spacing;

        void Measure(int a, int b)
        {
            var distance = Vector3.Distance(vertices[a], vertices[b]);
            if (distance <= 0f) return;
            if (spacing[a] == 0f || distance < spacing[a]) spacing[a] = distance;
            if (spacing[b] == 0f || distance < spacing[b]) spacing[b] = distance;
        }
    }

    private static bool ReadBlinkDelta(
        Mesh mesh, int index, float weight,
        Vector3[] deltas, Vector3[] next, Vector3[] normals, Vector3[] tangents)
    {
        var frames = mesh.GetBlendShapeFrameCount(index);
        if (frames == 0) return false;
        var upper = 0;
        while (upper < frames && mesh.GetBlendShapeFrameWeight(index, upper) < weight)
            upper++;
        if (upper == 0 || upper == frames)
        {
            var frame = upper == 0 ? 0 : frames - 1;
            var frameWeight = mesh.GetBlendShapeFrameWeight(index, frame);
            if (Mathf.Abs(frameWeight) < MinimumFrameWeight) return false;
            mesh.GetBlendShapeFrameVertices(index, frame, deltas, normals, tangents);
            var scale = weight / frameWeight;
            for (var vertex = 0; vertex < deltas.Length; vertex++) deltas[vertex] *= scale;
            return true;
        }
        var lowerWeight = mesh.GetBlendShapeFrameWeight(index, upper - 1);
        var upperWeight = mesh.GetBlendShapeFrameWeight(index, upper);
        if (Mathf.Approximately(lowerWeight, upperWeight)) return false;
        mesh.GetBlendShapeFrameVertices(index, upper - 1, deltas, normals, tangents);
        mesh.GetBlendShapeFrameVertices(index, upper, next, normals, tangents);
        var t = (weight - lowerWeight) / (upperWeight - lowerWeight);
        for (var vertex = 0; vertex < deltas.Length; vertex++)
            deltas[vertex] = Vector3.LerpUnclamped(deltas[vertex], next[vertex], t);
        return true;
    }

    private static Vector3Int Cell(Vector3 position, float size) => new(
        Mathf.FloorToInt(position.x / size),
        Mathf.FloorToInt(position.y / size),
        Mathf.FloorToInt(position.z / size));
}
