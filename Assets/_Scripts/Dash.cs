using UnityEngine;
using UnityEngine.InputSystem;

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(StarterAssetsInputs))]
    public class DashAbility : MonoBehaviour
    {
        public float dashDuration = 0.18f;
        public float dashCooldown = 0.6f;
        public float dashSpeed = 16f;
        public float dashDistance = 0f;
        public string rollStateName = "Roll";

        CharacterController cc;
        StarterAssetsInputs inputs;
        Transform cam;
        Animator anim;
        bool isDashing;
        float dashTime;
        float cd;
        Vector3 dashDir;
        float speedThisDash;
        int rollStateHash;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            inputs = GetComponent<StarterAssetsInputs>();
            cam = Camera.main ? Camera.main.transform : null;
            anim = GetComponent<Animator>();
            rollStateHash = Animator.StringToHash(rollStateName);
        }

        void Update()
        {
            if (cd > 0f) cd -= Time.deltaTime;

            if ((isDashing || cd > 0f) && inputs.dash)
                inputs.dash = false;

            if (!isDashing && cd <= 0f && TriggerPressed())
            {
                dashDir = ComputeDir();
                isDashing = true;
                dashTime = dashDuration;
                cd = dashCooldown;
                speedThisDash = dashDistance > 0f && dashDuration > 0f ? dashDistance / dashDuration : dashSpeed;

                if (anim) anim.CrossFadeInFixedTime(rollStateHash, 0f, 0, 0f);
            }

            if (isDashing)
            {
                cc.Move(dashDir * speedThisDash * Time.deltaTime);
                dashTime -= Time.deltaTime;
                if (dashTime <= 0f)
                    isDashing = false;
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
            if (m.sqrMagnitude > 0.01f && cam != null)
            {
                Vector3 f = cam.forward; f.y = 0f; f.Normalize();
                Vector3 r = cam.right; r.y = 0f; r.Normalize();
                return (f * m.y + r * m.x).normalized;
            }
            Vector3 dir = transform.forward;
            dir.y = 0f;
            return dir.normalized;
        }
    }
}
