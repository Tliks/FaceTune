namespace com.aoyon.facetune.ui;

internal class FaceTuneCustomEditorBase<T> : Editor where T : FaceTuneTagComponent
{
    public T Component = null!;
    private bool _isMainComponent = false;
    public bool CanBuild = false;
    public SessionContext Context;

    void OnEnable()
    {
        Component = (target as T)!;
        _isMainComponent = target is FaceTuneComponent;

        var mainComponent = _isMainComponent 
            ? Component as FaceTuneComponent 
            : Component.GetComponentInParentNullable<FaceTuneComponent>();

        if (mainComponent == null) return;

        CanBuild = mainComponent.TryGetSessionContext(out var ctx);
        if (CanBuild) Context = ctx;
    }

    public override void OnInspectorGUI()
    {
        if (!_isMainComponent && !CanBuild)
        {
            EditorGUILayout.HelpBox("Setup FaceTuneComponent and use this component as a child of it.", MessageType.Error);
            return;
        }

        base.OnInspectorGUI();
    }
}