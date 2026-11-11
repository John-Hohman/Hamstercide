using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerPusher : MonoBehaviour
{
    public float pushPower = 3f;

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        // Ignore objects without a Rigidbody or kinematic ones
        if (body == null || body.isKinematic) return;

        // Don't push things downward (e.g. when standing on them)
        if (hit.moveDirection.y < -0.3f) return;

        // Push horizontally only
        Vector3 dir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);

        float strength = pushPower;
        if (body.TryGetComponent(out Pushable p)) strength *= p.pushStrength;

        body.linearVelocity = dir * strength;   // use linearVelocity in Unity 6+
    }
}