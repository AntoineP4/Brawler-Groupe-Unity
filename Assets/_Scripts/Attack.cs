using UnityEngine;
using UnityEngine.InputSystem;

public class AttackTrigger : MonoBehaviour
{
    public Animator animator;
    public MonoBehaviour controller;
    public string attackStateName = "Attack";
    public float attackDuration = 0.7f;
    public float attackCooldown = 0.1f;

    bool isAttacking;
    float t;
    float cd;
    int attackHash;

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!controller) controller = GetComponent<MonoBehaviour>();
        attackHash = Animator.StringToHash(attackStateName);
    }

    void Update()
    {
        if (cd > 0f) cd -= Time.deltaTime;

        bool pressed = (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame) || Input.GetMouseButtonDown(0);
        if ((isAttacking || cd > 0f) && pressed) pressed = false;

        if (!isAttacking && cd <= 0f && pressed)
        {
            isAttacking = true;
            t = attackDuration;
            cd = attackCooldown + attackDuration;
            if (animator) animator.CrossFadeInFixedTime(attackHash, 0f, 0, 0f);
            if (controller) controller.enabled = false;
        }

        if (isAttacking)
        {
            t -= Time.deltaTime;
            if (t <= 0f)
            {
                isAttacking = false;
                if (controller) controller.enabled = true;
            }
        }
    }
}
