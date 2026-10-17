using UnityEngine;

/// <summary>
/// Regression fixture for UR049: a lookup on a foreign GameObject (not pooled, not freshly
/// instantiated) is dereferenced without verification. The origin is only "other", so it must
/// remain a Warning, never an Error.
/// </summary>
public class CrossObjectLookup : MonoBehaviour
{
    [SerializeField] private Transform _target;

    // UR049 — cross-object lookup, component assumed present.
    private void Update()
    {
        Rigidbody2D body = _target.GetComponent<Rigidbody2D>();
        body.velocity = Vector2.up;
    }
}
