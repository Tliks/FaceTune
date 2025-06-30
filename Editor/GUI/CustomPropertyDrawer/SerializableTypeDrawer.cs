namespace aoyon.facetune;

/// <summary>
/// SerializableType構造体のためのカスタムプロパティドロワー
/// ドラッグ＆ドロップとドロップダウンメニューによる型選択に対応
/// </summary>
[CustomPropertyDrawer(typeof(SerializableType))]
internal class SerializableTypeDrawer : PropertyDrawer
{
    private static List<Type>? s_cachedTypes; // 型をキャッシュするための静的フィールド

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty nameProperty = property.FindPropertyRelative(SerializableType.NamePropName);

        string currentTypeName = GetCurrentTypeName(nameProperty.stringValue);
        
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
        HandleDragAndDrop(position, nameProperty);
        if (EditorGUI.DropdownButton(position, new GUIContent(currentTypeName), FocusType.Keyboard))
        {
            ShowTypeSelectionMenu(nameProperty);
        }

        EditorGUI.EndProperty();
    }

    private string GetCurrentTypeName(string assemblyQualifiedName)
    {
        if (string.IsNullOrEmpty(assemblyQualifiedName))
        {
            return "None (Type)";
        }
        Type type = Type.GetType(assemblyQualifiedName);
        if (type != null)
        {
            return type.Name;
        }
        else
        {
            string[] parts = assemblyQualifiedName.Split(',');
            return $"{parts[0]} (Not Found)";
        }
    }

    private void HandleDragAndDrop(Rect dropArea, SerializedProperty nameProperty)
    {
        Event evt = Event.current;
        if (!dropArea.Contains(evt.mousePosition)) return;

        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (DragAndDrop.objectReferences.Length != 1 || DragAndDrop.objectReferences[0] == null)
                    break;

                Type draggedType = GetTypeFromDraggedObject(DragAndDrop.objectReferences[0]);
                if (draggedType != null || DragAndDrop.objectReferences[0] is GameObject)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        var draggedObject = DragAndDrop.objectReferences[0];
                        if (draggedObject is GameObject go)
                        {
                            ShowComponentSelectionMenu(go, nameProperty);
                        }
                        else
                        {
                            SetType(nameProperty, draggedType);
                        }
                        evt.Use();
                    }
                }
                break;
        }
    }

    private Type GetTypeFromDraggedObject(UnityEngine.Object obj)
    {
        if (obj is MonoScript monoScript) return monoScript.GetClass();
        else return obj.GetType();
    }

    private void SetType(SerializedProperty nameProperty, Type? type)
    {
        nameProperty.stringValue = type != null ? type.AssemblyQualifiedName : "";
        nameProperty.serializedObject.ApplyModifiedProperties();
    }

    private void ShowComponentSelectionMenu(GameObject go, SerializedProperty nameProperty)
    {
        GenericMenu menu = new GenericMenu();

        var goIcon = EditorGUIUtility.IconContent("GameObject").image;
        menu.AddItem(new GUIContent("GameObject (Type)", goIcon), false, () => {
            SetType(nameProperty, typeof(GameObject));
        });
        menu.AddSeparator("");

        var components = go.GetComponents<Component>();
        foreach (var component in components)
        {
            if (component == null) continue;
            
            Type type = component.GetType();
            var content = new GUIContent($"{type.Name} ({type.Namespace})", EditorGUIUtility.ObjectContent(component, type).image);

            menu.AddItem(content, false, () => {
                SetType(nameProperty, type);
            });
        }
        menu.ShowAsContext();
    }
    
    private void ShowTypeSelectionMenu(SerializedProperty nameProperty)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("None"), string.IsNullOrEmpty(nameProperty.stringValue), () => SetType(nameProperty, null));
        menu.AddSeparator("");

        // キャッシュされた型がない場合のみ、型をロードしてキャッシュする
        if (s_cachedTypes == null)
        {
            s_cachedTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(t =>
                {
                    if (t.IsAbstract || !t.IsPublic) return false; // publicかつ具象クラスのみ
                    return t == typeof(GameObject) ||                // GameObjectを許可
                           typeof(Component).IsAssignableFrom(t) ||    // Component派生を許可
                           typeof(ScriptableObject).IsAssignableFrom(t); // ScriptableObject派生を許可
                })
                .OrderBy(t => t.FullName)
                .ToList(); // 結果をリストとしてキャッシュ
        }

        foreach (Type type in s_cachedTypes) // キャッシュされた型を使用
        {
            string menuLabel = type.FullName.Replace('.', '/');
            bool isSelected = type.AssemblyQualifiedName == nameProperty.stringValue;
            menu.AddItem(new GUIContent(menuLabel), isSelected, () => SetType(nameProperty, type));
        }
        menu.ShowAsContext();
    }
}