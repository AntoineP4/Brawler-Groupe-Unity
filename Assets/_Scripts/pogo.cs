using UnityEngine;
using UnityEngine.InputSystem;

public class PogoSlam : MonoBehaviour
{
    [Header("Refs")]
    public CharacterController cc;
    public MonoBehaviour thirdPersonController; // StarterAssets ThirdPersonController
    public Animator animator;
    public Transform feet;
    public Transform cameraTransform;

    [Header("Masks")]
    public LayerMask groundMask = ~0;
    public LayerMask enemyMask;               // mets le Layer Enemy ici

    [Header("Slam")]
    public float slamSpeed = 28f;
    public float extraGravity = 40f;

    [Header("Cast shape")]
    public float castRadius = 0.6f;           // épaisseur de la capsule
    public float castHeight = 1.2f;           // hauteur de la capsule autour des pieds
    public float groundSnap = 0.06f;

    [Header("Smoothing")]
    public float decelDistance = 2.2f;
    public float minDescentSpeed = 8f;
    public float decelTime = 0.12f;
    public float landingBlendTime = 0.06f;

    [Header("Bounce + inertie")]
    public float bounceSpeed = 16f;
    public float airSteer = 20f;              // contrôle horizontal pendant le bounce
    public float airDrag = 6f;
    public float handoffFailSafe = 1.5f;      // sécurité (sec) pour rendre le contrôle quoi qu’il arrive

    [Header("Debug")]
    public bool debugLogs = false;

    bool slamming;
    bool landingBlend;
    float landingT;
    float smoothVel;
    float failsafeTimer;

    Vector3 vVert;                 // vitesse verticale gérée ici
    Vector3 vHoriz;                // vitesse horizontale conservée/contrôlée pendant slam/bounce

    void Awake()
    {
        if (!cc) cc = GetComponent<CharacterController>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        bool jumpPressed = (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.Space);

        if (!slamming && !cc.isGrounded && jumpPressed)
            StartSlam();

        if (slamming) TickSlam();
        else if (landingBlend) TickLandingBlend();
    }

    void StartSlam()
    {
        slamming = true;
        landingBlend = false;
        failsafeTimer = 0f;

        // conserver l’inertie horizontale de départ
        vHoriz = cc.velocity; vHoriz.y = 0f;
        vVert = Vector3.down * slamSpeed;

        if (thirdPersonController) thirdPersonController.enabled = false; // on prend la main verticale
        if (animator) animator.SetFloat("Speed", 0f);

        if (debugLogs) Debug.Log("[Pogo] Slam start");
    }

    void TickSlam()
    {
        float dt = Time.deltaTime;
        failsafeTimer += dt;

        // contrôle horizontal pendant slam/bounce (air-control)
        Vector2 in2 = Vector2.zero;
        if (Gamepad.current != null) in2 = Gamepad.current.leftStick.ReadValue();
        if (in2 == Vector2.zero) { in2.x = Input.GetAxisRaw("Horizontal"); in2.y = Input.GetAxisRaw("Vertical"); }
        Vector3 camF = cameraTransform ? Vector3.Scale(cameraTransform.forward, new Vector3(1, 0, 1)).normalized : Vector3.forward;
        Vector3 camR = cameraTransform ? Vector3.Scale(cameraTransform.right, new Vector3(1, 0, 1)).normalized : Vector3.right;
        Vector3 desired = (camF * in2.y + camR * in2.x);
        if (desired.sqrMagnitude > 1f) desired.Normalize();
        vHoriz = Vector3.MoveTowards(vHoriz, desired * vHoriz.magnitude, airSteer * dt);
        vHoriz = Vector3.MoveTowards(vHoriz, Vector3.zero, airDrag * dt);

        // gravité + éventuelle décélération avant sol
        vVert += Vector3.down * extraGravity * dt;

        // --------- HIT ENEMY – robuste ----------
        Vector3 from, to;
        GetFeetCapsule(out from, out to);

        Vector3 delta = (vHoriz + vVert) * dt;
        float castDist = delta.magnitude + 0.05f;

        // 1) CapsuleCast sur la trajectoire de ce frame
        if (Physics.CapsuleCast(from, to, castRadius, delta.normalized, out RaycastHit hitE, castDist, enemyMask, QueryTriggerInteraction.Collide))
        {
            if (debugLogs) Debug.Log($"[Pogo] Enemy via CapsuleCast: {hitE.collider.name}");
            DoBounce();
            return;
        }

        // 2) Anticiper le sol: lissage + snap
        if (Physics.CapsuleCast(from, to, castRadius * 0.5f, Vector3.down, out RaycastHit gHit, 5f, groundMask, QueryTriggerInteraction.Ignore))
        {
            float d = gHit.distance;
            if (d <= decelDistance && vVert.y < 0f)
            {
                float currentDown = -vVert.y;
                float targetDown = Mathf.Max(minDescentSpeed, 0.01f);
                float eased = Mathf.SmoothDamp(currentDown, targetDown, ref smoothVel, decelTime);
                vVert.y = -eased;
            }
            if (d <= (castRadius + groundSnap) && vVert.y <= 0f)
            {
                // on arrive au sol
                cc.Move(Vector3.down * Mathf.Max(0f, d - castRadius));
                EndSlamSmooth();
                return;
            }
        }

        // 3) Move
        cc.Move(delta);

        // 4) OverlapCapsule après le move (si on a traversé vite)
        if (Physics.OverlapCapsule(from + delta, to + delta, castRadius, enemyMask, QueryTriggerInteraction.Collide).Length > 0)
        {
            if (debugLogs) Debug.Log("[Pogo] Enemy via OverlapCapsule");
            DoBounce();
            return;
        }

        // Failsafe: si jamais on reste bloqué > 1.5s, on redonne le contrôle
        if (failsafeTimer > handoffFailSafe)
        {
            if (debugLogs) Debug.LogWarning("[Pogo] Failsafe handoff");
            ForceGiveBackControl();
        }
    }

    void DoBounce()
    {
        vVert = Vector3.up * bounceSpeed; // impulsion vers le haut
        // on continue le mode slam pour gérer la verticale, mais on laisse déjà l’air-control
        if (debugLogs) Debug.Log($"[Pogo] BOUNCE! speed={bounceSpeed}");
    }

    void EndSlamSmooth()
    {
        slamming = false;
        landingBlend = true;
        landingT = 0f;
        vVert = Vector3.zero;
        if (debugLogs) Debug.Log("[Pogo] Ground contact -> landing blend");
    }

    void TickLandingBlend()
    {
        landingT += Time.deltaTime / Mathf.Max(landingBlendTime, 0.0001f);
        float t = Mathf.Clamp01(landingT);
        cc.Move(Vector3.down * groundSnap * (1f - t));

        if (t >= 1f)
            ForceGiveBackControl();
    }

    void ForceGiveBackControl()
    {
        landingBlend = false;
        slamming = false;
        if (thirdPersonController && !thirdPersonController.enabled)
            thirdPersonController.enabled = true;   // rend les contrôles
        if (debugLogs) Debug.Log("[Pogo] Control restored");
    }

    // Helpers capsule autour des pieds
    void GetFeetCapsule(out Vector3 a, out Vector3 b)
    {
        Vector3 basePos = feet
            ? feet.position
            : cc.bounds.center + Vector3.down * (cc.height * 0.5f - cc.radius + 0.02f);

        a = basePos + Vector3.up * (castHeight * 0.5f);
        b = basePos - Vector3.up * (castHeight * 0.5f);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!cc) return;
        GetFeetCapsule(out var a, out var b);
        UnityEditor.Handles.color = new Color(0, 1, 1, 0.4f);
        UnityEditor.Handles.DrawWireDisc(a, Vector3.up, castRadius);
        UnityEditor.Handles.DrawWireDisc(b, Vector3.up, castRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(a, b);
    }
#endif
}
