using UnityEngine;
using static UnityEngine.UI.Image;

public class GrabController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform holdPoint;      // empty child of the camera, placed in front of it
    [SerializeField] Camera cam;

    [Header("Settings")]
    [SerializeField] float grabRange = 3f;
    [SerializeField] float followSpeed = 15f;
    [SerializeField] float throwForce = 12f;
    [SerializeField] LayerMask grabbableLayers = ~0;

    Rigidbody held;

    Vector3 AimDirection(Vector3 origin)
    {
        Ray camRay = new Ray(cam.transform.position, cam.transform.forward);
        Vector3 target = camRay.GetPoint(50f); // default: a far point straight ahead

        // Find the nearest thing under the crosshair, ignoring the character
        float nearest = float.MaxValue;
        foreach (RaycastHit h in Physics.RaycastAll(camRay, 50f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < nearest) { nearest = h.distance; target = h.point; }
        }

        return (target - origin).normalized;
    }


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            //Debug.Log("E pressed. Holding something? " + (held != null));

            if (held == null) TryGrab();
            else Drop();
        }

        if (held != null && Input.GetMouseButtonDown(0))
            Throw();
    }


    void FixedUpdate()
    {
        if (held == null) return;

        // Move toward the hold point using velocity so physics/collisions still work
        Vector3 toTarget = holdPoint.position - held.position;
        held.linearVelocity = toTarget * followSpeed;
        held.angularVelocity = Vector3.zero;
    }

    void TryGrab()
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 dir = AimDirection(origin);
        //Debug.DrawRay(origin, dir * grabRange, Color.red, 2f);

        // Get every hit along the cast, nearest first
        RaycastHit[] hits = Physics.SphereCastAll(origin, 0.3f, dir, grabRange, grabbableLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // Skip the character's own colliders (this object and all its children)
            if (hit.collider.transform.IsChildOf(transform)) continue;

            //Debug.Log("Cast hit: " + hit.collider.name);

            //if (hit.rigidbody == null) { Debug.Log("No Rigidbody on " + hit.collider.name); continue; }
            //if (hit.rigidbody.isKinematic) { Debug.Log(hit.collider.name + " is kinematic"); continue; }

            held = hit.rigidbody;
            held.useGravity = false;
            held.linearDamping = 10f;
            //Debug.Log("Grabbed " + held.name);
            return;
        }

        //Debug.Log("Nothing grabbable in range.");
    }

    void Drop()
    {
        held.useGravity = true;
        held.linearDamping = 0f;
        held = null;
    }

    void Throw()
    {
        Rigidbody r = held;
        Drop();
        r.AddForce(AimDirection(transform.position + Vector3.up * 0.5f) * throwForce, ForceMode.Impulse);
    }
}