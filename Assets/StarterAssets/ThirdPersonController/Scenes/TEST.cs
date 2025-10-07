using UnityEngine;
using UnityEngine.InputSystem;
// si Unity Starter Assets est en namespace, décommente :
// using StarterAssets;

public class AttackTrigger : MonoBehaviour
{
    public Animator animator;
    public MonoBehaviour controller; // référence au ThirdPersonController
    public float attackDuration = 1.0f;

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!controller) controller = GetComponent(typeof(MonoBehaviour)) as MonoBehaviour; // ton ThirdPersonController
    }

    void Update()
    {
        bool pressed = (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame)
                       || Input.GetMouseButtonDown(0);

        if (pressed)
        {
            animator.SetTrigger("Attack");
            if (controller) controller.enabled = false;   // option : gèle le déplacement
            Invoke(nameof(ReenableMove), attackDuration);
        }
    }

    void ReenableMove()
    {
        if (controller) controller.enabled = true;
    }
}
