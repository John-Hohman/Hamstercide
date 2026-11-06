using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 6f;
    public float jumpHeight = 1.5f;
    public float gravity = -20f;

    [Header("Camera")]
    public Transform cameraPivot;
    public float sensitivity = 2f;
    public float minPitch = -30f;       // how far you can look up
    public float maxPitch = 60f;
    public float followSmoothTime = 0.1f;
    public Vector3 pivotOffset = new Vector3(0f, 0.5f, 0f);
    private Vector3 followVelocity;

    [Header("Model")]
    public Transform model;
    public float turnSpeed = 12f;
    private CharacterController controller;
    private Vector3 velocity;
    private float yaw;
    private float pitch;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        Move();

    }

    void LateUpdate()
    {
        Look();

    }


    void Look()
    {
        yaw += Input.GetAxis("Mouse X") * sensitivity;
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sensitivity, minPitch, maxPitch);

        // World rotation now, since the pivot is no longer a child of the Player
        cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Smoothly chase the player
        Vector3 targetPosition = transform.position + pivotOffset;
        cameraPivot.position = Vector3.SmoothDamp(cameraPivot.position, targetPosition,
                                                  ref followVelocity, followSmoothTime);
    }

    void Move()
    {
        // Cache grounded state before moving
        bool grounded = controller.isGrounded;

        if (grounded && velocity.y < 0f)
            velocity.y = -2f;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        // Movement directions based on where the camera is looking (yaw only)
        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 move = yawRotation * new Vector3(x, 0f, z);
        if (move.magnitude > 1f) move.Normalize();

        if (move.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move);
            model.rotation = Quaternion.Slerp(model.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        if (Input.GetButtonDown("Jump") && grounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;

        controller.Move((move * speed + velocity) * Time.deltaTime);
    }
}