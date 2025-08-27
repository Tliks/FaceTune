namespace Aoyon.FaceTune;

internal static class BlendShapeUtility
{
    public static void GetBlendShapes(this SkinnedMeshRenderer renderer, ICollection<BlendShapeWeight> resultToAdd)
    {
        for (var i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
        {
            var name = renderer.sharedMesh.GetBlendShapeName(i);
            var weight = renderer.GetBlendShapeWeight(i);
            resultToAdd.Add(new BlendShapeWeight(name, weight));
        }
    }
    
    public static void GetBlendShapesAndSetWeightToZero(this SkinnedMeshRenderer renderer, ICollection<BlendShapeWeight> resultToAdd)
    {
        for (var i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
        {
            var name = renderer.sharedMesh.GetBlendShapeName(i);
            resultToAdd.Add(new BlendShapeWeight(name, 0f));
        }
    }

    public static BlendShapeWeight[] GetBlendShapes(this SkinnedMeshRenderer renderer, Mesh mesh)
    {
        var blendShapes = new BlendShapeWeight[mesh.blendShapeCount];
        for (var i = 0; i < mesh.blendShapeCount; i++)
        {
            var name = mesh.GetBlendShapeName(i);
            var weight = renderer.GetBlendShapeWeight(i);
            blendShapes[i] = new BlendShapeWeight(name, weight);
        }
        return blendShapes;
    }

    /// <summary>
    /// ブレンドシェイプを適用する
    /// defaultValueはblendShapeSetに含まれないブレンドシェイプのハンドリング
    /// -1のとき維持し、それ以外の場合は指定された値で上書き
    /// </summary>
    public static void ApplyBlendShapes(this SkinnedMeshRenderer renderer, Mesh mesh, BlendShapeSet blendShapeSet, float defaultValue = -1, bool record = false)
    {
#if UNITY_EDITOR
        if (record) UnityEditor.Undo.RecordObject(renderer, "Apply Blend Shape");
#endif
        var blendShapeCount = mesh.blendShapeCount;
        for (var i = 0; i < blendShapeCount; i++)
        {
            var name = mesh.GetBlendShapeName(i);
            var currentWeight = renderer.GetBlendShapeWeight(i);
            if (blendShapeSet.TryGetValue(name, out var blendShape))
            {
                if (blendShape.Weight == currentWeight) continue; // 余分な変更を避ける
                renderer.SetBlendShapeWeight(i, blendShape.Weight);
            }
            else if (defaultValue != -1)
            {
                if (currentWeight == defaultValue) continue;
                renderer.SetBlendShapeWeight(i, defaultValue);
            }
        }
    }
    
    public static BlendShapeWeightAnimation ToBlendShapeAnimation(this BlendShapeWeight blendShape)
    {
        return BlendShapeWeightAnimation.SingleFrame(blendShape.Name, blendShape.Weight);
    }

    public static IEnumerable<BlendShapeWeightAnimation> ToBlendShapeAnimations(this IEnumerable<BlendShapeWeight> blendShapes)
    {
        return blendShapes.Select(bs => bs.ToBlendShapeAnimation());
    }

    public static GenericAnimation ToGenericAnimation(this BlendShapeWeight blendShape, string path)
    {
        return blendShape.ToBlendShapeAnimation().ToGeneric(path);
    }

    public static IEnumerable<GenericAnimation> ToGenericAnimations(this IEnumerable<BlendShapeWeight> blendShapes, string path)
    {
        return blendShapes.Select(bs => bs.ToGenericAnimation(path));
    }

    public static IEnumerable<BlendShapeWeight> ToFirstFrameBlendShapes(this IEnumerable<BlendShapeWeightAnimation> animations)
    {
        return animations.Select(a => a.ToFirstFrameBlendShape());
    }

    public static IEnumerable<GenericAnimation> ToGenericAnimations(this IEnumerable<BlendShapeWeightAnimation> blendShapes, string path)
    {
        return blendShapes.Select(bs => bs.ToGeneric(path));
    }
}