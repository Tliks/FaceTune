namespace Aoyon.FaceTune.Gui;

/// <summary>Suggests shapes that affect the region controlled by blink or lip sync.</summary>
internal static class ConflictShapeDetector
{
    private const float RegionThreshold = 0.0000001f;
    private const float MinimumRmsMotion = 0.00001f;
    private const float MinimumRegionFraction = 0.03f;
    private const float TranslationCoverage = 0.9f;
    private const float MaximumTranslationVariance = 0.01f;

    private readonly record struct ClosurePair(int A, int B);

    // Null means the reference shapes could not define a usable region; do not overwrite the list.
    public static IReadOnlyList<string>? DetectBlink(
        Mesh mesh,
        IReadOnlyCollection<BlendShapeWeight> blinkShapes,
        ISet<string> unavailableNames)
    {
        if (!mesh.isReadable || mesh.vertexCount == 0) return null;
        var count = mesh.vertexCount;
        var vertices = mesh.vertices;
        var region = new bool[count];
        var deltas = new Vector3[count];
        var next = new Vector3[count];
        var normals = new Vector3[count];
        var tangents = new Vector3[count];
        var closed = new Vector3[count];
        var neighbors = BuildNeighbors(mesh, count);
        var magnitude = Mathf.Max(mesh.bounds.size.magnitude, 0.001f);
        var sourceNames = blinkShapes.Select(shape => shape.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var shape in blinkShapes)
        {
            var index = mesh.GetBlendShapeIndex(shape.Name);
            if (index < 0 || Mathf.Approximately(shape.Weight, 0f)
                || !ReadBlinkDelta(mesh, index, shape.Weight, deltas, next, normals, tangents))
                continue;
            var maximumMotion = 0f;
            for (var vertex = 0; vertex < count; vertex++)
            {
                closed[vertex] = vertices[vertex] + deltas[vertex];
                maximumMotion = Mathf.Max(maximumMotion, deltas[vertex].magnitude);
            }
            var regionThreshold2 = Mathf.Pow(Mathf.Max(
                maximumMotion * 0.01f, magnitude * 0.00001f), 2f);
            for (var vertex = 0; vertex < count; vertex++)
                if (deltas[vertex].sqrMagnitude > regionThreshold2) region[vertex] = true;
            var radius = Mathf.Max(magnitude * 0.0005f,
                Mathf.Min(maximumMotion * 0.35f,
                    Mathf.Max(magnitude * 0.008f, maximumMotion * 0.25f)));
            if (maximumMotion < radius * 1.5f) continue;

            // Closing counterparts may not move at all; include them in the region.
            var cells = new Dictionary<Vector3Int, List<int>>();
            for (var vertex = 0; vertex < count; vertex++)
            {
                var cell = Cell(closed[vertex], radius);
                if (!cells.TryGetValue(cell, out var contents))
                    cells[cell] = contents = new List<int>();
                contents.Add(vertex);
            }
            var pairs = new List<ClosurePair>();
            var movementThreshold2 = Mathf.Pow(magnitude * 0.000001f, 2f);
            for (var vertex = 0; vertex < count; vertex++)
            {
                if ((closed[vertex] - vertices[vertex]).sqrMagnitude <= movementThreshold2)
                    continue;
                var cell = Cell(closed[vertex], radius);
                for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                for (var z = -1; z <= 1; z++)
                {
                    if (!cells.TryGetValue(cell + new Vector3Int(x, y, z), out var contents))
                        continue;
                    foreach (var other in contents)
                    {
                        if (other == vertex || (other < vertex
                            && (closed[other] - vertices[other]).sqrMagnitude > movementThreshold2))
                            continue;
                        if (Array.IndexOf(neighbors[vertex], other) >= 0) continue;
                        var closedDistance = (closed[vertex] - closed[other]).magnitude;
                        if (closedDistance > radius) continue;
                        var openDistance = (vertices[vertex] - vertices[other]).magnitude;
                        var reduction = openDistance - closedDistance;
                        if (openDistance < radius * 1.5f
                            || closedDistance > openDistance * 0.55f
                            || reduction < radius * 0.75f) continue;
                        pairs.Add(new ClosurePair(vertex, other));
                    }
                }
            }
            if (pairs.Count < 4) continue;
            foreach (var pair in pairs)
            {
                region[pair.A] = true;
                region[pair.B] = true;
            }
        }

        if (region.Count(value => value) < 4) return null;
        return DetectConflicts(mesh, region, sourceNames, unavailableNames, true, blink: true);
    }

    public static IReadOnlyList<string>? Detect(
        Mesh mesh,
        IReadOnlyCollection<string> referenceNames,
        ISet<string> unavailableNames)
    {
        if (!mesh.isReadable || mesh.vertexCount == 0) return null;
        var count = mesh.vertexCount;
        var region = new bool[count];
        var deltas = new Vector3[count];
        var normals = new Vector3[count];
        var tangents = new Vector3[count];
        var magnitude = Mathf.Max(mesh.bounds.size.magnitude, 0.001f);
        var threshold2 = Mathf.Pow(magnitude * RegionThreshold, 2f);
        var references = referenceNames.ToHashSet(StringComparer.Ordinal);
        foreach (var name in references)
        {
            var index = mesh.GetBlendShapeIndex(name);
            if (index < 0) continue;
            for (var frame = 0; frame < mesh.GetBlendShapeFrameCount(index); frame++)
            {
                var weight = Mathf.Abs(mesh.GetBlendShapeFrameWeight(index, frame));
                if (weight < 0.001f) continue;
                mesh.GetBlendShapeFrameVertices(index, frame, deltas, normals, tangents);
                var frameThreshold2 = threshold2 * weight * weight / 10000f;
                for (var vertex = 0; vertex < count; vertex++)
                    if (deltas[vertex].sqrMagnitude > frameThreshold2) region[vertex] = true;
            }
        }
        if (!ExpandRegion(region, BuildNeighbors(mesh, count))) return null;
        return DetectConflicts(mesh, region, references, unavailableNames, true);
    }

    private static bool ExpandRegion(bool[] region, int[][] neighbors)
    {
        var original = new List<int>();
        for (var i = 0; i < region.Length; i++)
            if (region[i]) original.Add(i);
        if (original.Count < 4) return false;
        // One hop includes the transition into stationary lower lids / mouth corners.
        foreach (var vertex in original)
            foreach (var adjacent in neighbors[vertex]) region[adjacent] = true;
        return true;
    }

    private static IReadOnlyList<string> DetectConflicts(
        Mesh mesh, bool[] region, ISet<string> references,
        ISet<string> unavailableNames, bool canExcludeTranslation, bool blink = false)
    {
        var count = mesh.vertexCount;
        var regionIndices = Enumerable.Range(0, count).Where(i => region[i]).ToArray();
        var regionCount = regionIndices.Length;
        var deltas = new Vector3[count];
        var normals = new Vector3[count];
        var tangents = new Vector3[count];
        var magnitude = Mathf.Max(mesh.bounds.size.magnitude, 0.001f);
        var minimumEnergy = regionCount * Mathf.Pow(magnitude * MinimumRmsMotion, 2f);
        var result = new List<string>();
        for (var index = 0; index < mesh.blendShapeCount; index++)
        {
            var name = mesh.GetBlendShapeName(index);
            if (references.Contains(name) || unavailableNames.Contains(name)) continue;
            for (var frame = 0; frame < mesh.GetBlendShapeFrameCount(index); frame++)
            {
                var weight = Mathf.Abs(mesh.GetBlendShapeFrameWeight(index, frame));
                if (weight < 0.001f) continue;
                mesh.GetBlendShapeFrameVertices(index, frame, deltas, normals, tangents);
                var energy = 0f;
                var maximum = 0f;
                var sum = Vector3.zero;
                foreach (var vertex in regionIndices)
                {
                    var delta = deltas[vertex];
                    energy += delta.sqrMagnitude;
                    maximum = Mathf.Max(maximum, delta.sqrMagnitude);
                    sum += delta;
                }
                var scale = 100f / weight;
                if (energy * scale * scale < minimumEnergy) continue;
                var totalEnergy = 0f;
                foreach (var delta in deltas) totalEnergy += delta.sqrMagnitude;
                if (energy / totalEnergy < MinimumRegionFraction) continue;

                if (blink)
                {
                    var residual = Mathf.Max(0f, energy - sum.sqrMagnitude / regionCount);
                    if (residual / energy <= MaximumTranslationVariance
                        || residual * scale * scale < minimumEnergy) continue;
                }
                else if (canExcludeTranslation)
                {
                    var moving = 0;
                    var threshold2 = Mathf.Max(maximum * 0.0025f,
                        Mathf.Pow(magnitude * 0.000001f * weight / 100f, 2f));
                    foreach (var vertex in regionIndices)
                        if (deltas[vertex].sqrMagnitude > threshold2) moving++;
                    var variance = Mathf.Max(0f,
                        energy - sum.sqrMagnitude / regionCount);
                    if (moving >= regionCount * TranslationCoverage
                        && variance / energy <= MaximumTranslationVariance) continue;
                }
                result.Add(name);
                break;
            }
        }
        return result;
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
            if (Mathf.Abs(frameWeight) < 0.001f) return false;
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

    private static int[][] BuildNeighbors(Mesh mesh, int count)
    {
        var lists = new List<int>?[count];
        void Add(int from, int to)
        {
            (lists[from] ??= new List<int>()).Add(to);
        }
        for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
        {
            if (mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;
            var triangles = mesh.GetTriangles(submesh);
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];
                Add(a, b); Add(b, a);
                Add(b, c); Add(c, b);
                Add(c, a); Add(a, c);
            }
        }
        var neighbors = new int[count][];
        for (var vertex = 0; vertex < count; vertex++)
            neighbors[vertex] = lists[vertex]?.Distinct().ToArray() ?? Array.Empty<int>();
        return neighbors;
    }
}
