using UnityEngine;

public class CameraCollision : MonoBehaviour
{
    public Transform pivot;
    public float maxDistance = 5f;
    public float minDistance = 0.5f;
    public float radius = 0.2f;
    public float returnSpeed = 8f;
    public LayerMask collisionMask = ~0;

    private float currentDistance;

    void Start()
    {
        currentDistance = maxDistance;
    }

    void LateUpdate()
    {
        Vector3 direction = -pivot.forward;
        float targetDistance = maxDistance;

        if (Physics.SphereCast(pivot.position, radius, direction, out RaycastHit hit,
                               maxDistance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            targetDistance = Mathf.Clamp(hit.distance, minDistance, maxDistance);
        }

        // Snap in instantly when blocked, ease back out when clear
        if (targetDistance < currentDistance)
            currentDistance = targetDistance;
        else
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, returnSpeed * Time.deltaTime);

        transform.position = pivot.position + direction * currentDistance;
    }
}