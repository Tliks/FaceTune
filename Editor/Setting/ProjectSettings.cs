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

    private static void SetValue<T>(ref T field, T value)
    {
        if (!Equals(field, value))
        {
            field = value;
            instance.Save(true);
        }
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
            SetValue(ref instance.enableSelectedExpressionPreview, value);
            EnableSelectedExpressionPreviewChanged?.Invoke(value);
        }
    }
}
    