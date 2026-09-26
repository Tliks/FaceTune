namespace Aoyon.FaceTune.Gui;

/// <summary>Finds shapes which change how a blink or lip-sync reference moves the mesh.</summary>
internal static class ConflictShapeDetector
{
    private const float RegionThreshold = 0.0000001f;
    private const float MinimumConcentration = 0.3f;
    private const float MinimumInterference = 0.01f;
    private const float Regularization = 0.0001f;

    private readonly record struct ClosurePair(int A, int B);
    private readonly record struct BlinkBoundary(ClosurePair[] Pairs, float ClosingEnergy);

    private readonly record struct RawReference(int[] Indices, Vector3[] Deltas);
    private readonly record struct ReferenceVertex(int Index, float[] EdgeWeights);
    private readonly record struct ReferenceFrame(ReferenceVertex[] Vertices, float Energy);

    private readonly record struct SymmetricInverse(
        float XX, float XY, float XZ, float YY, float YZ, float ZZ)
    {
        public Vector3 Multiply(Vector3 value) => new(
            XX * value.x + XY * value.y + XZ * value.z,
            XY * value.x + YY * value.y + YZ * value.z,
            XZ * value.x + YZ * value.y + ZZ * value.z);
    }

    // Null means the reference shapes could not define a usable region; do not overwrite the list.
    public static IReadOnlyList<string>? DetectBlink(
        Mesh mesh,
        IReadOnlyCollection<BlendShapeWeight> blinkShapes,
        ISet<string> unavailableNames)
    {
        if (!mesh.isReadable || mesh.vertexCount == 0) return null;
        var vertices = mesh.vertices;
        var count = vertices.Length;
        var deltas = new Vector3[count];
        var nextFrameDeltas = new Vector3[count];
        var normals = new Vector3[count];
        var tangents = new Vector3[count];
        var closed = new Vector3[count];
        var neighbors = BuildNeighbors(mesh, count);
        // Cache each endpoint's 0/1/2-hop patch; closure pairs often share endpoints.
        var neighborhoods = new Dictionary<int, int[]>();
        int[] Neighborhood(int vertex)
        {
            if (neighborhoods.TryGetValue(vertex, out var existing)) return existing;
            var verticesInPatch = new HashSet<int> { vertex };
            foreach (var adjacent in neighbors[vertex])
            {
                verticesInPatch.Add(adjacent);
                foreach (var secondHop in neighbors[adjacent])
                    verticesInPatch.Add(secondHop);
            }
            return neighborhoods[vertex] = verticesInPatch.ToArray();
        }
        var magnitude = Mathf.Max(mesh.bounds.size.magnitude, 0.001f);
        var boundaries = new List<BlinkBoundary>();
        var sourceNames = blinkShapes.Select(shape => shape.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var shape in blinkShapes)
        {
            var index = mesh.GetBlendShapeIndex(shape.Name);
            if (index < 0 || Mathf.Approximately(shape.Weight, 0f)) continue;
            if (!ReadBlinkDelta(mesh, index, shape.Weight,
                    deltas, nextFrameDeltas, normals, tangents)) continue;
            var maximumMotion = 0f;
            for (var vertex = 0; vertex < count; vertex++)
            {
                closed[vertex] = vertices[vertex] + deltas[vertex];
                maximumMotion = Mathf.Max(maximumMotion, deltas[vertex].magnitude);
            }
            var radius = Mathf.Max(magnitude * 0.0005f,
                Mathf.Min(maximumMotion * 0.35f,
                    Mathf.Max(magnitude * 0.008f, maximumMotion * 0.25f)));
            if (maximumMotion < radius * 1.5f) continue;

            // Index the closed pose once per blink. Only moved vertices initiate
            // queries, but their counterpart may be a stationary lower eyelid.
            var cells = new Dictionary<Vector3Int, List<int>>();
            for (var vertex = 0; vertex < count; vertex++)
            {
                var cell = Cell(closed[vertex], radius);
                if (!cells.TryGetValue(cell, out var contents))
                    cells[cell] = contents = new List<int>();
                contents.Add(vertex);
            }
            var pairs = new List<ClosurePair>();
            var closingEnergy = 0f;
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
                        // A shrinking edge on one surface is not a closing gap.
                        if (Array.IndexOf(neighbors[vertex], other) >= 0) continue;
                        var separation = closed[vertex] - closed[other];
                        var closedDistance = separation.magnitude;
                        if (closedDistance > radius) continue;
                        var openDistance = (vertices[vertex] - vertices[other]).magnitude;
                        var reduction = openDistance - closedDistance;
                        if (openDistance < radius * 1.5f
                            || closedDistance > openDistance * 0.55f
                            || reduction < radius * 0.75f) continue;
                        pairs.Add(new ClosurePair(vertex, other));
                        closingEnergy += reduction * reduction;
                    }
                }
            }
            if (pairs.Count < 4) continue;
            foreach (var pair in pairs)
            {
                Neighborhood(pair.A);
                Neighborhood(pair.B);
            }
            boundaries.Add(new BlinkBoundary(pairs.ToArray(), closingEnergy));
        }
        if (boundaries.Count == 0) return null;

        var neighborhoodMotion = new Vector3[count];
        var result = new List<string>();
        for (var index = 0; index < mesh.blendShapeCount; index++)
        {
            var name = mesh.GetBlendShapeName(index);
            if (sourceNames.Contains(name) || unavailableNames.Contains(name)) continue;
            for (var frame = 0; frame < mesh.GetBlendShapeFrameCount(index); frame++)
            {
                var frameWeight = Mathf.Abs(mesh.GetBlendShapeFrameWeight(index, frame));
                if (frameWeight < 0.001f) continue;
                mesh.GetBlendShapeFrameVertices(index, frame, deltas, normals, tangents);
                // One mean per unique endpoint, reused by all closure pairs.
                foreach (var entry in neighborhoods)
                {
                    var sum = Vector3.zero;
                    foreach (var vertex in entry.Value) sum += deltas[vertex];
                    neighborhoodMotion[entry.Key] = sum / entry.Value.Length;
                }
                var scale = 100f / mesh.GetBlendShapeFrameWeight(index, frame);
                var detected = false;
                foreach (var boundary in boundaries)
                {
                    var interference = 0f;
                    foreach (var pair in boundary.Pairs)
                    {
                        var relativeChange = (neighborhoodMotion[pair.A]
                            - neighborhoodMotion[pair.B]) * scale;
                        interference += relativeChange.sqrMagnitude;
                    }
                    if (interference / boundary.ClosingEnergy < MinimumInterference) continue;
                    detected = true;
                    break;
                }
                if (!detected) continue;
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

    // Lip sync continues to use neighborhood deformation rather than eye-closure pairs.
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
        var rawReferences = new List<RawReference>();
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
                var threshold2 = referenceThreshold2 * weight * weight / 10000f;
                var indices = new List<int>();
                var motions = new List<Vector3>();
                for (var vertex = 0; vertex < count; vertex++)
                {
                    if (deltas[vertex].sqrMagnitude <= threshold2) continue;
                    region[vertex] = true;
                    indices.Add(vertex);
                    motions.Add(deltas[vertex]);
                }
                if (indices.Count > 0)
                    rawReferences.Add(new RawReference(indices.ToArray(), motions.ToArray()));
            }
        }
        if (region.Count(selected => selected) < 4) return null;

        var neighbors = BuildNeighbors(mesh, count);
        var inverses = new SymmetricInverse[count];
        var usable = new bool[count];
        for (var vertex = 0; vertex < count; vertex++)
            if (region[vertex])
                usable[vertex] = TryInverseNeighborhood(
                    vertices, vertex, neighbors[vertex], out inverses[vertex]);

        // With C = sum edge edge^T, A = (sum edge' edge^T + lambda I)
        // (C + lambda I)^-1. Since edge' = edge + (candidateDelta[neighbor]
        // - candidateDelta[vertex]), (A - I) * referenceDelta is a weighted
        // sum of those candidate delta differences.
        // Precompute the weights for each reference vertex, leaving no matrix solve or
        // per-frame allocation in the candidate loop.
        var referenceFrames = new List<ReferenceFrame>();
        foreach (var raw in rawReferences)
        {
            var active = new List<ReferenceVertex>();
            var energy = 0f;
            for (var i = 0; i < raw.Indices.Length; i++)
            {
                var vertex = raw.Indices[i];
                if (!usable[vertex]) continue;
                var transformed = inverses[vertex].Multiply(raw.Deltas[i]);
                var adjacent = neighbors[vertex];
                var edgeWeights = new float[adjacent.Length];
                for (var j = 0; j < adjacent.Length; j++)
                    edgeWeights[j] = Vector3.Dot(
                        vertices[adjacent[j]] - vertices[vertex], transformed);
                var motionEnergy = raw.Deltas[i].sqrMagnitude;
                active.Add(new ReferenceVertex(vertex, edgeWeights));
                energy += motionEnergy;
            }
            if (energy > 0f) referenceFrames.Add(new ReferenceFrame(active.ToArray(), energy));
        }
        if (referenceFrames.Count == 0) return null;

        var result = new List<string>();
        for (var index = 0; index < mesh.blendShapeCount; index++)
        {
            var name = mesh.GetBlendShapeName(index);
            if (references.Contains(name) || unavailableNames.Contains(name)) continue;
            for (var frame = 0; frame < mesh.GetBlendShapeFrameCount(index); frame++)
            {
                var frameWeight = Mathf.Abs(mesh.GetBlendShapeFrameWeight(index, frame));
                if (frameWeight < 0.001f) continue;
                mesh.GetBlendShapeFrameVertices(index, frame, deltas, normals, tangents);
                var total = 0f;
                var local = 0f;
                for (var vertex = 0; vertex < count; vertex++)
                {
                    var motionEnergy = deltas[vertex].sqrMagnitude;
                    total += motionEnergy;
                    if (region[vertex]) local += motionEnergy;
                }
                if (total < 1e-12f || local / total < MinimumConcentration) continue;

                // Frames may be authored at weights other than 100. Compare their
                // full-strength deformations; local / total is scale-invariant.
                var candidateScale = 100f / frameWeight;
                var detected = false;
                foreach (var reference in referenceFrames)
                {
                    var interference = 0f;
                    foreach (var blink in reference.Vertices)
                    {
                        var displacement = Vector3.zero;
                        var adjacent = neighbors[blink.Index];
                        for (var j = 0; j < adjacent.Length; j++)
                            displacement += blink.EdgeWeights[j]
                                * (deltas[adjacent[j]] - deltas[blink.Index]);
                        interference += displacement.sqrMagnitude;
                    }
                    if (interference * candidateScale * candidateScale / reference.Energy
                        < MinimumInterference) continue;
                    detected = true;
                    break;
                }
                if (!detected) continue;
                result.Add(name);
                break;
            }
        }
        return result;
    }

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

    private static bool TryInverseNeighborhood(
        Vector3[] vertices, int index, int[] neighbors, out SymmetricInverse inverse)
    {
        inverse = default;
        if (neighbors.Length < 2) return false;
        double xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
        foreach (var neighbor in neighbors)
        {
            var edge = vertices[neighbor] - vertices[index];
            xx += (double)edge.x * edge.x;
            xy += (double)edge.x * edge.y;
            xz += (double)edge.x * edge.z;
            yy += (double)edge.y * edge.y;
            yz += (double)edge.y * edge.z;
            zz += (double)edge.z * edge.z;
        }
        var trace = xx + yy + zz;
        if (trace < 1e-20) return false;
        // Flat or nearly collinear neighborhoods lack a unique 3D inverse. Bias the
        // unconstrained directions towards identity instead of amplifying mesh noise.
        var damping = trace * Regularization / 3.0;
        xx += damping;
        yy += damping;
        zz += damping;
        var cofactorXX = yy * zz - yz * yz;
        var cofactorXY = xz * yz - xy * zz;
        var cofactorXZ = xy * yz - xz * yy;
        var cofactorYY = xx * zz - xz * xz;
        var cofactorYZ = xy * xz - xx * yz;
        var cofactorZZ = xx * yy - xy * xy;
        var determinant = xx * cofactorXX + xy * cofactorXY + xz * cofactorXZ;
        if (determinant <= 0 || double.IsNaN(determinant)) return false;
        var scale = 1.0 / determinant;
        inverse = new SymmetricInverse(
            (float)(cofactorXX * scale), (float)(cofactorXY * scale),
            (float)(cofactorXZ * scale), (float)(cofactorYY * scale),
            (float)(cofactorYZ * scale), (float)(cofactorZZ * scale));
        return true;
    }
}
