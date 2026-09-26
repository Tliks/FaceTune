namespace Aoyon.FaceTune.Gui;

/// <summary>Finds shapes which deform (rather than rigidly move) a reference shape's region.</summary>
internal static class ConflictShapeDetector
{
    private const float RegionThreshold = 0.0002f;
    private const float MinimumConcentration = 0.3f;
    private const float MovingVertexFraction = 0.002f;
    private const float MinimumShapeChangeFraction = 0.1f;
    private const int AnchorCount = 12;

    // Null means the reference shapes could not define a usable region; do not overwrite the list.
    public static IReadOnlyList<string>? Detect(
        Mesh mesh,
        IReadOnlyCollection<string> referenceNames,
        ISet<string> unavailableNames)
    {
        if (!mesh.isReadable || mesh.vertexCount == 0) return null;
        var vertices = mesh.vertices;
        var count = vertices.Length;
        var deltas = new Vector3[count];
        var normals = new Vector3[count];
        var tangents = new Vector3[count];
        var magnitude = Mathf.Max(mesh.bounds.size.magnitude, 0.001f);
        var referenceThreshold2 = Mathf.Pow(magnitude * RegionThreshold, 2f);
        var region = new bool[count];
        var references = referenceNames.ToHashSet(StringComparer.Ordinal);
        foreach (var name in references)
        {
            var index = mesh.GetBlendShapeIndex(name);
            if (index < 0) continue;
            for (var frame = 0; frame < mesh.GetBlendShapeFrameCount(index); frame++)
            {
                mesh.GetBlendShapeFrameVertices(index, frame, deltas, normals, tangents);
                var weight = Mathf.Abs(mesh.GetBlendShapeFrameWeight(index, frame));
                if (weight < 0.001f) continue;
                var threshold2 = referenceThreshold2 * weight * weight / 10000f;
                for (var vertex = 0; vertex < count; vertex++)
                    if (deltas[vertex].sqrMagnitude > threshold2) region[vertex] = true;
            }
        }

        var regionIndices = Enumerable.Range(0, count).Where(i => region[i]).ToArray();
        if (regionIndices.Length < 4) return null;
        var moving = new int[regionIndices.Length];
        var anchors = new int[AnchorCount];
        var result = new List<string>();
        for (var index = 0; index < mesh.blendShapeCount; index++)
        {
            var name = mesh.GetBlendShapeName(index);
            if (references.Contains(name) || unavailableNames.Contains(name)) continue;
            // Evaluate frames separately: combining per-vertex maxima would invent a non-existent pose.
            for (var frame = 0; frame < mesh.GetBlendShapeFrameCount(index); frame++)
            {
                var weight = Mathf.Abs(mesh.GetBlendShapeFrameWeight(index, frame));
                if (weight < 0.001f) continue;
                mesh.GetBlendShapeFrameVertices(index, frame, deltas, normals, tangents);
                var total = 0f;
                var local = 0f;
                var maximumLocal = 0f;
                for (var vertex = 0; vertex < count; vertex++)
                {
                    var energy = deltas[vertex].sqrMagnitude;
                    total += energy;
                    if (!region[vertex]) continue;
                    local += energy;
                    maximumLocal = Mathf.Max(maximumLocal, energy);
                }
                if (total < 1e-12f || local / total < MinimumConcentration) continue;

                // Exclude numerical noise, not the weakly moving boundary of a deformation.
                var noise = Mathf.Max(magnitude * 0.000001f * weight / 100f,
                    Mathf.Sqrt(maximumLocal) * MovingVertexFraction);
                var noise2 = noise * noise;
                var movingCount = 0;
                foreach (var vertex in regionIndices)
                    if (deltas[vertex].sqrMagnitude > noise2) moving[movingCount++] = vertex;
                if (movingCount < 3) continue;

                // Compare only points moved by this candidate. An unmoved reference point
                // would turn a rigid translation of a local patch into a false deformation.
                var anchorCount = ChooseAnchors(vertices, moving, movingCount, anchors);
                if (anchorCount < 2) continue;
                var shapeChange = 0f;
                var relativeMotion = 0f;
                var pairCount = 0;
                for (var i = 0; i < movingCount; i++)
                {
                    var vertex = moving[i];
                    for (var j = 0; j < anchorCount; j++)
                    {
                        var anchor = anchors[j];
                        if (anchor == vertex) continue;
                        var before = vertices[vertex] - vertices[anchor];
                        var motion = deltas[vertex] - deltas[anchor];
                        var change = (before + motion).magnitude - before.magnitude;
                        shapeChange += change * change;
                        relativeMotion += motion.sqrMagnitude;
                        pairCount++;
                    }
                }
                var minimumChange = magnitude * 0.00005f * weight / 100f;
                if (relativeMotion <= 1e-20f
                    || shapeChange < pairCount * minimumChange * minimumChange
                    || shapeChange / relativeMotion < MinimumShapeChangeFraction) continue;
                result.Add(name);
                break;
            }
        }
        return result;
    }

    // Farthest-point sampling spreads a few references over the moving patch. Static
    // vertices never qualify as anchors, and the low noise gate retains its taper.
    private static int ChooseAnchors(Vector3[] vertices, int[] moving, int count, int[] anchors)
    {
        anchors[0] = moving[0];
        var selected = 1;
        while (selected < Mathf.Min(anchors.Length, count))
        {
            var best = -1;
            var farthest = 0f;
            for (var i = 0; i < count; i++)
            {
                var vertex = moving[i];
                var distance = float.PositiveInfinity;
                for (var j = 0; j < selected; j++)
                    distance = Mathf.Min(distance,
                        (vertices[vertex] - vertices[anchors[j]]).sqrMagnitude);
                if (distance <= farthest) continue;
                farthest = distance;
                best = vertex;
            }
            if (best < 0) break;
            anchors[selected++] = best;
        }
        return selected;
    }
}
