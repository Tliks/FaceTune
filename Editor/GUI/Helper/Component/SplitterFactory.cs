using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui
{
    public static class SplitterFactory
    {
        public enum Direction { Horizontal, Vertical }

        public static VisualElement Create(VisualElement panel1, VisualElement panel2, Direction direction, float minPanelSize = 50f)
        {
            var splitter = new VisualElement();
            splitter.AddToClassList(direction == Direction.Horizontal ? "horizontal-splitter" : "vertical-splitter");

            splitter.RegisterCallback<PointerDownEvent>(evt =>
            {
                splitter.CapturePointer(evt.pointerId);
                var startPos = (direction == Direction.Horizontal) ? evt.position.x : evt.position.y;
                var startSize1 = (direction == Direction.Horizontal) ? panel1.resolvedStyle.flexBasis.value : panel1.resolvedStyle.height;
                var startSize2 = (direction == Direction.Horizontal) ? panel2.resolvedStyle.flexBasis.value : panel2.resolvedStyle.height;

                void PointerMoveHandler(PointerMoveEvent moveEvt)
                {
                    var currentPos = (direction == Direction.Horizontal) ? moveEvt.position.x : moveEvt.position.y;
                    var delta = currentPos - startPos;
                    var newSize1 = startSize1 + delta;
                    var newSize2 = startSize2 - delta;

                    if (newSize1 < minPanelSize || newSize2 < minPanelSize) return;

                    panel1.style.flexBasis = newSize1;
                    panel2.style.flexBasis = newSize2;
                }

                void PointerUpHandler(PointerUpEvent upEvt)
                {
                    splitter.UnregisterCallback<PointerMoveEvent>(PointerMoveHandler);
                    splitter.UnregisterCallback<PointerUpEvent>(PointerUpHandler);
                    splitter.ReleasePointer(upEvt.pointerId);
                }

                splitter.RegisterCallback<PointerMoveEvent>(PointerMoveHandler);
                splitter.RegisterCallback<PointerUpEvent>(PointerUpHandler);
            });

            return splitter;
        }
    }
} 