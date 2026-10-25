using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ARTargetShooter
{
    /// <summary>
    /// Raises OnLongPress once after the pointer has been held on this UI element for holdSeconds.
    /// Uses unscaled time and the UI pointer events (works with the Input System UI module).
    /// </summary>
    public class LongPressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float holdSeconds = 1f;

        public event Action OnLongPress;

        private bool pressing;
        private float pressStart;

        public void OnPointerDown(PointerEventData eventData)
        {
            pressing = true;
            pressStart = Time.unscaledTime;
        }

        // Releasing or sliding off before the time is up cancels the press.
        public void OnPointerUp(PointerEventData eventData) => pressing = false;
        public void OnPointerExit(PointerEventData eventData) => pressing = false;

        private void Update()
        {
            if (pressing && Time.unscaledTime - pressStart >= holdSeconds)
            {
                pressing = false;   // fire once per hold
                OnLongPress?.Invoke();
            }
        }
    }
}
