namespace Aoyon.FaceTune.Importing;

internal static class FaceTuneImporterUtility
{
    public static SettingsComponent ImportFaceRendererAsSettings(
        AvatarContext context,
        GameObject destination)
    {
        var settings = destination.GetComponent<SettingsComponent>();
        if (settings == null)
            settings = Undo.AddComponent<SettingsComponent>(destination);
        else
            Undo.RecordObject(settings, "Import FaceRenderer Settings");

        settings.HasFacialBlendShapes = true;
        settings.FacialBlendShapesReference.Mode = SettingsReferenceMode.Direct;
        settings.FacialBlendShapesReference.Source = null;
        settings.FacialBlendShapes.Clip = null;
        settings.FacialBlendShapes.BlendShapeAnimations = context.FaceRenderer
            .GetBlendShapeWeights(context.FaceMesh)
            .ToBlendShapeAnimations()
            .ToList();
        settings.ApplyToRenderer = true;
        return settings;
    }
}
