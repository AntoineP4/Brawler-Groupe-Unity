using UnityEngine;
using UnityEngine.InputSystem;

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(StarterAssetsInputs))]
    public class DashAbility : MonoBehaviour
    {
        public float dashSpeed = 16f;
        public float dashDuration = 0.18f;
        public float dashCooldown = 0.6f;

        CharacterController cc;
        StarterAssetsInputs inputs;
        Transform cam;
        bool isDashing;
        float dashTime;
        float cd;
        Vector3 dashDir;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            inputs = GetComponent<StarterAssetsInputs>();
            cam = Camera.main ? Camera.main.transform : null;
        }

        void Update()
        {
            if (cd > 0f) cd -= Time.deltaTime;

            if (!isDashing && cd <= 0f && TriggerPressed())
            {
                dashDir = ComputeDir();
                isDashing = true;
                dashTime = dashDuration;
                cd = dashCooldown;
            }

            if (isDashing)
            {
                cc.Move(dashDir * dashSpeed * Time.deltaTime);
                dashTime -= Time.deltaTime;
                if (dashTime <= 0f) isDashing = false;
            }
        }

        bool TriggerPressed()
        {
            if (!inputs.dash) return false;
            inputs.dash = false;
            return true;
        }

        Vector3 ComputeDir()
        {
            Vector2 m = inputs.move;
            Vector3 dir;
            if (m.sqrMagnitude > 0.01f && cam != null)
            {
                Vector3 f = cam.forward; f.y = 0f; f.Normalize();
                Vector3 r = cam.right; r.y = 0f; r.Normalize();
                dir = (f * m.y + r * m.x).normalized;
            }
            else
            {
                dir = transform.forward;
                dir.y = 0f;
                dir.Normalize();
            }
            return dir;
        }
    }
}
