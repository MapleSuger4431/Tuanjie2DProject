using System;
using UnityEngine;

/// <summary>
/// Regression fixture for the UR048/UR049/UR050 exemptions. Nothing here may be reported: each
/// lookup either proves the component exists or fails loudly with a configuration error that
/// names the prefab and the component.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class VerifiedLookups : MonoBehaviour
{
    [SerializeField] private GameObject _projectilePrefab;

    // Same GameObject and guaranteed by [RequireComponent].
    private void UseOwnBody()
    {
        GetComponent<Rigidbody2D>().Sleep();
    }

    // TryGetComponent success branch.
    private void FireIfPresent()
    {
        GameObject spawned = UnityEngine.Object.Instantiate(_projectilePrefab);
        if (spawned.TryGetComponent(out Rigidbody2D body))
        {
            body.Sleep();
        }
    }

    // Null-conditional access never dereferences a missing component.
    private void FireOptional()
    {
        UnityEngine.Object.Instantiate(_projectilePrefab).GetComponent<Rigidbody2D>()?.Sleep();
    }

    // A guard that surfaces the configuration error instead of silently returning.
    private void FireOrFailLoudly()
    {
        GameObject spawned = UnityEngine.Object.Instantiate(_projectilePrefab);
        Rigidbody2D body = spawned.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            throw new InvalidOperationException(
                $"Prefab '{_projectilePrefab.name}' is missing Rigidbody2D; fix the prefab component contract.");
        }
        body.Sleep();
    }

    // Narrow reasoned suppression for a lookup the contract cannot express.
    private void FireExempt()
    {
        GameObject spawned = UnityEngine.Object.Instantiate(_projectilePrefab);
        // ComponentLookupExemption: reason=body supplied by the editor-only debug rig, absent from shipped prefabs
        Rigidbody2D body = spawned.GetComponent<Rigidbody2D>();
        body.Sleep();
    }
}
