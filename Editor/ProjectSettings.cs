namespace Aoyon.FaceTune.Settings;

[FilePath($"ProjectSettings/Packages/{FaceTuneConstants.QualifiedName}/settings.json", FilePathAttribute.Location.ProjectFolder)]
internal class ProjectSettings : ScriptableSingleton<ProjectSettings>
{
    // instanceの生成はメインスレッドでのみ可能なので一応ここで実行しておく
    [InitializeOnLoadMethod]
    static void Init()
    {
        _ = instance;
    }

    [SerializeField]
    private bool enableSelectedExpressionPreview = true;
    public static Action<bool>? EnableSelectedExpressionPreviewChanged;
    public static bool EnableSelectedExpressionPreview
    {
        get
        {
            return instance.enableSelectedExpressionPreview;
        }
        set
        {
            if (instance.enableSelectedExpressionPreview == value) return;
            instance.enableSelectedExpressionPreview = value;
            instance.Save(true);
            EnableSelectedExpressionPreviewChanged?.Invoke(value);
        }
    }
}
    