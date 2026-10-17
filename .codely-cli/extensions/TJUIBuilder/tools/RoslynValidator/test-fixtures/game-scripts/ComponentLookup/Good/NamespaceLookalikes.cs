using UnityEngine;

namespace UserPools
{
    public sealed class GameObjectPool { public GameObject Get() => new GameObject(); }
    public sealed class ObjectPool<T> where T : new() { public T Get() => new T(); }
    public static class Factory { public static GameObject Instantiate(GameObject prefab) => prefab; }
}

public sealed class NamespaceLookalikes : MonoBehaviour
{
    private readonly UserPools.GameObjectPool _gameObjects = new UserPools.GameObjectPool();
    private readonly UserPools.ObjectPool<Rigidbody2D> _components = new UserPools.ObjectPool<Rigidbody2D>();
    [SerializeField] private GameObject _prefab;

    private void UseLookalikes()
    {
        _gameObjects.Get().GetComponent<Rigidbody2D>().Sleep();
        _components.Get().Sleep();
        UserPools.Factory.Instantiate(_prefab).GetComponent<Rigidbody2D>().Sleep();
    }
}
