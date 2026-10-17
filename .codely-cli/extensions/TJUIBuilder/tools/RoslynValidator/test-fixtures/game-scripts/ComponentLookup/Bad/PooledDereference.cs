using UnityEngine;

public sealed class PooledDereference : MonoBehaviour
{
    [SerializeField] private GameObject _prefab;

    private void FireUnverified()
    {
        GameObject spawned = UnityEngine.Object.Instantiate(_prefab);
        spawned.GetComponent<Rigidbody2D>().Sleep();
    }

    private void FireUnverifiedAtPose()
    {
        GameObject spawned = UnityEngine.Object.Instantiate(_prefab, transform.position, transform.rotation);
        spawned.GetComponent<Rigidbody2D>().Sleep();
    }

    private void FireSilentGuard()
    {
        GameObject spawned = UnityEngine.Object.Instantiate(_prefab);
        Rigidbody2D body = spawned.GetComponent<Rigidbody2D>();
        if (body == null) return; // silent guard: missing component downgrades into a no-op
        body.Sleep();
    }
}
