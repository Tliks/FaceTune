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
    private bool enableHierarchySelectedExpressionPreview = true;
    [SerializeField]
    private bool enableProjectSelectedExpressionPreview = true;

    public static Action? SelectedExpressionPreviewSettingsChanged;

    public static bool EnableHierarchySelectedExpressionPreview
    {
        get => instance.enableHierarchySelectedExpressionPreview;
        set
        {
            if (instance.enableHierarchySelectedExpressionPreview == value) return;
            instance.enableHierarchySelectedExpressionPreview = value;
            SavePreviewSettings();
        }
    }

    public static bool EnableProjectSelectedExpressionPreview
    {
        get => instance.enableProjectSelectedExpressionPreview;
        set
        {
            if (instance.enableProjectSelectedExpressionPreview == value) return;
            instance.enableProjectSelectedExpressionPreview = value;
            SavePreviewSettings();
        }
    }

    private static void SavePreviewSettings()
    {
        instance.Save(true);
        SelectedExpressionPreviewSettingsChanged?.Invoke();
    }
}
    