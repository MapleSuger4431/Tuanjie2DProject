using UnityEngine;

public sealed class CoalesceVerification : MonoBehaviour
{
    [SerializeField] private GameObject _prefab;
    [SerializeField] private Rigidbody2D _fallback;

    private void VerifyWithCoalesce()
    {
        Rigidbody2D body = Object.Instantiate(_prefab).GetComponent<Rigidbody2D>();
        Rigidbody2D safe = body ?? _fallback;
        safe.Sleep();
    }

    private void VerifyWithCoalesceAssignment()
    {
        Rigidbody2D body = Object.Instantiate(_prefab).GetComponent<Rigidbody2D>();
        body ??= _fallback;
        body.Sleep();
    }
}
