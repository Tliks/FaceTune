namespace aoyon.facetune.ui;

internal class FaceTuneCustomEditorBase<T> : Editor where T : FaceTuneTagComponent
{
    public T Component = null!;

    public virtual void OnEnable()
    {
        Component = (target as T)!;
    }

    public virtual void OnDisable()
    {
    }

    public override void OnInspectorGUI()
    {
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