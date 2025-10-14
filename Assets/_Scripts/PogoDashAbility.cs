using UnityEngine;
using UnityEngine.InputSystem;

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(StarterAssetsInputs))]
    [RequireComponent(typeof(ThirdPersonController))]
    public class PogoDashAbility : MonoBehaviour
    {
        [Header("Dash (RT)")]
        public float dashDuration = 0.18f;
        public float dashCooldown = 0.6f;
        public float dashSpeed = 16f;
        public float dashDistance = 0f;
        public string dashAnimState = "Roll";

        [Header("Pogo (X en l'air)")]
        public float pogoDownSpeed = 18f;         // vitesse de descente
        public float pogoCheckRadius = 0.6f;      // rayon de détection
        public float pogoCheckAhead = 1.0f;       // distance testée devant
        public float pogoBounceHeight = 2.2f;     // hauteur de rebond
        public bool pogoIgnoresCooldown = true;   // autorise le pogo même si le CD du dash est en cours

        [Header("Hit/Bounce")]
        public LayerMask enemyLayers;             // coche "Enemy"
        public float hitRadius = 0.6f;
        public float hitAhead = 1.0f;
        public float hitBounceHeight = 2.0f;

        // état lu par le controller pour calmer les anims FreeFall pendant dash/pogo
        public bool IsDashing { get; private set; }

        CharacterController cc;
        StarterAssetsInputs inputs;
        ThirdPersonController ctrl;
        Transform cam;
        Animator anim;

        bool isPogoDown;
        float dashTime;       // utilisé seulement pour dash avant
        float cooldown;       // CD partagé
        Vector3 dashDir;
        float speedThisDash;
        int dashHash;

        bool airDashAvailable = true;
        bool wasGrounded;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            inputs = GetComponent<StarterAssetsInputs>();
            ctrl = GetComponent<ThirdPersonController>();
            cam = Camera.main ? Camera.main.transform : null;
            anim = GetComponent<Animator>();
            dashHash = string.IsNullOrEmpty(dashAnimState) ? 0 : Animator.StringToHash(dashAnimState);
        }

        void Update()
        {
            if (cooldown > 0f) cooldown -= Time.deltaTime;

            // recharge la charge d'air au contact du sol
            if (ctrl.Grounded && !wasGrounded) airDashAvailable = true;
            wasGrounded = ctrl.Grounded;

            // on consomme l'input si on est en dash ou en CD
            if ((IsDashing || cooldown > 0f) && inputs.dash) inputs.dash = false;

            // ---- DASH AVANT (RT) ----
            if (!IsDashing && cooldown <= 0f && DashPressed())
            {
                if (ctrl.Grounded || airDashAvailable)
                {
                    dashDir = ComputeDir();
                    StartDashForward();
                    if (!ctrl.Grounded) airDashAvailable = false;
                }
            }

            // ---- POGO (X) : peut interrompre un dash en cours ----
            if ((!ctrl.Grounded) && PogoPressed())
            {
                // si on est en dash normal, on le coupe pour partir en pogo
                if (IsDashing && !isPogoDown) StopDashInternal(noAnimReset: true);
                StartPogoDown();
            }

            // ---- Update mouvement ----
            if (IsDashing)
            {
                // direction / vitesse
                Vector3 step = dashDir * speedThisDash * Time.deltaTime;
                cc.Move(step);

                if (!isPogoDown)
                {
                    // dash avant : durée limitée
                    dashTime -= Time.deltaTime;
                    // collision ennemi -> rebond et stop
                    if (CheckHitEnemy(step.magnitude))
                    {
                        StopDashInternal(false);
                        ctrl.Bounce(hitBounceHeight);
                    }
                    // fin de dash
                    if (dashTime <= 0f) StopDashInternal(false);
                }
                else
                {
                    // ---- POGO infini : descend jusqu'à collision ----
                    // 1) touche ENNEMI -> rebond + restitue charge d'air
                    if (CheckHitEnemy(step.magnitude))
                    {
                        StopDashInternal(false);
                        ctrl.Bounce(pogoBounceHeight);
                        airDashAvailable = true;
                    }
                    // 2) touche SOL/OBSTACLE -> stop sans rebond
                    //   (CharacterController met Grounded la frame suivante; on ajoute un check de contact)
                    else if (ctrl.Grounded || (cc.collisionFlags & CollisionFlags.Below) != 0 || CheckHitSurface(step.magnitude))
                    {
                        StopDashInternal(false);
                    }
                }
            }
        }

        // ---------- start/stop ----------
        void StartDashForward()
        {
            IsDashing = true;
            isPogoDown = false;
            dashTime = dashDuration;
            cooldown = dashCooldown;
            speedThisDash = dashDistance > 0f && dashDuration > 0f ? dashDistance / dashDuration : dashSpeed;

            if (anim != null && dashHash != 0)
                anim.CrossFadeInFixedTime(dashHash, 0.05f, 0, 0f);
        }

        void StartPogoDown()
        {
            if (!pogoIgnoresCooldown && cooldown > 0f) return;

            IsDashing = true;
            isPogoDown = true;
            dashDir = Vector3.down;
            speedThisDash = pogoDownSpeed;
            // on met un petit CD pour éviter le spam extrême (optionnel)
            if (pogoIgnoresCooldown && cooldown < 0.05f) cooldown = 0.05f;

            // pas d'anim imposée pour ne pas perturber Jump/FreeFall;
            // si tu veux, tu peux mettre un state dédié ici.
        }

        void StopDashInternal(bool noAnimReset)
        {
            IsDashing = false;
            isPogoDown = false;
            if (!noAnimReset && anim != null && dashHash != 0)
            {
                // on laisse l'Animator reprendre son graphe (pas de force)
                // volontairement vide
            }
        }

        // ---------- inputs ----------
        bool DashPressed()
        {
            if (!inputs.dash) return false;
            inputs.dash = false;
            return true;
        }

        bool PogoPressed()
        {
            if (!inputs.pogo) return false;
            inputs.pogo = false;
            return true;
        }

        // ---------- helpers ----------
        Vector3 ComputeDir()
        {
            Vector2 m = inputs.move;
            if (m.sqrMagnitude > 0.01f && cam != null)
            {
                Vector3 f = cam.forward; f.y = 0f; f.Normalize();
                Vector3 r = cam.right; r.y = 0f; r.Normalize();
                return (f * m.y + r * m.x).normalized;
            }
            Vector3 dir = transform.forward; dir.y = 0f;
            return dir.normalized;
        }

        bool CheckHitEnemy(float stepDist)
        {
            Vector3 origin = cc.bounds.center;
            Vector3 dir = isPogoDown ? Vector3.down : (dashDir.sqrMagnitude < 0.0001f ? transform.forward : dashDir.normalized);
            float ahead = isPogoDown ? Mathf.Max(pogoCheckAhead, stepDist) : Mathf.Max(hitAhead, stepDist);
            float radius = isPogoDown ? Mathf.Max(pogoCheckRadius, hitRadius) : hitRadius;

            if (Physics.SphereCast(origin, radius, dir, out RaycastHit _, ahead, enemyLayers, QueryTriggerInteraction.Collide))
                return true;

            Vector3 end = origin + dir * ahead;
            var cols = Physics.OverlapSphere(end, radius, enemyLayers, QueryTriggerInteraction.Collide);
            return cols != null && cols.Length > 0;
        }

        // pour stopper le pogo sur les surfaces non-ennemi (si Grounded tarde d'une frame)
        bool CheckHitSurface(float stepDist)
        {
            Vector3 origin = cc.bounds.center;
            float ahead = Mathf.Max(0.2f, stepDist + 0.1f);
            // on teste tout sauf le layer du joueur
            int mask = ~LayerMask.GetMask(LayerMask.LayerToName(gameObject.layer));
            return Physics.SphereCast(origin, hitRadius, Vector3.down, out _, ahead, mask, QueryTriggerInteraction.Ignore);
        }

        // DEBUG aide visuelle
        //void OnDrawGizmosSelected()
        //{
        //    if (!Application.isPlaying) return;
        //    Vector3 origin = cc.bounds.center;
        //    Vector3 dir = isPogoDown ? Vector3.down : (dashDir.sqrMagnitude < 0.0001f ? transform.forward : dashDir.normalized);
        //    float ahead = isPogoDown ? pogoCheckAhead : hitAhead;
        //    float radius = isPogoDown ? Mathf.Max(pogoCheckRadius, hitRadius) : hitRadius;
        //    Gizmos.DrawWireSphere(origin + dir * ahead, radius);
        //}
    }
}
