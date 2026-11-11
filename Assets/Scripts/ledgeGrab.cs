using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Standalone ledge grab. Add it to your player next to your existing movement script.
/// Grab a ledge while falling, then press Jump to hop up and over it (or Drop to let go).
/// While hanging / ledge-jumping it disables your movement script, then hands control back.
/// Works with either a CharacterController or a non-kinematic Rigidbody. Pivot should be at the feet.
/// </summary>
public class LedgeGrab : MonoBehaviour
{
    [Header("Your Existing Setup")]
    [Tooltip("Drag your movement script here. It is disabled while on a ledge.")]
    public MonoBehaviour movementScript;
    [Tooltip("The object that actually rotates to face the movement direction (usually your character model). Leave empty to use this object.")]
    public Transform facing;

    [Header("Detection")]
    public LayerMask ledgeLayers = ~0;         // Environment layers only - exclude the Player layer
    public float wallCheckHeight = 1.2f;       // Height above feet of the forward wall ray
    public float wallCheckDistance = 0.7f;
    public float minLedgeHeight = 1.5f;        // Lowest ledge top (above feet) you can grab
    public float maxLedgeHeight = 2.2f;        // Highest ledge top (above feet) you can grab
    public float grabCooldown = 0.4f;
    public float standClearanceOffset = 0.4f;  // How far onto the ledge we check for standing room

    [Header("Ground Check (used to only grab while airborne)")]
    public float groundCheckDistance = 0.25f;

    [Header("Hanging")]
    public float handsHeight = 1.8f;           // Feet-to-hands distance; ledge top sits here when hanging
    public float hangDistanceFromWall = 0.35f;
    public float snapDuration = 0.15f;

    [Header("Ledge Jump")]
    public float jumpClearance = 0.8f;         // How far above the ledge top the jump rises
    public float jumpForwardSpeed = 4f;        // Forward speed once you're above the ledge
    [Tooltip("Only used with CharacterController. Match your movement script's gravity (negative). Rigidbody uses Physics.gravity.")]
    public float ledgeJumpGravity = -20f;

    [Header("Input (swap for the new Input System if needed)")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode dropKey = KeyCode.LeftControl;

    [Header("Events (hook up animations, sounds, camera changes)")]
    public UnityEvent onGrab;
    public UnityEvent onLedgeJump;
    public UnityEvent onDrop;

    public bool IsHanging => state != State.Free;

    private enum State { Free, Transition, Hanging }

    private State state = State.Free;
    private CharacterController cc;
    private Rigidbody rb;
    private float nextGrabTime;
    private float lastY;
    private float velocityY;

    private Vector3 ledgeTopPoint;
    private Vector3 wallNormal;

    // Flattened direction the player is currently facing
    Vector3 FacingDir => Vector3.ProjectOnPlane(facing.forward, Vector3.up).normalized;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();
        if (facing == null) facing = transform;
        lastY = transform.position.y;
    }

    void Update()
    {
        // Track vertical velocity ourselves so this works with any movement setup
        velocityY = (transform.position.y - lastY) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastY = transform.position.y;

        if (state == State.Free)
            TryGrabLedge();
        else if (state == State.Hanging)
            HandleHangInput();
    }

    // ---------------------------------------------------------------- Detection

    bool IsGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        return Physics.Raycast(origin, Vector3.down, 0.1f + groundCheckDistance,
            ledgeLayers, QueryTriggerInteraction.Ignore);
    }

    void TryGrabLedge()
    {
        // Only grab while airborne and falling (or near the peak of a jump)
        if (Time.time < nextGrabTime || velocityY > 0.5f || IsGrounded())
            return;

        // 1) Wall in front at chest height?
        Vector3 wallOrigin = transform.position + Vector3.up * wallCheckHeight;
        if (!Physics.Raycast(wallOrigin, FacingDir, out RaycastHit wallHit,
                wallCheckDistance, ledgeLayers, QueryTriggerInteraction.Ignore))
            return;

        if (Mathf.Abs(wallHit.normal.y) > 0.3f)   // must be roughly vertical
            return;

        // 2) Cast down from above/inside the wall to find the ledge top
        Vector3 topOrigin = wallHit.point - wallHit.normal * 0.1f;
        topOrigin.y = transform.position.y + maxLedgeHeight;
        float castDistance = maxLedgeHeight - minLedgeHeight;

        if (!Physics.Raycast(topOrigin, Vector3.down, out RaycastHit topHit,
                castDistance, ledgeLayers, QueryTriggerInteraction.Ignore))
            return;

        if (topHit.normal.y < 0.7f)               // top must be roughly flat
            return;

        // 3) Room to stand on top?
        float radius = cc != null ? cc.radius : 0.3f;
        float height = cc != null ? cc.height : 1.8f;
        Vector3 standPoint = topHit.point - wallHit.normal * standClearanceOffset;
        Vector3 bottom = standPoint + Vector3.up * (radius + 0.05f);
        Vector3 top = standPoint + Vector3.up * (height - radius);
        if (Physics.CheckCapsule(bottom, top, radius * 0.9f, ledgeLayers, QueryTriggerInteraction.Ignore))
            return;

        ledgeTopPoint = topHit.point;
        wallNormal = wallHit.normal;
        StartCoroutine(GrabRoutine());
    }

    // ---------------------------------------------------------------- Take / release control

    void TakeControl()
    {
        if (movementScript != null) movementScript.enabled = false;
        if (cc != null) cc.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;   // Unity 6. On older versions use rb.velocity
            rb.isKinematic = true;
        }
    }

    void ReleaseControl(bool keepVelocity = false)
    {
        if (cc != null) cc.enabled = true;
        if (rb != null)
        {
            rb.isKinematic = false;
            if (!keepVelocity) rb.linearVelocity = Vector3.zero;   // Unity 6. On older versions use rb.velocity
        }
        if (movementScript != null) movementScript.enabled = true;

        lastY = transform.position.y;
        velocityY = 0f;
        nextGrabTime = Time.time + grabCooldown;
        state = State.Free;
    }

    // ---------------------------------------------------------------- Grab / hang / jump / drop

    Vector3 GetHangPosition()
    {
        Vector3 pos = ledgeTopPoint + wallNormal * hangDistanceFromWall;
        pos.y = ledgeTopPoint.y - handsHeight;
        return pos;
    }

    IEnumerator GrabRoutine()
    {
        state = State.Transition;
        TakeControl();
        onGrab?.Invoke();

        Vector3 startPos = transform.position;
        Quaternion startRot = facing.rotation;
        Vector3 hangPos = GetHangPosition();
        Quaternion hangRot = Quaternion.LookRotation(-wallNormal, Vector3.up);

        for (float t = 0f; t < 1f; t += Time.deltaTime / snapDuration)
        {
            transform.position = Vector3.Lerp(startPos, hangPos, t);
            facing.rotation = Quaternion.Slerp(startRot, hangRot, t);
            yield return null;
        }

        transform.position = hangPos;
        facing.rotation = hangRot;
        state = State.Hanging;
    }

    void HandleHangInput()
    {
        if (Input.GetKeyDown(jumpKey))
            StartCoroutine(LedgeJumpRoutine());
        else if (Input.GetKeyDown(dropKey))
            Drop();
    }

    IEnumerator LedgeJumpRoutine()
    {
        state = State.Transition;
        onLedgeJump?.Invoke();

        // Physics back on, but your movement script stays off until the jump is done
        if (cc != null) cc.enabled = true;
        if (rb != null) rb.isKinematic = false;

        float g = rb != null ? Physics.gravity.y : ledgeJumpGravity;

        // Launch speed needed to rise from the hang position to just above the ledge top
        float riseHeight = (ledgeTopPoint.y - transform.position.y) + jumpClearance;
        float vy = Mathf.Sqrt(2f * Mathf.Abs(g) * riseHeight);
        Vector3 forward = -wallNormal;

        if (rb != null) rb.linearVelocity = Vector3.up * vy;   // Unity 6. On older versions use rb.velocity

        bool clearedLedge = false;
        float timeout = Time.time + 2f;

        while (Time.time < timeout)
        {
            // Only move forward once the feet are above the ledge, so we don't push into the wall
            if (!clearedLedge && transform.position.y >= ledgeTopPoint.y - 0.05f)
                clearedLedge = true;

            // How far past the ledge edge we are (measured away from the wall)
            float pastEdge = Vector3.Dot(transform.position - ledgeTopPoint, forward);
            bool onTop = pastEdge >= standClearanceOffset;

            // Keep moving forward until we're properly over the edge
            Vector3 horizontal = (clearedLedge && !onTop) ? forward * jumpForwardSpeed : Vector3.zero;

            if (cc != null)
            {
                vy += g * Time.deltaTime;
                CollisionFlags flags = cc.Move((horizontal + Vector3.up * vy) * Time.deltaTime);
                if ((flags & CollisionFlags.Above) != 0 && vy > 0f) vy = 0f;
            }
            else
            {
                vy = rb.linearVelocity.y;
                rb.linearVelocity = horizontal + Vector3.up * vy;
            }

            // Hand control back once we're falling AND (over the edge, landed, or never made it up)
            if (vy <= 0f && (!clearedLedge || onTop || IsGrounded()))
                break;

            yield return rb != null && cc == null ? new WaitForFixedUpdate() : null;
        }

        ReleaseControl(keepVelocity: true);
    }

    void Drop()
    {
        ReleaseControl();
        onDrop?.Invoke();
    }

    // ---------------------------------------------------------------- Debug

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 origin = transform.position + Vector3.up * wallCheckHeight;
        Transform f = facing != null ? facing : transform;
        Vector3 dir = Vector3.ProjectOnPlane(f.forward, Vector3.up).normalized;
        Gizmos.DrawLine(origin, origin + dir * wallCheckDistance);

        Gizmos.color = Color.cyan;
        Vector3 lo = transform.position + dir * wallCheckDistance;
        Gizmos.DrawLine(lo + Vector3.up * minLedgeHeight, lo + Vector3.up * maxLedgeHeight);
    }
}