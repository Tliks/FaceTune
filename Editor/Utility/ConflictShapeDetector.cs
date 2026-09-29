namespace Aoyon.FaceTune.Gui;

/// <summary>基準Shapeの変形範囲に干渉するBlendShapeを検出する。</summary>
internal static class ConflictShapeDetector
{
    // 有効な閉眼範囲を作れない場合はnullを返し、設定を上書きしない。
    public static IReadOnlyList<string>? DetectBlink(
        Mesh mesh,
        IReadOnlyCollection<BlendShapeWeight> blinkShapes,
        ISet<string> unavailableNames)
    {
        if (!mesh.isReadable || mesh.vertexCount == 0) return null;

        var references = blinkShapes.Select(shape => shape.Name).ToHashSet(StringComparer.Ordinal);
        bool[] region;
        Vector3[] closed;
        float maximum;
        using (new Utils.ProfilingSampleScope("ConflictShapeDetector.BlinkRegion"))
            (region, closed, maximum) = BuildBlinkRegion(mesh, blinkShapes);
        if (maximum <= 0f) return null;
        IReadOnlyList<(int First, int Second)> pairs;
        var pairDegrees = new int[mesh.vertexCount];
        using (new Utils.ProfilingSampleScope("ConflictShapeDetector.BlinkPairs"))
        {
            pairs = BuildNearbyPairs(closed, region, maximum * ClosedRegionWidthFraction);
            foreach (var (first, second) in pairs)
            {
                pairDegrees[first]++;
                pairDegrees[second]++;
            }
        }
        if (pairs.Count == 0) return null;
        using (new Utils.ProfilingSampleScope("ConflictShapeDetector.BlinkConflicts"))
            return FindConflicts(mesh, region, references, unavailableNames, pairs, pairDegrees);
    }

    public static IReadOnlyList<string>? DetectLipSync(
        Mesh mesh,
        IReadOnlyCollection<string> referenceNames,
        ISet<string> unavailableNames)
    {
        if (!mesh.isReadable || mesh.vertexCount == 0) return null;

        var region = BuildLipSyncRegion(mesh, referenceNames);
        var references = referenceNames.ToHashSet(StringComparer.Ordinal);
        return FindConflicts(mesh, region, references, unavailableNames);
    }

    // 最大Blink移動量の一定値未満は起点にしない。
    private const float MinimumBlinkMotionFraction = 0.01f;

    private static (bool[] Region, Vector3[] Closed, float Maximum) BuildBlinkRegion(
        Mesh mesh, IReadOnlyCollection<BlendShapeWeight> blinkShapes)
    {
        var count = mesh.vertexCount;
        var vertices = mesh.vertices;
        var region = new bool[count];
        var closed = new Vector3[count];
        var combinedDeltas = new Vector3[count];
        var deltas = new Vector3[count];
        var next = new Vector3[count];
        var normals = new Vector3[count];
        var tangents = new Vector3[count];

        foreach (var shape in blinkShapes)
        {
            var index = mesh.GetBlendShapeIndex(shape.Name);
            if (index < 0 || Mathf.Approximately(shape.Weight, 0f)
                || !ReadBlinkDelta(index, shape.Weight))
                continue;

            for (var vertex = 0; vertex < count; vertex++)
                combinedDeltas[vertex] += deltas[vertex];
        }
        var (moving, maximum) = FindBlinkSeeds();
        if (moving.Count == 0) return (region, closed, 0f);
        for (var vertex = 0; vertex < count; vertex++)
            closed[vertex] = vertices[vertex] + combinedDeltas[vertex];
        ExpandClosedRegion(closed, moving, maximum, region);
        return (region, closed, maximum);

        (List<int> Moving, float Maximum) FindBlinkSeeds()
        {
            var maximumSquared = 0f;
            for (var vertex = 0; vertex < count; vertex++)
                maximumSquared = Mathf.Max(maximumSquared, combinedDeltas[vertex].sqrMagnitude);
            var moving = new List<int>();
            if (maximumSquared <= 0f) return (moving, 0f);

            var thresholdSquared = maximumSquared
                * MinimumBlinkMotionFraction * MinimumBlinkMotionFraction;
            for (var vertex = 0; vertex < count; vertex++)
                if (combinedDeltas[vertex].sqrMagnitude >= thresholdSquared) moving.Add(vertex);
            return (moving, Mathf.Sqrt(maximumSquared));
        }

        bool ReadBlinkDelta(int index, float weight)
        {
            var frameCount = mesh.GetBlendShapeFrameCount(index);
            if (frameCount == 0) return false;

            var lastFrame = frameCount - 1;
            var lastWeight = mesh.GetBlendShapeFrameWeight(index, lastFrame);
            if (Mathf.Approximately(lastWeight, 0f)) return false;
            var targetWeight = Mathf.Min(weight, lastWeight);

            var frame = 0;
            var frameWeight = mesh.GetBlendShapeFrameWeight(index, frame);
            while (frame < lastFrame && frameWeight < targetWeight)
            {
                frame++;
                frameWeight = mesh.GetBlendShapeFrameWeight(index, frame);
            }

            if (frame == 0)
            {
                if (Mathf.Approximately(frameWeight, 0f)) return false;
                mesh.GetBlendShapeFrameVertices(index, frame, deltas, normals, tangents);
                var scale = targetWeight / frameWeight;
                for (var vertex = 0; vertex < count; vertex++) deltas[vertex] *= scale;
                return true;
            }

            var previousWeight = mesh.GetBlendShapeFrameWeight(index, frame - 1);
            if (Mathf.Approximately(previousWeight, frameWeight)) return false;
            mesh.GetBlendShapeFrameVertices(index, frame - 1, deltas, normals, tangents);
            mesh.GetBlendShapeFrameVertices(index, frame, next, normals, tangents);
            var t = (targetWeight - previousWeight) / (frameWeight - previousWeight);
            for (var vertex = 0; vertex < count; vertex++)
                deltas[vertex] = Vector3.LerpUnclamped(deltas[vertex], next[vertex], t);
            return true;
        }
    }

    // リップシンクの各フレームで最大移動量の一定値未満は範囲に含めない。
    private const float MinimumLipSyncMotionFraction = 0.01f;

    private static bool[] BuildLipSyncRegion(
        Mesh mesh, IReadOnlyCollection<string> referenceNames)
    {
        var count = mesh.vertexCount;
        var region = new bool[count];
        var deltas = new Vector3[count];
        var normals = new Vector3[count];
        var tangents = new Vector3[count];
        foreach (var name in referenceNames)
        {
            var index = mesh.GetBlendShapeIndex(name);
            if (index < 0) continue;
            for (var frame = 0; frame < mesh.GetBlendShapeFrameCount(index); frame++)
            {
                var weight = mesh.GetBlendShapeFrameWeight(index, frame);
                if (Mathf.Approximately(weight, 0f)) continue;
                mesh.GetBlendShapeFrameVertices(index, frame, deltas, normals, tangents);
                var maximum = 0f;
                foreach (var delta in deltas) maximum = Mathf.Max(maximum, delta.sqrMagnitude);
                if (maximum <= 0f) continue;
                var threshold2 = maximum * MinimumLipSyncMotionFraction * MinimumLipSyncMotionFraction;
                for (var vertex = 0; vertex < count; vertex++)
                    if (deltas[vertex].sqrMagnitude >= threshold2) region[vertex] = true;
            }
        }
        return region;
    }

    private const float ClosedRegionWidthFraction = 0.1f;

    private static void ExpandClosedRegion(
        Vector3[] closed, List<int> moving, float maximum, bool[] region)
    {
        var radius = maximum * ClosedRegionWidthFraction;
        var radiusSquared = radius * radius;
        // 半径をセル幅にすると、探索対象は隣接セルまでに収まる。
        var cellSize = radius;
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
            var cell = Cell(closed[vertex], cellSize);
            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
            for (var z = -1; z <= 1; z++)
            {
                if (!cells.TryGetValue(cell + new Vector3Int(x, y, z), out var contents)) continue;
                foreach (var other in contents)
                {
                    if ((closed[other] - closed[vertex]).sqrMagnitude <= radiusSquared)
                        region[other] = true;
                }
            }
        }
    }

    private static IReadOnlyList<(int First, int Second)> BuildNearbyPairs(
        Vector3[] closed, bool[] region, float radius)
    {
        var cells = new Dictionary<Vector3Int, List<int>>();
        for (var vertex = 0; vertex < region.Length; vertex++)
        {
            if (!region[vertex]) continue;
            var cell = Cell(closed[vertex], radius);
            if (!cells.TryGetValue(cell, out var contents))
                cells[cell] = contents = new List<int>();
            contents.Add(vertex);
        }

        var pairs = new List<(int First, int Second)>();
        var radiusSquared = radius * radius;
        for (var vertex = 0; vertex < region.Length; vertex++)
        {
            if (!region[vertex]) continue;
            var cell = Cell(closed[vertex], radius);
            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
            for (var z = -1; z <= 1; z++)
            {
                if (!cells.TryGetValue(cell + new Vector3Int(x, y, z), out var contents)) continue;
                foreach (var other in contents)
                    if (other > vertex && (closed[other] - closed[vertex]).sqrMagnitude <= radiusSquared)
                        pairs.Add((vertex, other));
            }
        }
        return pairs;
    }

    // 変形量の一定以上が基準範囲に集中する候補だけを残す。
    private const float MinimumRegionFraction = 0.99f;
    // リップシンクでは平均移動を除いた残差、まばたきでは近傍ペアの移動差を評価する。
    private const float MinimumResidualFraction = 0.02f;
    private const float MinimumPairDifferenceFraction = 0.02f;

    private static IReadOnlyList<string>? FindConflicts(
        Mesh mesh, bool[] region, ISet<string> references, ISet<string> unavailableNames,
        IReadOnlyList<(int First, int Second)>? pairs = null, int[]? pairDegrees = null)
    {
        var regionIndices = Enumerable.Range(0, region.Length).Where(vertex => region[vertex]).ToArray();
        if (regionIndices.Length == 0) return null;
        var deltas = new Vector3[mesh.vertexCount];
        var normals = new Vector3[mesh.vertexCount];
        var tangents = new Vector3[mesh.vertexCount];
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
                var totalEnergy = 0f;
                foreach (var delta in deltas) totalEnergy += delta.sqrMagnitude;
                if (totalEnergy <= 0f) continue;
                var regionEnergy = 0f;
                var pairEnergy = 0f;
                foreach (var vertex in regionIndices)
                {
                    var energy = deltas[vertex].sqrMagnitude;
                    regionEnergy += energy;
                    if (pairDegrees != null) pairEnergy += pairDegrees[vertex] * energy;
                }
                if (regionEnergy / totalEnergy < MinimumRegionFraction) continue;
                var hasChange = pairs == null
                    ? HasRegionalChange(deltas, regionIndices, regionEnergy)
                    : HasNearbyChange(deltas, pairs, pairEnergy);
                if (!hasChange) continue;
                result.Add(name);
                break;
            }
        }
        return result;
    }

    private static bool HasRegionalChange(Vector3[] deltas, int[] regionIndices, float regionEnergy)
    {
        var motion = Vector3.zero;
        foreach (var vertex in regionIndices) motion += deltas[vertex];
        var residual = Mathf.Max(0f, regionEnergy - motion.sqrMagnitude / regionIndices.Length);
        return residual / regionEnergy >= MinimumResidualFraction;
    }

    private static bool HasNearbyChange(
        Vector3[] deltas, IReadOnlyList<(int First, int Second)> pairs, float pairEnergy)
    {
        if (pairEnergy <= 0f) return false;
        var threshold = pairEnergy * MinimumPairDifferenceFraction;
        var differenceEnergy = 0f;
        foreach (var (first, second) in pairs)
        {
            differenceEnergy += (deltas[first] - deltas[second]).sqrMagnitude;
            if (differenceEnergy >= threshold) return true;
        }
        return false;
    }

    private static Vector3Int Cell(Vector3 position, float size) => new(
        Mathf.FloorToInt(position.x / size),
        Mathf.FloorToInt(position.y / size),
        Mathf.FloorToInt(position.z / size));
}
