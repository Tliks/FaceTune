using Aoyon.FaceTune.Build;
using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static class CustomLipSyncBlendShapeProxy
{
    private const string ProxyNamePrefix = "FaceTune__CustomLipSync__";

    internal readonly record struct Result(
        BuildSettings Settings,
        ExpressionPlan Expressions,
        ImmutableHashSet<string> ProxyNames);

    public static Result Apply(
        BuildContext buildContext,
        BuildSettings settings,
        ExpressionPlan expressions,
        ISet<string> builtInLipSyncBlendShapes)
    {
        using var _ = new Utils.ProfilingSampleScope(
            "Animator.ResolveCustomLipSyncProxy");
        var usedNames = expressions.Items
            .Select(item => item.LipSync)
            .Where(lipSync => lipSync.Mode == LipSyncSettings.Kind.Custom)
            .SelectMany(lipSync => GetVisemeShapes(lipSync.Shapes))
            .SelectMany(shapes => shapes)
            .Select(shape => shape.Name)
            .Where(builtInLipSyncBlendShapes.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (usedNames.Length == 0)
            return new Result(settings, expressions, ImmutableHashSet<string>.Empty);

        var sourceMesh = settings.AvatarContext.FaceMesh;
        var mesh = Object.Instantiate(sourceMesh);
        mesh.name = $"{sourceMesh.name} (FaceTune Custom LipSync)";
        var existingNames = Enumerable.Range(0, mesh.blendShapeCount)
            .Select(mesh.GetBlendShapeName)
            .ToHashSet(StringComparer.Ordinal);
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);

        using (new Utils.ProfilingSampleScope(
                   "Animator.CustomLipSyncProxy.DuplicateBlendShapes"))
        {
            foreach (var sourceName in usedNames)
            {
                var sourceIndex = sourceMesh.GetBlendShapeIndex(sourceName);
                if (sourceIndex < 0) continue;

                var proxyName = CreateProxyName(sourceName, existingNames);
                DuplicateBlendShape(sourceMesh, sourceIndex, mesh, proxyName);
                existingNames.Add(proxyName);
                mapping.Add(sourceName, proxyName);
            }
        }

        if (mapping.Count == 0)
        {
            Object.DestroyImmediate(mesh);
            return new Result(settings, expressions, ImmutableHashSet<string>.Empty);
        }

        using (new Utils.ProfilingSampleScope(
                   "Animator.CustomLipSyncProxy.RewritePlan"))
        {
            settings.AvatarContext.FaceRenderer.sharedMesh = mesh;

            var avatarContext = settings.AvatarContext with { FaceMesh = mesh };
            var rewrittenSettings = settings with { AvatarContext = avatarContext };
            var rewrittenExpressions = new ExpressionPlan(expressions.Items.Select(item =>
                item.LipSync.Mode == LipSyncSettings.Kind.Custom
                    ? item with { LipSync = Rewrite(item.LipSync, mapping) }
                    : item));
            return new Result(
                rewrittenSettings,
                rewrittenExpressions,
                mapping.Values.ToImmutableHashSet(StringComparer.Ordinal));
        }
    }

    private static string CreateProxyName(string sourceName, ISet<string> existingNames)
    {
        var baseName = ProxyNamePrefix + sourceName;
        var name = baseName;
        for (var suffix = 1; existingNames.Contains(name); suffix++)
            name = $"{baseName}_{suffix}";
        return name;
    }

    private static void DuplicateBlendShape(
        Mesh source,
        int sourceIndex,
        Mesh destination,
        string destinationName)
    {
        var deltaVertices = new Vector3[source.vertexCount];
        var deltaNormals = new Vector3[source.vertexCount];
        var deltaTangents = new Vector3[source.vertexCount];
        for (var frame = 0; frame < source.GetBlendShapeFrameCount(sourceIndex); frame++)
        {
            source.GetBlendShapeFrameVertices(
                sourceIndex,
                frame,
                deltaVertices,
                deltaNormals,
                deltaTangents);
            destination.AddBlendShapeFrame(
                destinationName,
                source.GetBlendShapeFrameWeight(sourceIndex, frame),
                deltaVertices,
                deltaNormals,
                deltaTangents);
        }
    }

    private static LipSyncSettings Rewrite(
        LipSyncSettings source,
        IReadOnlyDictionary<string, string> mapping)
        => new()
        {
            Mode = source.Mode,
            CancellerBlendShapes = source.CancellerBlendShapes.ToList(),
            Shapes = new VrcVisemeLipSyncShapes
            {
                Sil = Rewrite(source.Shapes.Sil, mapping),
                PP = Rewrite(source.Shapes.PP, mapping),
                FF = Rewrite(source.Shapes.FF, mapping),
                TH = Rewrite(source.Shapes.TH, mapping),
                DD = Rewrite(source.Shapes.DD, mapping),
                KK = Rewrite(source.Shapes.KK, mapping),
                CH = Rewrite(source.Shapes.CH, mapping),
                SS = Rewrite(source.Shapes.SS, mapping),
                NN = Rewrite(source.Shapes.NN, mapping),
                RR = Rewrite(source.Shapes.RR, mapping),
                AA = Rewrite(source.Shapes.AA, mapping),
                E = Rewrite(source.Shapes.E, mapping),
                IH = Rewrite(source.Shapes.IH, mapping),
                OH = Rewrite(source.Shapes.OH, mapping),
                OU = Rewrite(source.Shapes.OU, mapping)
            }
        };

    private static List<BlendShapeWeight> Rewrite(
        IEnumerable<BlendShapeWeight> shapes,
        IReadOnlyDictionary<string, string> mapping)
        => shapes
            .Select(shape => mapping.TryGetValue(shape.Name, out var proxyName)
                ? shape with { Name = proxyName }
                : shape)
            .ToList();

    private static IEnumerable<IReadOnlyList<BlendShapeWeight>> GetVisemeShapes(
        VrcVisemeLipSyncShapes shapes)
    {
        yield return shapes.Sil;
        yield return shapes.PP;
        yield return shapes.FF;
        yield return shapes.TH;
        yield return shapes.DD;
        yield return shapes.KK;
        yield return shapes.CH;
        yield return shapes.SS;
        yield return shapes.NN;
        yield return shapes.RR;
        yield return shapes.AA;
        yield return shapes.E;
        yield return shapes.IH;
        yield return shapes.OH;
        yield return shapes.OU;
    }
}
