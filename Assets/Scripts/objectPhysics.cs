using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Pushable : MonoBehaviour
{
    [Tooltip("Multiplier for how strongly the player can push this object")]
    public float pushStrength = 1f;
}