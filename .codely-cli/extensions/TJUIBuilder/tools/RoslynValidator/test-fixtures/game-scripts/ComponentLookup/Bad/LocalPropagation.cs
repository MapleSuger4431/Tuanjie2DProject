using UnityEngine;

public sealed class LocalPropagation : MonoBehaviour
{
    [SerializeField] private GameObject _prefab;

    private void TwoHop()
    {
        GameObject first = Object.Instantiate(_prefab);
        GameObject second = first;
        second.GetComponent<Rigidbody2D>().Sleep();
    }
    private void ThreeHop()
    {
        GameObject first = Object.Instantiate(_prefab);
        GameObject second = first;
        GameObject third = second;
        third.GetComponent<Rigidbody2D>().Sleep();
    }
    private void BeyondBoundary()
    {
        GameObject first = Object.Instantiate(_prefab);
        GameObject second = first;
        GameObject third = second;
        GameObject fourth = third;
        fourth.GetComponent<Rigidbody2D>().Sleep();
    }
}
