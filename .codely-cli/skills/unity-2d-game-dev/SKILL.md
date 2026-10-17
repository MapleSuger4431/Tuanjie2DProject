---
name: unity-2d-game-dev
description: Unity 2D game development expert — Sprite, Tilemap, 2D physics (Box2D), 2D animation, 2D camera, pixel art, 2D lighting. Use when building 2D game frameworks, implementing tilemaps, 2D character controllers, pixel-perfect rendering, or 2D collision detection for RPG/platformer/top-down/card games.
---

# Unity 2D Game Development

## Sprite Setup

```csharp
// SpriteRenderer for display
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteObject : MonoBehaviour
{
    public Sprite sprite;
    void Start() => GetComponent<SpriteRenderer>().sprite = sprite;
}

// Sprite array (animation frames)
public Sprite[] walkFrames;
private SpriteRenderer _sr;
private int _currentFrame;

void AnimateWalk() {
    _sr.sprite = walkFrames[_currentFrame];
    _currentFrame = (_currentFrame + 1) % walkFrames.Length;
}
```

## Tilemap

```
Setup: GameObject > 2D Object > Tilemap > Rectangular
Components: Grid (parent) + Tilemap + Tilemap Renderer + Tilemap Collider 2D
```

```csharp
using UnityEngine.Tilemaps;

public class TilemapManager : MonoBehaviour
{
    public Tilemap groundTilemap;
    public TileBase dirtTile;
    public TileBase grassTile;

    void SetTile(Vector3Int pos, TileBase tile) {
        groundTilemap.SetTile(pos, tile);
    }

    // World position → tile position
    Vector3Int WorldToTile(Vector3 worldPos) {
        return groundTilemap.WorldToCell(worldPos);
    }

    // Check if tile exists
    bool HasTile(Vector3Int pos) {
        return groundTilemap.HasTile(pos);
    }

    // Paint at runtime
    void PaintArea(Vector3Int start, Vector3Int end, TileBase tile) {
        BoundsInt area = new BoundsInt(start, end - start + Vector3Int.one);
        groundTilemap.BoxFill(area.position, tile, area.xMin, area.yMin, area.xMax, area.yMax);
    }
}
```

**Tilemap Collider 2D + Composite Collider 2D**: Add Composite Collider 2D to merge tile colliders into one mesh (massive performance boost).

## 2D Physics

```csharp
[RequireComponent(typeof(Rigidbody2D))]
public class Player2D : MonoBehaviour
{
    private Rigidbody2D _rb;
    public float moveSpeed = 5f;
    public float jumpForce = 7f;

    void Awake() {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 1f;
        _rb.freezeRotation = true; // Prevent toppling
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void Update() {
        float move = Input.GetAxisRaw("Horizontal");
        _rb.velocity = new Vector2(move * moveSpeed, _rb.velocity.y);

        if (Input.GetKeyDown(KeyCode.Space) && IsGrounded())
            _rb.velocity = new Vector2(_rb.velocity.x, jumpForce);
    }

    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float _groundCheckRadius = 0.2f;
    [SerializeField] private Transform _groundCheck;

    bool IsGrounded() {
        return Physics2D.OverlapCircle(_groundCheck.position, _groundCheckRadius, _groundLayer);
    }
}
```

### 2D Colliders
- **BoxCollider2D** — most efficient
- **CircleCollider2D** — rolling objects, characters
- **CapsuleCollider2D** — better for humanoid (no corner catching)
- **PolygonCollider2D** — exact sprite shape (expensive)
- **CompositeCollider2D** — merges multiple colliders (use with Tilemap)

## 2D Camera

```csharp
public class Camera2DFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 0.125f;
    public Vector3 offset = new Vector3(0, 0, -10);

    void LateUpdate() {
        Vector3 desired = target.position + offset;
        Vector3 smoothed = Vector3.Lerp(transform.position, desired, smoothSpeed);
        transform.position = smoothed;
    }
}

// Pixel-perfect camera (URP package)
// Add "Pixel Perfect Camera" component:
//   Assets Pixels Per Unit: 32 (for 32x32 sprites)
//   Reference Resolution: 320x180
//   Pixel Snapping: ✓
```

## 2D Animation

```csharp
// Animator with 2D sprites
// Create: Sprite Sheet → Slice in Sprite Editor → Drag frames into Animator

// Frame-based animation (no Animator, lightweight)
public class FrameAnimator : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 8f;
    private SpriteRenderer _sr;
    private float _timer;

    void Update() {
        _timer += Time.deltaTime;
        int frame = (int)(_timer * fps) % frames.Length;
        _sr.sprite = frames[frame];
    }
}
```

## Pixel-Perfect Rendering

1. Set `Filter Mode: Point (no filter)` on sprite textures.
2. Set `Compression: None` on pixel art textures.
3. Use `Pixel Perfect Camera` component (URP 2D package).
4. Set `Pixels Per Unit` consistently (e.g., 16 or 32).
5. Camera `Orthographic Size = Screen Height / (2 * PPU)`.

## 2D Lighting (URP 2D)

```
1. URP Asset → Renderer: 2D Renderer
2. Add Light 2D component:
   - Light Type: Point / Sprite / Parametric / Freeform
   - Intensity, Color, Falloff
3. Add "Shadow Caster 2D" for shadows
4. Sort Order controls light layering
```

## 2D Collision Detection

```csharp
// Overlap area (trigger detection)
Collider2D[] hits = Physics2D.OverlapAreaAll(minPoint, maxPoint);

// Raycast 2D
RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.up, 5f, layerMask);
if (hit.collider != null)
    Debug.Log($"Hit: {hit.collider.name}");

// Circle overlap (melee attack)
Collider2D[] enemies = Physics2D.OverlapCircleAll(attackPoint.position, 1.5f, enemyLayer);
foreach (var enemy in enemies)
    enemy.GetComponent<Enemy2D>().TakeDamage();
```

## Key Gotchas

1. **Rigidbody2D vs Rigidbody**: 2D physics is completely separate from 3D. Don't mix.
2. **Z-axis**: In 2D, Z is for sorting (SpriteRenderer.sortingOrder), not position.
3. **Box2D limits**: Max 1000 dynamic 2D colliders per scene. Use Composite Collider 2D for tilemaps.
4. **Sprite Atlas**: Use Sprite Atlas to reduce draw calls (batching). `Create > 2D > Sprite Atlas`.
5. **Sorting Layer**: Set up sorting layers in `Project Settings > Graphics > Custom Sorting Layers`. Use `sortingOrder` for fine control within a layer.
6. **Camera Orthographic**: 2D cameras must be `Orthographic`, not `Perspective`.
7. **Pixel snapping**: Without it, pixel art wobbles at non-integer positions. Use Pixel Perfect Camera.
