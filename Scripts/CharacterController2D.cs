using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class CharacterController2D : MonoBehaviour {
    public GroundCheckSettings groundCheckSettings;
    public MovementSettings movementSettings;
    public ExternalForcesSettings externalForcesSettings;
    public SlopeSettings slopeSettings;
    public JumpSettings jumpSettings;
    public WallJumpSettings wallJumpSettings;
    public DashSettings dashSettings;
    public StepSettings stepSettings;
    public TopEdgeNudgeSettings topEdgeNudgeSettings;
    public MovingPlatformsSettings movingPlatformsSettings;

    Rigidbody2D rb;
    Collider2D coll;

    private void Awake() {
        // Init variables
        rb = GetComponent<Rigidbody2D>();
        coll = GetComponent<Collider2D>();

        SetSlipperyMaterial(); // Add Slippery Material
        CheckRigidbody();
    }

    private void Update() {
        SmoothInput(); // Refresh smooth input
        
        jumpTimer -= Time.deltaTime; // Reduce jump timer

        if (dashSettings.canDash) {
            if (dashCooldownTimer > 0 && dashCooldownTimer - Time.deltaTime <= 0) {
                if (dashesLeft > 0) dashRechargedEvent?.Invoke();
                else notifyDashRechargeOnGrounded = true;
            }

            dashTimer -= Time.deltaTime;
            dashCooldownTimer -= Time.deltaTime;
        }
    }

    private void FixedUpdate() {
        rb.velocity /= externalForcesSettings.externalForceFriction; // Reduce external forces

        Collider2D groundCollider = CheckGrounded(); // Ground check

        if (isGrounded) {
            dashesLeft = dashSettings.dashesInAir; // Reset dashes left on grounded
            if (notifyDashRechargeOnGrounded) { // Dash recharged event
                notifyDashRechargeOnGrounded = false;
                dashRechargedEvent?.Invoke();
            }

            jumpsLeft = jumpSettings.jumpsInAir; // Reset jumps left on grounded
        }

        CheckForMovingPlatform(groundCollider); // Moveable platform check
        MoveToMovingPlatform(); // Move with platform

        CalculateRealVelocity(); // Velocity
        ApplyGravity(); // Gravity (AFTER Velocity)
        
        ApplyStepForce(); // Apply step force (AFTER Gravity)

        if (movementSettings.canMove && jumpTimer > 0) Jump(); // Jump
        if (movementSettings.canMove && dashSettings.canDash && dashTimer > 0) Dash(); // Dash
        
        inputVelocity.x = movementSettings.canMove ? (smoothMovementInput.x * GetMovementSpeed()) : 0; // Horizontal movement
        walkSpeed = (movementSettings.canMove && velocity.x != 0) ? smoothMovementInput.x : 0; // No walk speed when running at wall or cant move

        CheckWallJumping(); // Check if wall jumping

        SlideDown(); // Slide down slopes / Stick to ground

        ApplyTopEdgeForce(); // Top edge detection (AFTER inputVelocity.x set)

        ApplyWallJumpGravity(); // Slide down walls

        rb.MovePosition(rb.position + (GetFinalVelocity() + rb.velocity) * Time.fixedDeltaTime); // Apply velocity

        SetMovingPlatformOffset(); // Set new platform offset (AFTER applying velocity)

        FaceInRightDirection(); // Face in right direction
    }

    private void OnValidate() {
        coll = GetComponent<Collider2D>();

        // Get Bounds
        if (groundCheckSettings.automaticCheckBox) SetGroundcheckParameters();

        // Get Step Settings
        if (stepSettings.automaticConfiguration) SetStepCheckParameters();

        // Get Top Edge Settings
        if (topEdgeNudgeSettings.automaticConfiguration) SetTopEdgeParameters();
    }

    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.green;
        DrawGroundcheckDebug();

        Gizmos.color = Color.yellow;
        DrawWallJumpCheckDebug();

        Gizmos.color = Color.red;
        DrawStepDebug();

        Gizmos.color = Color.blue;
        DrawTopEdgeDebug();

        Gizmos.color = Color.magenta;
        DrawSlopeDebug();
    }

    #region Input
    [HideInInspector] public Vector2 movementInput;
    
    public void OnJumpButton() => jumpTimer = jumpReminderTime;
    public void OnDashButton() => dashTimer = dashReminderTime;
    #endregion

    #region Events and public values
    /// <summary>
    /// Actual walk speed. Changes when near apex of jump.
    /// </summary>
    [HideInInspector] public float walkSpeed { get; private set; } = 0;
    [HideInInspector] public bool isGrounded { get; private set; } = false;
    [HideInInspector] public bool isFalling { get; private set; } = false;
    [HideInInspector] public bool isWallJumping { get; private set; } = false;
    [HideInInspector] public bool isOnMovingPlatform { get { return currentPlatform != null; } }
    [HideInInspector] public int jumpsLeft;
    /// <summary>
    /// Actual velocity (readonly). If you want to add external forces, use Rigidbody2D.AddForce
    /// </summary>
    [HideInInspector] public Vector2 velocity { get; private set; } = Vector2.zero;

    [HideInInspector] public UnityEvent jumpEvent;
    [HideInInspector] public UnityEvent groundEvent;
    [HideInInspector] public UnityEvent wallGrabEvent;
    [HideInInspector] public UnityEvent dashEvent;
    [HideInInspector] public UnityEvent dashRechargedEvent;
    [HideInInspector] public UnityEvent stepEvent;
    [HideInInspector] public UnityEvent<Vector2> topEdgeEvent;
    #endregion

    #region Movement
    Vector2 inputVelocity = Vector2.zero;
    Vector2 smoothMovementInput = Vector2.zero;

    Vector2 lastPos = Vector2.zero;

    private void SmoothInput() {
        smoothMovementInput.y = movementInput.y;

        if (movementSettings.instantDirectionChange && (movementInput.x > 0 && smoothMovementInput.x < 0) || (movementInput.x < 0 && smoothMovementInput.x > 0))
            smoothMovementInput.x = 0; // Instant direction change

        if (movementInput.x == 0 && smoothMovementInput.x != 0) {
            float deceleration = ((smoothMovementInput.x > 0) ? movementSettings.movementDeceleration : -movementSettings.movementDeceleration) * Time.deltaTime;
            if (smoothMovementInput.x - deceleration > 0 != smoothMovementInput.x > 0) smoothMovementInput.x = 0; // Dont decelerate in other direction
            else smoothMovementInput.x -= deceleration; // Decelerate
        }

        smoothMovementInput.x += movementInput.x * movementSettings.movementAcceleration * Time.deltaTime; // Accelerate
        smoothMovementInput.x = Mathf.Clamp(smoothMovementInput.x, -1, 1); // Clamp speed
    }

    private float GetMovementSpeed() {
        if (isGrounded) return movementSettings.speedOnGround;

        return Mathf.Lerp(jumpSettings.apexSpeed, movementSettings.speedOnGround, Mathf.Abs(inputVelocity.y) / jumpSettings.apexSpeedThreshold); // Jump apex -> More Speed
    }

    private void CalculateRealVelocity() {
        velocity = ((Vector2)transform.position - lastPos) / Time.fixedDeltaTime;
        lastPos = transform.position;
    }

    /// <summary>
    /// Reset whole velocity (Rigidbody2D.velocity included)
    /// </summary>
    public void ResetVelocity() {
        inputVelocity = Vector2.zero;
        rb.velocity = Vector2.zero;
    }

    private void FaceInRightDirection() {
        if (smoothMovementInput.x == 0) return;

        if ((transform.localScale.x > 0) != (smoothMovementInput.x > 0)) {
            transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
        }
    }
    #endregion

    #region Slope
    void SlideDown() {
        // If not jumping
        if (inputVelocity.y > 0) return;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, Mathf.Infinity, groundCheckSettings.groundLayer);

        // If slope is bigger than treshold (Slide down) or x input it not null (Stick to ground)
        if (Mathf.Abs(hit.normal.x) > slopeSettings.slideTreshold || (inputVelocity.x != 0 && isGrounded)) inputVelocity.y = -slopeSettings.slideForce;
    }

    void DrawSlopeDebug() {
        Vector2 origin = transform.position + Vector3.up * groundCheckSettings.checkBoxOffset.y * transform.localScale.y;
        Gizmos.DrawLine(origin, origin + (Vector2.up * slopeSettings.slideTreshold + Vector2.right) * 0.2f);
    }
    #endregion

    #region Ground Check
    bool wasGrounded = false;

    private Collider2D CheckGrounded() {
        Collider2D groundCollider = Physics2D.OverlapBox((Vector2) coll.bounds.center + groundCheckSettings.checkBoxOffset, groundCheckSettings.checkBoxSize, 0, groundCheckSettings.groundLayer);

        wasGrounded = isGrounded;
        isGrounded = groundCollider != null;

        if (!wasGrounded && isGrounded) groundEvent?.Invoke();
        if (wasGrounded && !isGrounded && inputVelocity.y < 0) inputVelocity.y = 0; // Reset default velocity

        isFalling = !isGrounded && inputVelocity.y < 0;

        return groundCollider;
    }

    private void SetGroundcheckParameters() {
        groundCheckSettings.checkBoxOffset = Vector2.down * coll.bounds.extents.y;
        groundCheckSettings.checkBoxSize = new Vector2(coll.bounds.extents.x * 2 - 0.025f, 0.01f);
    }

    private void DrawGroundcheckDebug() {
        Gizmos.DrawWireCube(coll.bounds.center + (Vector3)groundCheckSettings.checkBoxOffset, groundCheckSettings.checkBoxSize);
    }
    #endregion

    #region Jump
    const float jumpReminderTime = 0.1f;
    float jumpTimer = 0;

    private void Jump() {
        if (jumpsLeft <= 0) return;

        jumpsLeft--;
        jumpTimer = 0;
        inputVelocity.y = Mathf.Sqrt(jumpSettings.jumpForce * -2f * jumpSettings.gravity);

        // If is wall jumping: add force in opposite direction to wall
        if (wasWallJumping || isWallJumping) {
            rb.AddForce(new Vector2((transform.localScale.x > 0 ? -1 : 1) * wallJumpSettings.wallJumpAwayForce, 0), ForceMode2D.Impulse);
        }

        jumpEvent?.Invoke();
    }
    #endregion

    #region WallJump
    bool wasWallJumping = false;

    void CheckWallJumping() {
        if (isGrounded) {
            isWallJumping = false;
            return;
        }
        
        Collider2D wallCollider = Physics2D.OverlapBox(
            (Vector2)coll.bounds.center + new Vector2((transform.localScale.x > 0 ? 1 : -1) * wallJumpSettings.wallJumpCheckBoxOffset.x, wallJumpSettings.wallJumpCheckBoxOffset.y),
            wallJumpSettings.wallJumpCheckBoxSize, 0, groundCheckSettings.groundLayer
        );

        wasWallJumping = isWallJumping;
        isWallJumping = wallCollider != null;

        if (!wasWallJumping && isWallJumping) {
            wallGrabEvent?.Invoke();

            jumpsLeft = wallJumpSettings.restoreFullJumps ? jumpSettings.jumpsInAir : 1;
        }
    }

    void ApplyWallJumpGravity() {
        if (!isWallJumping || inputVelocity.y > 0) return;

        inputVelocity.y = wallJumpSettings.wallJumpDownGravity;
    }

    void DrawWallJumpCheckDebug() {
        Gizmos.DrawWireCube((Vector2)coll.bounds.center + wallJumpSettings.wallJumpCheckBoxOffset, wallJumpSettings.wallJumpCheckBoxSize);
    }
    #endregion

    #region Dash
    bool notifyDashRechargeOnGrounded = false;
    const float dashReminderTime = 0.1f;
    float dashTimer = 0;
    float dashCooldownTimer = 0;
    float dashesLeft = 0;

    private void Dash() {
        if (dashCooldownTimer > 0 || dashesLeft <= 0) return;
        dashesLeft--;
        dashTimer = 0;
        dashCooldownTimer = dashSettings.dashCooldown;

        Vector2 dashDirection = smoothMovementInput.normalized;
        if (dashDirection == Vector2.zero) dashDirection = (transform.localScale.x > 0) ? Vector2.right : Vector2.left;

        inputVelocity = Vector2.zero;
        rb.AddForce(dashDirection * dashSettings.dashForce, ForceMode2D.Impulse);

        dashEvent?.Invoke();
    }
    #endregion

    #region Gravity
    private void ApplyGravity() {
        float finalGravity = (inputVelocity.y < 0) ? jumpSettings.gravity * jumpSettings.downGravityMultiplier : jumpSettings.gravity; // Faster drop
        inputVelocity.y += finalGravity * Time.fixedDeltaTime;

        if (isGrounded && inputVelocity.y < 0) inputVelocity.y = 0f; // If grounded -> Dont apply force
        else if (!isGrounded && inputVelocity.y < jumpSettings.maxDropVelocity) inputVelocity.y = jumpSettings.maxDropVelocity; // Cap Velocity
        else if (!isGrounded && inputVelocity.y > 0 && velocity.y <= 0) inputVelocity.y = 0f; // On top collision -> Bump back down
    }

    private Vector2 GetFinalVelocity() {
        Vector2 finalInputVelocity = inputVelocity;

        if (inputVelocity.y > -jumpSettings.apexZeroGravityThreshold && inputVelocity.y < jumpSettings.apexZeroGravityThreshold)
            finalInputVelocity.y = 0; // Zero gravity at jump apex

        return finalInputVelocity;
    }
    #endregion

    #region Moving Platforms
    Transform currentPlatform;
    Vector2 currentPlatformOffset;

    private void CheckForMovingPlatform(Collider2D groundCollider) {
        if (groundCollider != null && groundCollider.tag == movingPlatformsSettings.platformTag) {
            if (currentPlatform == null) {
                currentPlatform = groundCollider.transform;
                currentPlatformOffset = transform.position - currentPlatform.position;
            }
        } else {
            currentPlatform = null;
        }
    }

    private void MoveToMovingPlatform() {
        if (currentPlatform == null) return;

        rb.position = (Vector2)currentPlatform.position + currentPlatformOffset;
    }

    private void SetMovingPlatformOffset() {
        currentPlatformOffset += Vector2.right * (inputVelocity.x + rb.velocity.x) * Time.fixedDeltaTime;
    }
    #endregion

    #region Step Detection
    private void ApplyStepForce() {
        if (CanStep()) {
            stepEvent?.Invoke();
            inputVelocity.y = stepSettings.moveForce;
        }
    }

    private bool CanStep() {
        if (inputVelocity.x == 0) return false;

        Vector2 faceDirection = (transform.localScale.x > 0) ? Vector2.right : Vector2.left;

        bool bottomCollision = Physics2D.Raycast(coll.bounds.center + Vector3.up * stepSettings.bottomYOffset, faceDirection, stepSettings.sideDistance, groundCheckSettings.groundLayer);
        if (!bottomCollision) return false;
        bool topCollision = Physics2D.Raycast(coll.bounds.center + Vector3.up * (stepSettings.bottomYOffset + stepSettings.secondCheckDistance), faceDirection, stepSettings.sideDistance, groundCheckSettings.groundLayer);
        if (topCollision) return false;
        
        return true;
    }

    private void SetStepCheckParameters() {
        stepSettings.secondCheckDistance = 0.1f;
        stepSettings.sideDistance = coll.bounds.extents.x + 0.05f;
        stepSettings.bottomYOffset = -coll.bounds.extents.y + 0.01f;
        stepSettings.moveForce = 1f;
    }

    private void DrawStepDebug() {
        Gizmos.DrawLine(coll.bounds.center + Vector3.up * stepSettings.bottomYOffset, coll.bounds.center + Vector3.up * stepSettings.bottomYOffset + Vector3.right * stepSettings.sideDistance);
        Gizmos.DrawLine(coll.bounds.center + Vector3.up * (stepSettings.bottomYOffset + stepSettings.secondCheckDistance), coll.bounds.center + Vector3.up * (stepSettings.bottomYOffset + stepSettings.secondCheckDistance) + Vector3.right * stepSettings.sideDistance);
    }
    #endregion

    #region Top Edge Detection
    private void ApplyTopEdgeForce() {
        float topEdgeDirection = GetTopEdgeDirection();

        switch (topEdgeDirection) {
            case 1:
                topEdgeEvent?.Invoke(Vector2.right);
                break;
            case 2:
                topEdgeEvent?.Invoke(Vector2.left);
                break;
        }

        inputVelocity.x += -GetTopEdgeDirection() * topEdgeNudgeSettings.nudgeForce;
    }

    private int GetTopEdgeDirection() {
        if (inputVelocity.x >= 0 && HasTopEdge(1)) return 1;
        if (inputVelocity.x <= 0 && HasTopEdge(-1)) return -1;

        return 0;
    }

    private bool HasTopEdge(int direction) {
        bool rightCollision = Physics2D.Raycast(coll.bounds.center + Vector3.right * topEdgeNudgeSettings.xOffset * direction, Vector2.up, topEdgeNudgeSettings.topDistance, groundCheckSettings.groundLayer);
        if (!rightCollision) return false;
        bool leftCollision = Physics2D.Raycast(coll.bounds.center + Vector3.right * (topEdgeNudgeSettings.xOffset - topEdgeNudgeSettings.secondCheckDistance) * direction, Vector2.up, topEdgeNudgeSettings.topDistance, groundCheckSettings.groundLayer);
        if (leftCollision) return false;

        return true;
    }

    private void SetTopEdgeParameters() {
        topEdgeNudgeSettings.secondCheckDistance = 0.15f;
        topEdgeNudgeSettings.topDistance = coll.bounds.extents.y + 0.5f;
        topEdgeNudgeSettings.xOffset = coll.bounds.extents.x;
        topEdgeNudgeSettings.nudgeForce = 3f;
    }

    private void DrawTopEdgeDebug() {
        int direction = (transform.localScale.x > 0) ? 1 : -1;

        Gizmos.DrawLine(coll.bounds.center + Vector3.right * topEdgeNudgeSettings.xOffset * direction,
            coll.bounds.center + Vector3.right * topEdgeNudgeSettings.xOffset * direction + Vector3.up * topEdgeNudgeSettings.topDistance);

        Gizmos.DrawLine(coll.bounds.center + Vector3.right * (topEdgeNudgeSettings.xOffset - topEdgeNudgeSettings.secondCheckDistance) * direction,
            coll.bounds.center + Vector3.right * (topEdgeNudgeSettings.xOffset - topEdgeNudgeSettings.secondCheckDistance) * direction + Vector3.up * topEdgeNudgeSettings.topDistance);
    }
    #endregion

    #region Checks
    private void SetSlipperyMaterial() {
        if (coll.sharedMaterial == null) {
            PhysicsMaterial2D physicsMaterial = new PhysicsMaterial2D("Slippery");
            physicsMaterial.friction = 0;

            coll.sharedMaterial = physicsMaterial;
        } else if (coll.sharedMaterial.friction != 0) {
            Debug.LogWarning("Physics material with friction applied. This could make the player stuck at walls.");
        }
    }

    private void CheckRigidbody() {
        if (rb.gravityScale != 0) {
            Debug.LogWarning("Set Rigidbody2D's gravity scale to 0.", rb);
        }
    }
    #endregion

    #region SettingsClasses
    [System.Serializable]
    public class GroundCheckSettings {
        public LayerMask groundLayer;
        [Space]
        public bool automaticCheckBox = false;
        public Vector2 checkBoxOffset;
        public Vector2 checkBoxSize;
    }

    [System.Serializable]
    public class MovementSettings {
        public bool canMove = true;
        public bool instantDirectionChange = true;
        public float movementAcceleration = 5;
        public float movementDeceleration = 7.5f;
        public float speedOnGround = 5;
    }

    [System.Serializable]
    public class ExternalForcesSettings {
        public float externalForceFriction = 1.5f;
    }

    [System.Serializable]
    public class SlopeSettings {
        public float slideTreshold = 0.2f;
        public float slideForce = 5;
    }

    [System.Serializable]
    public class JumpSettings {
        public float gravity = -19.62f;
        public float jumpForce = 2.5f;
        public float downGravityMultiplier = 1.25f;
        public float maxDropVelocity = -10;
        [Range(1, 10)] public int jumpsInAir = 1;

        [Header("Jump Apex")]
        public float apexSpeed = 7;
        public float apexSpeedThreshold = 10;
        public float apexZeroGravityThreshold = 0.4f;
    }

    [System.Serializable]
    public class WallJumpSettings {
        public bool canWallJump = true;
        public bool restoreFullJumps = false;
        public Vector2 wallJumpCheckBoxOffset;
        public Vector2 wallJumpCheckBoxSize;
        public float wallJumpDownGravity = -1;
        public float wallJumpAwayForce = 5;
    }

    [System.Serializable]
    public class DashSettings {
        public bool canDash = true;
        public float dashForce = 50;
        public float dashCooldown = 0.6f;
        [Range(1, 10)] public int dashesInAir = 1;
    }

    [System.Serializable]
    public class StepSettings {
        public bool automaticConfiguration = false;
        public float bottomYOffset = -0.49f;
        public float secondCheckDistance = 0.1f;
        public float sideDistance = 0.3f;
        public float moveForce = 1;
    }

    [System.Serializable]
    public class TopEdgeNudgeSettings {
        public bool automaticConfiguration = false;
        public float xOffset = 0.25f;
        public float secondCheckDistance = 0.125f;
        public float topDistance = 0.55f;
        public float nudgeForce = 3;
    }

    [System.Serializable]
    public class MovingPlatformsSettings {
        public string platformTag = "Platform";
    }
    #endregion
}