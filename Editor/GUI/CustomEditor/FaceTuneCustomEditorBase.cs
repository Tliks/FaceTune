namespace com.aoyon.facetune.ui;

internal class FaceTuneCustomEditorBase<T> : Editor where T : FaceTuneTagComponent
{
    public T Component = null!;
    private bool _isMainComponent = false;
    public SessionContext? Context;

    public virtual void OnEnable()
    {
        Component = (target as T)!;
        _isMainComponent = target is FaceTuneComponent;

        var mainComponent = _isMainComponent 
            ? Component as FaceTuneComponent 
            : Component.GetComponentInParentNullable<FaceTuneComponent>();

        if (mainComponent == null) return;

        if (mainComponent.TryGetSessionContext(out var ctx))
        {
            Context = ctx;
        }
    }

    public virtual void OnDisable()
    {
        Context = null;
    }

    public override void OnInspectorGUI()
    {
        if (!_isMainComponent && Context == null)
        {
            EditorGUILayout.HelpBox("Setup FaceTuneComponent and use this component as a child of it.", MessageType.Error);
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