using UnityEngine;

// A resting animal (the cattle at the kamatha): its Idle clip starts at a random point and runs a
// little faster or slower, so two animals never move in step.
public class IdleAnimal : MonoBehaviour
{
    void Start()
    {
        var animator = GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.Play("Idle", 0, Random.value);
        animator.speed = Random.Range(0.85f, 1.1f);
    }
}
