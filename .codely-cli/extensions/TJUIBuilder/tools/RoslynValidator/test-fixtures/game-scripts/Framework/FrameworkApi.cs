using UnityEngine;

/// <summary>
/// Minimal in-source mirror of the Create.GameFramework API surface the UR fixtures exercise.
/// UR038 identifies framework API by namespace and fully qualified name, so fixtures
/// compile against this file instead of depending on the host project's compiled package DLL.
/// The framework's only public *Event type today is GameStateChangedEvent (UI state contract,
/// published via UIFrame.SetGameState — there is no game-side event bus in this package).
/// </summary>
namespace Create.GameFramework
{
    public struct GameStateChangedEvent { }
}
