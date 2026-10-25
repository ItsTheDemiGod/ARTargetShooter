using UnityEngine.InputSystem.EnhancedTouch;

namespace ARTargetShooter
{
    /// <summary>
    /// Reference-counted switch for EnhancedTouchSupport. Every script that reads
    /// EnhancedTouch calls Acquire() in OnEnable and Release() in OnDisable, so one script
    /// being disabled never turns touch off for the others.
    /// </summary>
    public static class TouchInputService
    {
        private static int users;

        public static void Acquire()
        {
            users++;
            if (users == 1) EnhancedTouchSupport.Enable();
        }

        public static void Release()
        {
            if (users == 0) return;   // unbalanced Release: ignore
            users--;
            if (users == 0) EnhancedTouchSupport.Disable();
        }
    }
}
