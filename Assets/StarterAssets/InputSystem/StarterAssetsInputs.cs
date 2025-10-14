using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public class StarterAssetsInputs : MonoBehaviour
    {
        [Header("Character Input Values")]
        public Vector2 move;
        public Vector2 look;
        public bool jump;
        public bool sprint;
        public bool dash;
        public bool pogo;

        [Header("Movement Settings")]
        public bool analogMovement = true;

        [Header("Mouse Cursor Settings")]
        public bool cursorLocked = true;
        public bool cursorInputForLook = true;

        [Header("Sprint From Stick")]
        [Range(0f, 1f)] public float sprintThreshold = 0.9f;
        public bool useStickToSprint = true;

#if ENABLE_INPUT_SYSTEM
        public void OnMove(InputValue value) { MoveInput(value.Get<Vector2>()); }
        public void OnLook(InputValue value) { if (cursorInputForLook) LookInput(value.Get<Vector2>()); }
        public void OnJump(InputValue value) { JumpInput(value.isPressed); }
        public void OnSprint(InputValue value) { if (!useStickToSprint) SprintInput(value.isPressed); }
        public void OnPogo(InputValue value) { PogoInput(value.isPressed); }
        public void OnDash(InputValue value) { DashInput(value.isPressed); }
#endif

        public void MoveInput(Vector2 v) => move = v;
        public void LookInput(Vector2 v) => look = v;
        public void JumpInput(bool v) => jump = v;
        public void SprintInput(bool v) => sprint = v;
        public void PogoInput(bool v) => pogo = v;
        public void DashInput(bool v) => dash = v;

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (useStickToSprint)
            {
                bool pad = Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame;
                bool kb = Keyboard.current != null && Keyboard.current.wasUpdatedThisFrame;

                if (pad)
                {
                    // Sur manette, sprint seulement si le stick est poussé fort
                    float th = sprintThreshold * sprintThreshold;
                    sprint = move.sqrMagnitude >= th;
                }
                else if (kb)
                {
                    // Sur clavier, on court dès qu’on bouge
                    sprint = move.sqrMagnitude > 0.01f;
                }
            }
#endif
        }

        private void OnApplicationFocus(bool hasFocus) { SetCursorState(cursorLocked); }
        public void SetCursorState(bool s) { Cursor.lockState = s ? CursorLockMode.Locked : CursorLockMode.None; }
    }
}
