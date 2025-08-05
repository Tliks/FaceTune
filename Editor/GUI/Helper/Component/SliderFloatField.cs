using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.Components;

internal class SliderFloatField : VisualElement
{
    public new class UxmlFactory : UxmlFactory<SliderFloatField, UxmlTraits> { }

    public new class UxmlTraits : VisualElement.UxmlTraits
    {
        readonly UxmlFloatAttributeDescription LowValue = new() { name = "low-value", defaultValue = 0f };
        readonly UxmlFloatAttributeDescription highValue = new() { name = "high-value", defaultValue = 100f };
        readonly UxmlFloatAttributeDescription value = new() { name = "value", defaultValue = 0f };

        public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
        {
            base.Init(ve, bag, cc);
            var element = (ve as SliderFloatField)!;

            element.lowValue = LowValue.GetValueFromBag(bag, cc);
            element.highValue = highValue.GetValueFromBag(bag, cc);
            element.value = value.GetValueFromBag(bag, cc);
        }
    }

    private readonly Slider _slider;
    private readonly FloatField _floatField;

    public float value
    {
        get => _slider.value;
        set
        {
            var clampedValue = Mathf.Clamp(value, lowValue, highValue);
            _slider.SetValueWithoutNotify(clampedValue);
            _floatField.SetValueWithoutNotify(clampedValue);
        }
    }

    public float lowValue
    {
        get => _slider.lowValue;
        set => _slider.lowValue = value;
    }

    public float highValue
    {
        get => _slider.highValue;
        set => _slider.highValue = value;
    }

    public SliderFloatField()
    {
        style.flexDirection = FlexDirection.Row;
        style.alignItems = Align.Center;

        _slider = new Slider()
        {
            lowValue = 0f,
            highValue = 100f
        };
        _slider.style.flexGrow = 1;
        _slider.style.flexShrink = 1;
        _slider.style.flexBasis = new StyleLength(StyleKeyword.Auto);

        _floatField = new FloatField();
        _floatField.style.flexGrow = 0;
        _floatField.style.flexShrink = 0;
        _floatField.style.flexBasis = new StyleLength(40);

        Add(_slider);
        Add(_floatField);

        _slider.RegisterValueChangedCallback(evt =>
        {
            _floatField.SetValueWithoutNotify(evt.newValue);
            using var changeEvent = ChangeEvent<float>.GetPooled(evt.previousValue, evt.newValue);
            changeEvent.target = this;
            SendEvent(changeEvent);
        });

        _floatField.RegisterValueChangedCallback(evt =>
        {
            var clampedValue = Mathf.Clamp(evt.newValue, _slider.lowValue, _slider.highValue);
            _slider.SetValueWithoutNotify(clampedValue);
            
            if (clampedValue != evt.newValue)
            {
                _floatField.SetValueWithoutNotify(clampedValue);
            }

            using var changeEvent = ChangeEvent<float>.GetPooled(evt.previousValue, clampedValue);
            changeEvent.target = this;
            SendEvent(changeEvent);
        });
    }

    public void SetValueWithoutNotify(float newValue)
    {
        _slider.SetValueWithoutNotify(newValue);
        _floatField.SetValueWithoutNotify(newValue);
    }

    public void RegisterValueChangedCallback(EventCallback<ChangeEvent<float>> callback)
    {
        RegisterCallback(callback);
    }

    public void UnregisterValueChangedCallback(EventCallback<ChangeEvent<float>> callback)
    {
        UnregisterCallback(callback);
    }
}