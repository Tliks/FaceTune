namespace Aoyon.FaceTune.Gui;

internal static class ExpressionDataEditorUtility
{
    public static void AddClipFirstFrame(
        ExpressionData data,
        ICollection<BlendShapeWeight> resultToAdd,
        string? bodyPath,
        IReadOnlyList<BlendShapeWeightAnimation>? facialAnimations = null)
    {
        if (data.Clip == null) return;
        data.Clip.GetFirstFrameBlendShapes(data.ClipOption, resultToAdd, bodyPath, facialAnimations);
    }
}
