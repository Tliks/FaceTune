namespace com.aoyon.facetune.ui;

internal class FaceTuneCustomEditorBase<T> : Editor where T : FaceTuneTagComponent
{
    public T Component = null!;
    public SessionContext? Context = null;

    public virtual void OnEnable()
    {
        Component = (target as T)!;
        SessionContextBuilder.TryGet(Component.gameObject, out Context);
    }

    public virtual void OnDisable()
    {
        Context = null;
    }

    public override void OnInspectorGUI()
    {
        if (Context == null)
        {
            EditorGUILayout.HelpBox("Failed to get SessionContext.", MessageType.Error);
        }

        serializedObject.Update();
        var iterator = serializedObject.GetIterator();
        iterator.NextVisible(true);
        while (iterator.NextVisible(false))
        {
            EditorGUILayout.PropertyField(iterator, true);
        }
        serializedObject.ApplyModifiedProperties();
    }
}