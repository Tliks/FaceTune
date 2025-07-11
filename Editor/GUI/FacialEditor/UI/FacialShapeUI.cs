using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace aoyon.facetune.ui
{
    internal class FacialShapeUI
    {
        private readonly BlendShapeOverrideManager _blendShapeManager;
        private readonly VisualElement _root;
        private readonly BlendShapeGrouping _groupManager;

        private VisualElement _groupTogglesContainer;
        private readonly TextField _selectedSearchField;
        private readonly TextField _styleSearchField;
        private readonly TextField _unselectedSearchField;
        private readonly FloatField _addWeightField;
        private readonly ListView _styleListView;
        private readonly ListView _selectedListView;
        private readonly ListView _unselectedListView;

        private readonly List<string> _styleShapesSource;
        private readonly List<string> _selectedShapesSource;
        private readonly List<string> _unselectedShapesSource;

        public FacialShapeUI(VisualElement root, BlendShapeOverrideManager manager)
        {
            _root = root;
            _blendShapeManager = manager;
            _groupManager = new BlendShapeGrouping(_blendShapeManager.GetAllKeys());

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                AssetDatabase.GUIDToAssetPath("5405c529d1ac1ba478455a85e4b1c771")
            );
            if (styleSheet != null) _root.styleSheets.Add(styleSheet);

            _selectedSearchField = new TextField();
            _styleSearchField = new TextField();
            _unselectedSearchField = new TextField();
            _addWeightField = new FloatField("Wgt:") { value = 100, style = { width = 100, marginLeft = 5 } };
            _addWeightField.labelElement.style.minWidth = 30;

            _styleListView = new ListView { reorderable = false, selectionType = SelectionType.Single, style = { flexGrow = 1 } };
            _selectedListView = new ListView { reorderable = false, selectionType = SelectionType.Single, style = { flexGrow = 1 } };
            _unselectedListView = new ListView { reorderable = false, selectionType = SelectionType.Single, style = { flexGrow = 1 } };

            _styleShapesSource = new List<string>();
            _selectedShapesSource = new List<string>();
            _unselectedShapesSource = new List<string>();

            _styleListView.itemsSource = _styleShapesSource;
            _selectedListView.itemsSource = _selectedShapesSource;
            _unselectedListView.itemsSource = _unselectedShapesSource;
            
            CreateGUI();

            _blendShapeManager.OnDataModified += RefreshUI;
            _selectedSearchField.RegisterValueChangedCallback(_ => RefreshUI());
            _styleSearchField.RegisterValueChangedCallback(_ => RefreshUI());
            _unselectedSearchField.RegisterValueChangedCallback(_ => RefreshUI());

            RefreshUI();
        }

        private void CreateGUI()
        {
            _root.Clear();
            var mainContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            var leftPanel = new VisualElement { style = { flexBasis = new Length(60, LengthUnit.Percent), flexGrow = 1, flexDirection = FlexDirection.Column } };
            var rightPanel = new VisualElement { style = { flexBasis = new Length(40, LengthUnit.Percent), flexGrow = 1, flexDirection = FlexDirection.Column } };
            var horizontalSplitter = SplitterFactory.Create(leftPanel, rightPanel, SplitterFactory.Direction.Horizontal);

            mainContainer.Add(leftPanel);
            mainContainer.Add(horizontalSplitter);
            mainContainer.Add(rightPanel);
            _root.Add(mainContainer);

            var groupFilterPanel = CreateGroupFilterPanel();
            var selectedShapesPanel = CreateFilteredListView("Selected Shapes", _selectedListView, _selectedSearchField,
                (shapeNames, weight) => _blendShapeManager.SetShapesWeight(shapeNames, weight),
                shapeNames => _blendShapeManager.UnoverrideShapes(shapeNames.Select(n => _blendShapeManager.GetIndexForShape(n)))
            );
            var styleShapesPanel = CreateFilteredListView("Style Shapes", _styleListView, _styleSearchField,
                (shapeNames, weight) => _blendShapeManager.SetShapesWeight(shapeNames, weight), null);
            
            var leftSplitter = SplitterFactory.Create(styleShapesPanel, selectedShapesPanel, SplitterFactory.Direction.Vertical);
            leftPanel.Add(groupFilterPanel);
            leftPanel.Add(styleShapesPanel);
            leftPanel.Add(leftSplitter);
            leftPanel.Add(selectedShapesPanel);

            var unselectedShapesPanel = CreateFilteredListView("Unselected Shapes", _unselectedListView, _unselectedSearchField, null, null);
            // クリックで選択のみ（何もしない）
            _unselectedListView.selectionType = SelectionType.None;
            _unselectedListView.makeItem = () => {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, minHeight = 24, paddingTop = 2, paddingBottom = 2, paddingLeft = 2, paddingRight = 2 } };
                var label = new Label { style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft } };
                row.Add(label);
                row.RegisterCallback<ClickEvent>(evt => {
                    var shapeName = row.userData as string;
                    if (string.IsNullOrEmpty(shapeName)) return;
                    var shapeIndex = _blendShapeManager.GetIndexForShape(shapeName);
                    if (shapeIndex != -1 && _unselectedShapesSource.Contains(shapeName))
                    {
                        PerformUIInitiatedUpdate(
                            () => _blendShapeManager.OverrideShapes(new List<int> { shapeIndex }, _addWeightField.value),
                            () => {
                                if (_unselectedShapesSource.Remove(shapeName))
                                {
                                    if (!_selectedShapesSource.Contains(shapeName))
                                    {
                                        _selectedShapesSource.Add(shapeName);
                                    }
                                }
                                _selectedShapesSource.Sort();
                                _selectedListView.RefreshItems();
                                _unselectedListView.RefreshItems();
                            }
                        );
                    }
                });
                return row;
            };
            _unselectedListView.bindItem = (element, index) => {
                var label = element.Q<Label>();
                var shapeName = _unselectedShapesSource[index];
                label.text = shapeName;
                element.userData = shapeName;
                element.EnableInClassList("list-item-even", index % 2 == 0);
                element.EnableInClassList("list-item-odd", index % 2 != 0);
            };
            // onSelectionChange/onItemsChosenは不要なので削除

            var addAllContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 5, paddingRight = 5 } };
            var addAllButton = new Button(() =>
            {
                var itemsToAdd = _unselectedShapesSource.ToList();
                var indices = itemsToAdd.Select(name => _blendShapeManager.GetIndexForShape(name)).Where(i => i != -1);
                PerformUIInitiatedUpdate(
                    () => _blendShapeManager.OverrideShapes(indices, _addWeightField.value),
                    RefreshUI
                );
            }) { text = "Add All", style = { flexGrow = 1 } };
            addAllContainer.Add(addAllButton);
            addAllContainer.Add(_addWeightField);
            unselectedShapesPanel.Insert(2, addAllContainer);
            rightPanel.Add(unselectedShapesPanel);

            // ListView全体にPointerMoveEventを登録し、hover制御をC#で管理
            _unselectedListView.RegisterCallback<PointerMoveEvent>(evt => {
                foreach (var row in _unselectedListView.Query<VisualElement>().Class("list-item-container").ToList())
                    row.RemoveFromClassList("hover");
                var panel = _unselectedListView.panel;
                if (panel != null) {
                    var ve = panel.Pick(evt.position);
                    if (ve != null && ve.ClassListContains("list-item-container"))
                        ve.AddToClassList("hover");
                }
            });
        }

        private VisualElement CreateGroupFilterPanel()
        {
            var settingsBox = new Box { style = { marginBottom = 10, paddingLeft = 5, paddingRight = 5, paddingTop = 5, paddingBottom = 5, flexShrink = 0 } };
            settingsBox.Add(new Label("Group Filter") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5 } });
            _groupTogglesContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            settingsBox.Add(_groupTogglesContainer);
            RebuildGroupToggles();
            var buttonsContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 5 } };
            var allButton = new Button(() => { _groupManager.SelectAll(true); RebuildGroupToggles(); RefreshUI(); }) { text = "All", style = { flexGrow = 1 } };
            var noneButton = new Button(() => { _groupManager.SelectAll(false); RebuildGroupToggles(); RefreshUI(); }) { text = "None", style = { flexGrow = 1 } };
            buttonsContainer.Add(allButton);
            buttonsContainer.Add(noneButton);
            settingsBox.Add(buttonsContainer);
            return settingsBox;

            void RebuildGroupToggles()
            {
                _groupTogglesContainer.Clear();
                foreach (var group in _groupManager.Groups)
                {
                    var toggle = new Toggle($"{group.Name}({group.BlendShapeIndices.Count})") { value = group.IsSelected };
                    toggle.AddToClassList("group-toggle");
                    toggle.RegisterValueChangedCallback(evt => { group.IsSelected = evt.newValue; RefreshUI(); });
                    _groupTogglesContainer.Add(toggle);
                }
            }
        }

        private VisualElement CreateFilteredListView(string title, ListView listView, TextField searchField, Action<IEnumerable<string>, float> setAllCallback, Action<IEnumerable<string>> removeAllCallback)
        {
            var container = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Column } };
            container.Add(new Label(title) { style = { unityFontStyleAndWeight = FontStyle.Bold, paddingLeft = 5 } });
            var searchRow = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingLeft = 5, paddingRight = 5, paddingTop = 5, paddingBottom = 5 } };
            searchField.style.flexGrow = 1;
            searchRow.Add(searchField);
            container.Add(searchRow);

            if (title == "Unselected Shapes")
            {
                listView.makeItem = MakeUnselectedElement;
                listView.bindItem = (e, i) => BindUnselectedElement(e, (string)listView.itemsSource[i], i);
            }
            else
            {
                listView.makeItem = MakeSelectedElement;
                listView.bindItem = (e, i) => BindSelectedElement(e, (string)listView.itemsSource[i], title == "Style Shapes", i);
            }
            container.Add(listView);

            var buttonContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingLeft = 5, paddingRight = 5, paddingTop = 5, paddingBottom = 5 } };
            if (setAllCallback != null)
            {
                var setAll100Button = new Button(() => { PerformUIInitiatedUpdate(() => setAllCallback(listView.itemsSource.Cast<string>().ToList(), 100f), RefreshUI); }) { text = "Set All to 100", style = { flexGrow = 1 } };
                buttonContainer.Add(setAll100Button);
                var setAll0Button = new Button(() => { PerformUIInitiatedUpdate(() => setAllCallback(listView.itemsSource.Cast<string>().ToList(), 0f), RefreshUI); }) { text = "Set All to 0", style = { flexGrow = 1 } };
                buttonContainer.Add(setAll0Button);
            }

            if (removeAllCallback != null)
            {
                var removeAllButton = new Button(() => { PerformUIInitiatedUpdate(() => removeAllCallback(listView.itemsSource.Cast<string>().ToList()), RefreshUI); }) { text = "Remove All", style = { flexGrow = 1 } };
                buttonContainer.Add(removeAllButton);
            }
            if (buttonContainer.childCount > 0) container.Add(buttonContainer);
            return container;
        }

        private bool ShouldShow(string shapeName, string searchFilter)
        {
            var index = _blendShapeManager.GetIndexForShape(shapeName);
            if (index == -1) return false;
            return (string.IsNullOrEmpty(searchFilter) || shapeName.ToLower().Contains(searchFilter.ToLower())) && _groupManager.IsBlendShapeVisible(index);
        }

        private VisualElement MakeSelectedElement()
        {
            var container = new VisualElement { name = "list-item-container", style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, minHeight = 24, paddingTop = 2, paddingBottom = 2, paddingLeft = 2, paddingRight = 2 } };
            container.AddToClassList("list-item-hover");
            var nameLabel = new Label { name = "name", style = { flexGrow = 1, flexShrink = 1, minWidth = 80, unityTextAlign = TextAnchor.MiddleLeft } };
            var colorBar = new VisualElement { name = "color-bar", style = { width = 50, height = 10, marginLeft = 5, marginRight = 5, flexShrink = 0 } };
            var slider = new Slider { name = "slider", lowValue = 0, highValue = 100, style = { flexGrow = 2, flexShrink = 1, minWidth = 100 } };
            var valueLabel = new Label { name = "value", style = { width = 30, unityTextAlign = TextAnchor.MiddleRight, flexShrink = 0 } };
            var zeroButton = new Button { name = "zero-button", text = "0", style = { flexShrink = 0 } };
            zeroButton.AddToClassList("value-button");
            var hundredButton = new Button { name = "hundred-button", text = "100", style = { flexShrink = 0 } };
            hundredButton.AddToClassList("value-button");
            var actionButton = new Button { name = "action", style = { width = 60, marginLeft = 5, flexShrink = 0 } };
            
            // Register event handlers once during element creation
            slider.RegisterValueChangedCallback(evt =>
            {
                if (Math.Abs(evt.previousValue - evt.newValue) > 0.01)
                {
                    var data = container.userData as ElementData;
                    if (data != null)
                        PerformUIInitiatedUpdate(() => _blendShapeManager.SetShapeWeight(data.shapeName, evt.newValue), () => RefreshItem(data.isStyle ? _styleListView : _selectedListView, data.shapeName));
                }
            });

            actionButton.clicked += () =>
            {
                var data = container.userData as ElementData;
                if (data == null) return;
                
                if (data.isStyle)
                {
                    PerformUIInitiatedUpdate(() => _blendShapeManager.SetShapeWeight(data.shapeName, _blendShapeManager.GetInitialWeight(data.shapeName)), () => RefreshItem(_styleListView, data.shapeName));
                }
                else
                {
                    PerformUIInitiatedUpdate(() => _blendShapeManager.UnoverrideShape(data.shapeName), () =>
                    {
                        if (_selectedShapesSource.Remove(data.shapeName))
                        {
                            if (!_unselectedShapesSource.Contains(data.shapeName) && ShouldShow(data.shapeName, _unselectedSearchField.value))
                            {
                                _unselectedShapesSource.Add(data.shapeName);
                            }
                            _unselectedShapesSource.Sort();
                            _selectedListView.RefreshItems();
                            _unselectedListView.RefreshItems();
                        }
                    });
                }
            };

            zeroButton.clicked += () =>
            {
                var data = container.userData as ElementData;
                if (data != null)
                    PerformUIInitiatedUpdate(() => _blendShapeManager.SetShapeWeight(data.shapeName, 0f), () => RefreshItem(data.isStyle ? _styleListView : _selectedListView, data.shapeName));
            };

            hundredButton.clicked += () =>
            {
                var data = container.userData as ElementData;
                if (data != null)
                    PerformUIInitiatedUpdate(() => _blendShapeManager.SetShapeWeight(data.shapeName, 100f), () => RefreshItem(data.isStyle ? _styleListView : _selectedListView, data.shapeName));
            };

            container.Add(nameLabel); container.Add(colorBar); container.Add(slider); container.Add(valueLabel); container.Add(zeroButton); container.Add(hundredButton); container.Add(actionButton);
            return container;
        }

        private class ElementData
        {
            public string shapeName;
            public bool isStyle;
        }

        private void BindSelectedElement(VisualElement element, string shapeName, bool isStyle, int index)
        {
            var weight = _blendShapeManager.GetShapeWeight(shapeName);
            
            // Update userData with current item info
            element.userData = new ElementData { shapeName = shapeName, isStyle = isStyle };
            
            // Update UI elements with current data
            element.Q<Label>("name").text = shapeName;
            element.Q<Label>("value").text = weight.ToString("F0");
            UpdateColorBar(element.Q<VisualElement>("color-bar"), weight);
            element.Q<Button>("action").text = isStyle ? "Reset" : "Remove";
            element.Q<Slider>("slider").SetValueWithoutNotify(weight);

            element.EnableInClassList("list-item-even", index % 2 == 0);
            element.EnableInClassList("list-item-odd", index % 2 != 0);
            element.EnableInClassList("modified-style", isStyle && !Mathf.Approximately(weight, _blendShapeManager.GetInitialWeight(shapeName)));
        }

        private VisualElement MakeUnselectedElement()
        {
            var container = new VisualElement { name = "list-item-container", style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, minHeight = 24, paddingTop = 2, paddingBottom = 2, paddingLeft = 2, paddingRight = 2 } };
            container.AddToClassList("list-item-hover");
            var nameLabel = new Label { name = "name", style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft } };
            container.Add(nameLabel);
            return container;
        }

        private void BindUnselectedElement(VisualElement element, string shapeName, int index)
        {
            element.Q<Label>("name").text = shapeName;
            element.EnableInClassList("list-item-even", index % 2 == 0);
            element.EnableInClassList("list-item-odd", index % 2 != 0);
        }

        private void PerformUIInitiatedUpdate(Action dataUpdateAction, Action uiUpdateAction)
        {
            _blendShapeManager.OnDataModified -= RefreshUI;
            dataUpdateAction();
            uiUpdateAction();
            _blendShapeManager.OnDataModified += RefreshUI;
        }
        
        private void RefreshItem(ListView listView, string shapeName)
        {
            var source = listView.itemsSource as List<string>;
            if (source == null) return;
            var itemIndex = source.IndexOf(shapeName);
            if (itemIndex != -1)
            {
                listView.RefreshItem(itemIndex);
            }
        }

        public void RefreshUI()
        {
            var styleFilter = _styleSearchField.value;
            var selectedFilter = _selectedSearchField.value;
            var unselectedFilter = _unselectedSearchField.value;

            _styleShapesSource.Clear();
            _selectedShapesSource.Clear();
            _unselectedShapesSource.Clear();

            foreach (var shapeName in _blendShapeManager.GetAllKeys())
            {
                if (_blendShapeManager.IsStyleShape(shapeName))
                {
                    if (ShouldShow(shapeName, styleFilter)) _styleShapesSource.Add(shapeName);
                }
                else if (_blendShapeManager.IsOverridden(shapeName))
                {
                    if (ShouldShow(shapeName, selectedFilter)) _selectedShapesSource.Add(shapeName);
                }
                else
                {
                    if (ShouldShow(shapeName, unselectedFilter)) _unselectedShapesSource.Add(shapeName);
                }
            }

            _styleListView.RefreshItems();
            _selectedListView.RefreshItems();
            _unselectedListView.RefreshItems();
        }

        private void UpdateColorBar(VisualElement colorBar, float value)
        {
            var color = Color.Lerp(new Color(0.8f, 0.8f, 0.8f), new Color(0.2f, 0.8f, 0.2f), value / 100f);
            colorBar.style.backgroundColor = color;
        }
    }
} 