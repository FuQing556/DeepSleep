using UnityEngine;
using UnityEngine.EventSystems;

namespace DeepSleep.Runtime.Input.Touch
{
    public sealed class TouchCommandPad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public enum PadKind { Movement, WorldAim, Skill, Secondary, Cancel }
        public TouchCommandSource Source;
        public PadKind Kind;
        public RectTransform Area, Knob;
        private int _pointer = int.MinValue;
        public void OnPointerDown(PointerEventData e)
        {
            if (_pointer != int.MinValue) return;
            _pointer = e.pointerId; Apply(e);
            if (Kind == PadKind.WorldAim) Source.SetAttack(true);
            else if (Kind == PadKind.Skill) Source.SetSkill(true);
            else if (Kind == PadKind.Secondary) Source.SetSecondary(true);
            else if (Kind == PadKind.Cancel) Source.SetCancel(true);
        }
        public void OnDrag(PointerEventData e) { if (e.pointerId == _pointer) Apply(e); }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == _pointer) Release(); }
        private void OnDisable() { if (Source != null) Release(); }
        private void Release()
        {
            _pointer = int.MinValue;
            if (Kind == PadKind.Movement) { Source.SetMove(Vector2.zero); if (Knob != null) Knob.anchoredPosition = Vector2.zero; }
            else if (Kind == PadKind.WorldAim) Source.SetAttack(false);
            else if (Kind == PadKind.Skill) Source.SetSkill(false);
            else if (Kind == PadKind.Secondary) Source.SetSecondary(false);
            else if (Kind == PadKind.Cancel) Source.SetCancel(false);
        }
        private void Apply(PointerEventData e)
        {
            if (Kind == PadKind.WorldAim) Source.SetAimScreen(e.position);
            if (Kind != PadKind.Movement) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Area, e.position, e.pressEventCamera, out var local);
            float radius = Mathf.Min(Area.rect.width, Area.rect.height) * .5f;
            Vector2 move = Vector2.ClampMagnitude(local / Mathf.Max(1, radius), 1);
            Source.SetMove(move); if (Knob != null) Knob.anchoredPosition = move * radius;
        }
    }
}
