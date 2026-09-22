using Aoyon.FaceTune.Gui.Components;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class LipSyncPanel
{
    private readonly FacialShapesEditorContext _context;
    private readonly TextField _selectedSearch = new PlaceholderTextField();
    private readonly TextField _availableSearch = new PlaceholderTextField();
    private readonly ScrollView _sections = new();
    private readonly ListView _available = new();
    private readonly List<string> _availableNames = new();
    private readonly List<SimpleToggle> _visemeButtons = new();
    private readonly List<VisualElement> _visemeContents = new();

    public VisualElement SelectedElement { get; } = new SpacedVerticalElement();
    public VisualElement AvailableElement { get; } = new SpacedVerticalElement();

    public LipSyncPanel(FacialShapesEditorContext context)
    {
        _context = context;
        _selectedSearch.value = string.Empty;
        _availableSearch.value = string.Empty;
        if (_selectedSearch is PlaceholderTextField selected)
            selected.Placeholder = "facialEditor.search.placeholder".LS();
        if (_availableSearch is PlaceholderTextField available)
            available.Placeholder = "facialEditor.search.placeholder".LS();
        _selectedSearch.RegisterValueChangedCallback(_ => RebuildSections());
        _availableSearch.RegisterValueChangedCallback(_ => RebuildAvailable());

        _sections.style.flexGrow = 1f;
        _available.style.flexGrow = 1f;
        SelectedElement.Add(CreateModeField());
        SelectedElement.Add(_selectedSearch);
        SelectedElement.Add(_sections);
        AvailableElement.Add(_availableSearch);
        AvailableElement.Add(_available);

        _available.fixedItemHeight = FacialShapeUI.ListItemHeight;
        _available.selectionType = SelectionType.None;
        _available.makeItem = MakeAvailableItem;
        _available.bindItem = BindAvailableItem;
        _available.RegisterCallback<MouseLeaveEvent>(_ =>
            _context.PreviewManager.CurrentHoveredIndex = -1);
        context.GroupManager.OnGroupSelectionChanged += _ => Rebuild();
        context.GroupManager.OnLeftSelectionChanged += _ => RebuildSections();
        context.GroupManager.OnRightSelectionChanged += _ => RebuildAvailable();

        Rebuild();
    }

    private VisualElement CreateModeField()
    {
        var labels = new[]
        {
            "lipSync.mode.option.builtIn".LS(),
            "lipSync.mode.option.custom".LS()
        };
        var selected = _context.LipSync?.Mode == LipSyncSettings.Kind.Custom ? 1 : 0;
        var field = new PopupField<string>(
            "lipSync.mode.label".LS(),
            labels.ToList(),
            selected);
        field.RegisterValueChangedCallback(evt =>
        {
            var custom = evt.newValue == labels[1];
            SetMode(custom ? LipSyncSettings.Kind.Custom : LipSyncSettings.Kind.BuiltIn);
        });
        return field;
    }

    private void SetMode(LipSyncSettings.Kind mode)
    {
        if (_context.LipSyncProperty == null || _context.LipSync?.Mode == mode) return;
        var serialized = _context.SerializedObject;
        serialized.UpdateIfRequiredOrScript();
        var modeProperty = _context.LipSyncProperty.FindPropertyRelative(nameof(LipSyncSettings.Mode));
        modeProperty.intValue = (int)mode;
        if (mode == LipSyncSettings.Kind.Custom
            && _context.LipSync?.Mode == LipSyncSettings.Kind.BuiltIn
            && _context.BuiltInLipSync != null)
        {
            _context.LipSyncProperty
                .FindPropertyRelative(nameof(LipSyncSettings.Shapes))
                .CopyFrom(_context.BuiltInLipSync);
        }
        serialized.ApplyModifiedProperties();
        _context.NotifyLipSyncChanged();
        Rebuild();
    }

    public void Rebuild()
    {
        RebuildSections();
        RebuildAvailable();
    }

    private void RebuildSections()
    {
        _sections.Clear();
        _visemeButtons.Clear();
        _visemeContents.Clear();
        var values = (_context.PreviewLipSync ?? new VrcVisemeLipSyncShapes())
            .GetOrderedShapes();
        var search = _selectedSearch.value ?? string.Empty;
        for (var index = 0; index < VrcVisemeLipSyncShapes.Count; index++)
        {
            var visemeIndex = index;
            var section = new HorizontalElement();
            section.style.alignItems = Align.FlexStart;
            section.style.marginBottom = FacialShapeUI.Spacing;
            var button = new SimpleToggle
            {
                text = VrcVisemeLipSyncShapes.Names[index],
                value = index == _context.SelectedViseme
            };
            button.style.width = 44f;
            button.style.minWidth = 44f;
            button.style.maxWidth = 44f;
            button.style.height = FacialShapeUI.RowHeight;
            button.style.marginRight = FacialShapeUI.Spacing;
            button.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue)
                {
                    button.SetValueWithoutNotify(true);
                    return;
                }
                _context.SetSelectedViseme(visemeIndex);
                UpdateSelection();
            });
            button.RegisterCallback<MouseEnterEvent>(_ =>
                _context.SetHoveredViseme(visemeIndex));
            button.RegisterCallback<MouseLeaveEvent>(_ =>
                _context.SetHoveredViseme(-1));
            section.Add(button);
            _visemeButtons.Add(button);

            var content = new SpacedVerticalElement();
            content.style.flexGrow = 1f;
            content.SetEnabled(_context.LipSync?.Mode == LipSyncSettings.Kind.Custom
                               && index == _context.SelectedViseme);
            foreach (var shape in values[index])
            {
                var shapeIndex = _context.Catalog.IndexOf(shape.Name);
                if (search.Length > 0
                    && shape.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                    || _context.GroupManager.IsLeftSelected
                    && !_context.GroupManager.IsBlendShapeVisible(shapeIndex))
                    continue;
                content.Add(CreateShapeRow(index, shape));
            }
            if (content.childCount == 0)
            {
                var empty = new VisualElement();
                empty.style.height = FacialShapeUI.RowHeight;
                content.Add(empty);
            }
            section.Add(content);
            _visemeContents.Add(content);
            _sections.Add(section);
        }
    }

    private void UpdateSelection()
    {
        var editable = _context.LipSync?.Mode == LipSyncSettings.Kind.Custom;
        for (var index = 0; index < _visemeButtons.Count; index++)
        {
            _visemeButtons[index].SetValueWithoutNotify(index == _context.SelectedViseme);
            _visemeContents[index].SetEnabled(editable && index == _context.SelectedViseme);
        }
    }

    private VisualElement CreateShapeRow(int visemeIndex, BlendShapeWeight shape)
    {
        var row = SelectedShapeRowUI.Create();
        var metadata = row.Q<VisualElement>("metadata-gutter");
        var changed = row.Q<VisualElement>("changed-marker");
        var facialRail = row.Q<VisualElement>("facial-rail");
        var warning = row.Q<Image>("validation-warning");
        var name = row.Q<Label>("name");
        var weight = row.Q<SliderFloatField>("slider-float-field");
        var curve = row.Q<IMGUIContainer>("curve-field");
        var curveToggle = row.Q<Button>("curve-toggle");
        var toggle = row.Q<Button>("toggle-button");
        var remove = row.Q<Button>("action");

        var initial = GetInitialShape(visemeIndex, shape.Name);
        changed.EnableInClassList(
            "changed-marker--visible",
            initial == null || !Mathf.Approximately(initial.Value.Weight, shape.Weight));
        facialRail.style.opacity = 0f;
        metadata.RegisterCallback<ClickEvent>(_ => Restore(visemeIndex, shape.Name));
        name.text = shape.Name;
        var shapeIndex = _context.Catalog.IndexOf(shape.Name);
        var missing = shapeIndex < 0;
        var unavailable = _context.LipSyncUnavailableNames.Contains(shape.Name);
        warning.SetVisible(missing || unavailable);
        warning.tooltip = missing
            ? "blendShape.validation.missing.tooltip".LS()
            : unavailable
                ? "blendShape.validation.unavailable.tooltip".LS()
                : string.Empty;
        void ApplyWeight(float value)
        {
            SetWeight(visemeIndex, shape.Name, value);
            weight.SetValueWithoutNotify(value);
            var original = GetInitialShape(visemeIndex, shape.Name);
            changed.EnableInClassList(
                "changed-marker--visible",
                original == null || !Mathf.Approximately(original.Value.Weight, value));
        }

        weight.SetValueWithoutNotify(shape.Weight);
        weight.RegisterValueChangedCallback(evt => ApplyWeight(evt.newValue));
        curve.SetVisible(false);
        curveToggle.SetVisible(false);
        toggle.clicked += () => ApplyWeight(
            Mathf.Approximately(GetWeight(visemeIndex, shape.Name), 0f) ? 100f : 0f);
        remove.clicked += () => Remove(visemeIndex, shape.Name);
        return row;
    }

    private BlendShapeWeight? GetInitialShape(int visemeIndex, string name)
    {
        if (_context.InitialLipSync == null) return null;
        foreach (var shape in _context.InitialLipSync.Shapes.GetOrderedShapes()[visemeIndex])
        {
            if (shape.Name == name) return shape;
        }
        return null;
    }

    private float GetWeight(int visemeIndex, string name)
    {
        if (_context.LipSync == null) return 0f;
        foreach (var shape in _context.LipSync.Shapes.GetOrderedShapes()[visemeIndex])
        {
            if (shape.Name == name) return shape.Weight;
        }
        return 0f;
    }

    private void Restore(int visemeIndex, string name)
    {
        if (GetInitialShape(visemeIndex, name) is { } initial)
            SetWeight(visemeIndex, name, initial.Weight);
        else
            Remove(visemeIndex, name);
        Rebuild();
    }

    private void SetWeight(int visemeIndex, string name, float weight)
    {
        if (GetVisemeProperty(visemeIndex) is not { } property) return;
        for (var index = 0; index < property.arraySize; index++)
        {
            var element = property.GetArrayElementAtIndex(index);
            if (element.FindPropertyRelative(BlendShapeWeight.NamePropName).stringValue != name)
                continue;
            element.FindPropertyRelative(BlendShapeWeight.WeightPropName).floatValue = weight;
            property.serializedObject.ApplyModifiedProperties();
            _context.NotifyLipSyncChanged();
            return;
        }
    }

    private void Remove(int visemeIndex, string name)
    {
        if (GetVisemeProperty(visemeIndex) is not { } property) return;
        for (var index = 0; index < property.arraySize; index++)
        {
            if (property.GetArrayElementAtIndex(index)
                    .FindPropertyRelative(BlendShapeWeight.NamePropName).stringValue != name)
                continue;
            property.DeleteArrayElementAtIndex(index);
            property.serializedObject.ApplyModifiedProperties();
            _context.NotifyLipSyncChanged();
            Rebuild();
            return;
        }
    }

    private void Add(string name)
    {
        if (_context.LipSync?.Mode != LipSyncSettings.Kind.Custom
            || GetVisemeProperty(_context.SelectedViseme) is not { } property)
            return;
        for (var index = 0; index < property.arraySize; index++)
        {
            if (property.GetArrayElementAtIndex(index)
                    .FindPropertyRelative(BlendShapeWeight.NamePropName).stringValue == name)
                return;
        }
        property.InsertArrayElementAtIndex(property.arraySize);
        property.GetArrayElementAtIndex(property.arraySize - 1)
            .CopyFrom(new BlendShapeWeight(name, 100f));
        property.serializedObject.ApplyModifiedProperties();
        _context.NotifyLipSyncChanged();
        Rebuild();
    }

    private SerializedProperty? GetVisemeProperty(int index)
    {
        if (_context.LipSyncProperty == null) return null;
        _context.SerializedObject.UpdateIfRequiredOrScript();
        return _context.LipSyncProperty
            .FindPropertyRelative(nameof(LipSyncSettings.Shapes))
            .FindPropertyRelative(VrcVisemeLipSyncShapes.PropertyNames[index]);
    }

    private void RebuildAvailable()
    {
        _availableNames.Clear();
        var search = _availableSearch.value ?? string.Empty;
        foreach (var name in _context.Catalog.Names)
        {
            var shapeIndex = _context.Catalog.IndexOf(name);
            if (_context.LipSyncUnavailableNames.Contains(name)
                || search.Length > 0
                && name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                || _context.GroupManager.IsRightSelected
                && !_context.GroupManager.IsBlendShapeVisible(shapeIndex))
                continue;
            _availableNames.Add(name);
        }
        _available.itemsSource = _availableNames;
        _available.RefreshItems();
    }

    private VisualElement MakeAvailableItem()
    {
        var element = UnselectedShapeRowUI.Create();
        element.RegisterCallback<ClickEvent>(_ =>
        {
            if (element.userData is string name) Add(name);
        });
        element.RegisterCallback<MouseEnterEvent>(_ =>
        {
            if (element.userData is not string name) return;
            _context.PreviewManager.CurrentHoveredIndex =
                _context.Catalog.IndexOf(name);
        });
        return element;
    }

    private void BindAvailableItem(VisualElement element, int index)
    {
        var name = _availableNames[index];
        element.userData = name;
        element.Q<Label>("name").text = name;
        element.SetEnabled(_context.LipSync?.Mode == LipSyncSettings.Kind.Custom);
    }
}
