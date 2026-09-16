namespace Aoyon.FaceTune.Gui;

internal abstract class FaceTuneEditorBase<T> : Editor where T : FaceTuneTagComponent
{
    protected T Component => (T)target;
    protected virtual bool ShowLanguageSwitcher => false;
    protected virtual bool ShowAvatarContextWarning => true;

    public sealed override void OnInspectorGUI()
    {
        using var _ = new Utils.ProfilingSampleScope($"Inspector.{typeof(T).Name}");
        serializedObject.UpdateIfRequiredOrScript();
        PrepareInspector();
        DrawAvatarContextWarning();

        var height = GetInspectorHeight();
        var position = EditorGUILayout.GetControlRect(false, height, GUIStyle.none);
        DrawInspector(position);

        serializedObject.ApplyModifiedProperties();
        if (ShowLanguageSwitcher)
        {
            EditorGUILayout.Space();
            Localization.DrawLanguageSwitcher();
        }
    }

    protected virtual void PrepareInspector()
    {
    }

    private void DrawAvatarContextWarning()
    {
        if (!ShowAvatarContextWarning) return;

        var failures = targets
            .OfType<FaceTuneTagComponent>()
            .Select(component => AvatarContext.TryGet(
                component.gameObject,
                out _,
                out var result)
                ? AvatarContext.BuildResult.Success
                : result)
            .Where(result => result != AvatarContext.BuildResult.Success)
            .ToArray();
        if (failures.Length == 0) return;

        var messageKey = targets.Length > 1
            ? "inspector.avatarContext.multiple.message"
            : failures[0] switch
            {
                AvatarContext.BuildResult.NotFoundAvatarRoot
                    => "inspector.avatarContext.avatar.message",
                AvatarContext.BuildResult.NotFoundFaceRenderer
                    => "inspector.avatarContext.renderer.message",
                AvatarContext.BuildResult.NotFoundFaceMesh
                    => "inspector.avatarContext.mesh.message",
                _ => throw new ArgumentOutOfRangeException()
            };
        EditorGUILayout.HelpBox(messageKey.LS(), MessageType.Warning);
        EditorGUILayout.Space(GUIHelper.VerticalSpacing);
    }

    protected virtual void OnDisable()
    {
    }

    protected virtual float GetInspectorHeight()
    {
        var height = 0f;
        var iterator = serializedObject.GetIterator();
        var enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyPath == "m_Script") continue;
            height += GUIHelper.PropertyHeight(iterator);
        }
        return Mathf.Max(0f, height - GUIHelper.VerticalSpacing);
    }

    protected virtual void DrawInspector(Rect position)
    {
        var iterator = serializedObject.GetIterator();
        var enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyPath == "m_Script") continue;
            var property = iterator.Copy();
            position.height = EditorGUI.GetPropertyHeight(property, true);
            EditorGUI.PropertyField(position, property, true);
            position.NewLine();
        }
    }

}
