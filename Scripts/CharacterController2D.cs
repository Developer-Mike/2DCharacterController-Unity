using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class CharacterController2D : MonoBehaviour {
    [Header("Ground Check")]
    public LayerMask groundLayer;
    [Space]
    public bool automaticGroundcheckBox = true;
    public Vector2 groundcheckBoxOffset;
    public Vector2 groundcheckBoxSize;

    [Header("Movement")]
    public bool canMove = true;
    public bool instantDirectionChange = true;
    public float movementAcceleration = 5;
    public float movementDeceleration = 7.5f;
    public float speedOnGround = 5;

    [Header("Jump Apex")]
    public float apexSpeed = 7;
    public float apexSpeedThreshold = 10;
    public float apexZeroGravityThreshold = 0.4f;

    [Header("External Forces")]
    public float externalForceFriction = 1.25f;

    [Header("Slopes")]
    public float slideTreshold;
    public float slideForce;

    [Header("Jump")]
    public float gravity = -20;
    public float jumpForce = 2.5f;
    public float downGravityMultiplier = 1.25f;
    public float maxDropVelocity = -10;
    [Range(1, 10)] public int jumpsInAir = 1;

    [Header("Wall Jump")]
    public bool canWallJump = true;
    public bool restoreFullJumps;
    public Vector2 wallJumpCheckBoxOffset;
    public Vector2 wallJumpCheckBoxSize;
    public float wallJumpDownGravity;
    public float wallJumpAwayForce;

    [Header("Dash")]
    public bool canDash = true;
    public float dashForce = 50;
    public float dashCooldown = 0.6f;
    [Range(1, 10)] public int dashesInAir = 1;

    [Header("Step")]
    public bool automaticStepConfiguration = true;
    public float bottomStepYOffset;
    public float stepCheckDistance;
    public float stepDistance;
    public float stepMoveForce;

    [Header("Top Edge Detection")]
    public bool automaticTopEdgeConfiguration = true;
    public float topEdgeXOffset;
    public float topEdgeCheckDistance;
    public float topEdgeDistance;
    public float topEdgeMoveForce;

    [Header("Platforms")]
    public string platformTag = "Platform";

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

        if (canDash) {
            if (dashCooldownTimer > 0 && dashCooldownTimer - Time.deltaTime <= 0) {
                if (dashesLeft > 0) dashRechargedEvent?.Invoke();
                else notifyDashRechargeOnGrounded = true;
            }

            dashTimer -= Time.deltaTime;
            dashCooldownTimer -= Time.deltaTime;
        }
    }

    private void FixedUpdate() {
        rb.velocity /= externalForceFriction; // Reduce external forces

        Collider2D groundCollider = CheckGrounded(); // Ground check

        if (isGrounded) {
            dashesLeft = dashesInAir; // Reset dashes left on grounded
            if (notifyDashRechargeOnGrounded) { // Dash recharged event
                notifyDashRechargeOnGrounded = false;
                dashRechargedEvent?.Invoke();
            }

            jumpsLeft = jumpsInAir; // Reset jumps left on grounded
        }

        CheckForMovingPlatform(groundCollider); // Moveable platform check
        MoveToMovingPlatform(); // Move with platform

        CalculateRealVelocity(); // Velocity
        ApplyGravity(); // Gravity (AFTER Velocity)
        
        ApplyStepForce(); // Apply step force (AFTER Gravity)

        if (canMove && jumpTimer > 0) Jump(); // Jump
        if (canMove && canDash && dashTimer > 0) Dash(); // Dash
        
        inputVelocity.x = canMove ? (smoothMovementInput.x * GetMovementSpeed()) : 0; // Horizontal movement
        walkSpeed = (canMove && velocity.x != 0) ? smoothMovementInput.x : 0; // No walk speed when running at wall or cant move

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
        if (automaticGroundcheckBox) SetGroundcheckParameters();

        // Get Step Settings
        if (automaticStepConfiguration) SetStepCheckParameters();

        // Get Top Edge Settings
        if (automaticTopEdgeConfiguration) SetTopEdgeParameters();
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
    [HideInInspector] public float walkSpeed { get; private set; } = 0;
    [HideInInspector] public bool isGrounded { get; private set; } = false;
    [HideInInspector] public bool isFalling { get; private set; } = false;
    /*[HideInInspector]*/ public bool isWallJumping /*{ get; private set; }*/ = false;
    [HideInInspector] public int jumpsLeft;
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

        if (instantDirectionChange && (movementInput.x > 0 && smoothMovementInput.x < 0) || (movementInput.x < 0 && smoothMovementInput.x > 0))
            smoothMovementInput.x = 0; // Instant direction change

        if (movementInput.x == 0 && smoothMovementInput.x != 0) {
            float deceleration = ((smoothMovementInput.x > 0) ? movementDeceleration : -movementDeceleration) * Time.deltaTime;
            if (smoothMovementInput.x - deceleration > 0 != smoothMovementInput.x > 0) smoothMovementInput.x = 0; // Dont decelerate in other direction
            else smoothMovementInput.x -= deceleration; // Decelerate
        }

        smoothMovementInput.x += movementInput.x * movementAcceleration * Time.deltaTime; // Accelerate
        smoothMovementInput.x = Mathf.Clamp(smoothMovementInput.x, -1, 1); // Clamp speed
    }

    private float GetMovementSpeed() {
        if (isGrounded) return speedOnGround;

        return Mathf.Lerp(apexSpeed, speedOnGround, Mathf.Abs(inputVelocity.y) / apexSpeedThreshold); // Jump apex -> More Speed
    }

    private void CalculateRealVelocity() {
        velocity = ((Vector2)transform.position - lastPos) / Time.fixedDeltaTime;
        lastPos = transform.position;
    }

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

        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, Mathf.Infinity, groundLayer);

        // If slope is bigger than treshold (Slide down) or x input it not null (Stick to ground)
        if (Mathf.Abs(hit.normal.x) > slideTreshold || (inputVelocity.x != 0 && isGrounded)) inputVelocity.y = -slideForce;
    }

    void DrawSlopeDebug() {
        Vector2 origin = transform.position + Vector3.up * groundcheckBoxOffset.y * transform.localScale.y;
        Gizmos.DrawLine(origin, origin + (Vector2.up * slideTreshold + Vector2.right) * 0.2f);
    }
    #endregion

    #region Ground Check
    bool wasGrounded = false;

    private Collider2D CheckGrounded() {
        Collider2D groundCollider = Physics2D.OverlapBox((Vector2) coll.bounds.center + groundcheckBoxOffset, groundcheckBoxSize, 0, groundLayer);

        wasGrounded = isGrounded;
        isGrounded = groundCollider != null;

        if (!wasGrounded && isGrounded) groundEvent?.Invoke();
        if (wasGrounded && !isGrounded && inputVelocity.y < 0) inputVelocity.y = 0; // Reset default velocity

        isFalling = !isGrounded && inputVelocity.y < 0;

        return groundCollider;
    }

    private void SetGroundcheckParameters() {
        groundcheckBoxOffset = Vector2.down * coll.bounds.extents.y;
        groundcheckBoxSize = new Vector2(coll.bounds.extents.x * 2 - 0.025f, 0.01f);
    }

    private void DrawGroundcheckDebug() {
        Gizmos.DrawWireCube(coll.bounds.center + (Vector3)groundcheckBoxOffset, groundcheckBoxSize);
    }
    #endregion

    #region Jump
    const float jumpReminderTime = 0.1f;
    float jumpTimer = 0;

    private void Jump() {
        if (jumpsLeft <= 0) return;

        jumpsLeft--;
        jumpTimer = 0;
        inputVelocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);

        // If is wall jumping: add force in opposite direction to wall
        if (wasWallJumping || isWallJumping) {
            rb.AddForce(new Vector2((transform.localScale.x > 0 ? -1 : 1) * wallJumpAwayForce, 0), ForceMode2D.Impulse);
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
        
        Collider2D wallCollider = Physics2D.OverlapBox((Vector2)coll.bounds.center + new Vector2((transform.localScale.x > 0 ? 1 : -1) * wallJumpCheckBoxOffset.x, wallJumpCheckBoxOffset.y), wallJumpCheckBoxSize, 0, groundLayer);

        wasWallJumping = isWallJumping;
        isWallJumping = wallCollider != null;

        if (!wasWallJumping && isWallJumping) {
            wallGrabEvent?.Invoke();

            jumpsLeft = restoreFullJumps ? jumpsInAir : 1;
        }
    }

    void ApplyWallJumpGravity() {
        if (!isWallJumping || inputVelocity.y > 0) return;

        inputVelocity.y = wallJumpDownGravity;
    }

    void DrawWallJumpCheckDebug() {
        Gizmos.DrawWireCube((Vector2)coll.bounds.center + wallJumpCheckBoxOffset, wallJumpCheckBoxSize);
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
        dashCooldownTimer = dashCooldown;

        Vector2 dashDirection = smoothMovementInput.normalized;
        if (dashDirection == Vector2.zero) dashDirection = (transform.localScale.x > 0) ? Vector2.right : Vector2.left;

        inputVelocity = Vector2.zero;
        rb.AddForce(dashDirection * dashForce, ForceMode2D.Impulse);

        dashEvent?.Invoke();
    }
    #endregion

    #region Gravity
    private void ApplyGravity() {
        float finalGravity = (inputVelocity.y < 0) ? gravity * downGravityMultiplier : gravity; // Faster drop
        inputVelocity.y += finalGravity * Time.fixedDeltaTime;

        if (isGrounded && inputVelocity.y < 0) inputVelocity.y = 0f; // If grounded -> Dont apply force
        else if (!isGrounded && inputVelocity.y < maxDropVelocity) inputVelocity.y = maxDropVelocity; // Cap Velocity
        else if (!isGrounded && inputVelocity.y > 0 && velocity.y <= 0) inputVelocity.y = 0f; // On top collision -> Bump back down
    }

    private Vector2 GetFinalVelocity() {
        Vector2 finalInputVelocity = inputVelocity;

        if (inputVelocity.y > -apexZeroGravityThreshold && inputVelocity.y < apexZeroGravityThreshold)
            finalInputVelocity.y = 0; // Zero gravity at jump apex

        return finalInputVelocity;
    }
    #endregion

    #region Moving Platforms
    Transform currentPlatform;
    Vector2 currentPlatformOffset;

    private void CheckForMovingPlatform(Collider2D groundCollider) {
        if (groundCollider != null && groundCollider.tag == platformTag) {
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
            inputVelocity.y = stepMoveForce;
        }
    }

    private bool CanStep() {
        if (inputVelocity.x == 0) return false;

        Vector2 faceDirection = (transform.localScale.x > 0) ? Vector2.right : Vector2.left;

        bool bottomCollision = Physics2D.Raycast(coll.bounds.center + Vector3.up * bottomStepYOffset, faceDirection, stepDistance, groundLayer);
        if (!bottomCollision) return false;
        bool topCollision = Physics2D.Raycast(coll.bounds.center + Vector3.up * (bottomStepYOffset + stepCheckDistance), faceDirection, stepDistance, groundLayer);
        if (topCollision) return false;
        
        return true;
    }

    private void SetStepCheckParameters() {
        stepCheckDistance = 0.1f;
        stepDistance = coll.bounds.extents.x + 0.05f;
        bottomStepYOffset = -coll.bounds.extents.y + 0.01f;
        stepMoveForce = 1f;
    }

    private void DrawStepDebug() {
        Gizmos.DrawLine(coll.bounds.center + Vector3.up * bottomStepYOffset, coll.bounds.center + Vector3.up * bottomStepYOffset + Vector3.right * stepDistance);
        Gizmos.DrawLine(coll.bounds.center + Vector3.up * (bottomStepYOffset + stepCheckDistance), coll.bounds.center + Vector3.up * (bottomStepYOffset + stepCheckDistance) + Vector3.right * stepDistance);
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

        inputVelocity.x += -GetTopEdgeDirection() * topEdgeMoveForce;
    }

    private int GetTopEdgeDirection() {
        if (inputVelocity.x >= 0 && HasTopEdge(1)) return 1;
        if (inputVelocity.x <= 0 && HasTopEdge(-1)) return -1;

        return 0;
    }

    private bool HasTopEdge(int direction) {
        bool rightCollision = Physics2D.Raycast(coll.bounds.center + Vector3.right * topEdgeXOffset * direction, Vector2.up, topEdgeDistance, groundLayer);
        if (!rightCollision) return false;
        bool leftCollision = Physics2D.Raycast(coll.bounds.center + Vector3.right * (topEdgeXOffset - topEdgeCheckDistance) * direction, Vector2.up, topEdgeDistance, groundLayer);
        if (leftCollision) return false;

        return true;
    }

    private void SetTopEdgeParameters() {
        topEdgeCheckDistance = 0.15f;
        topEdgeDistance = coll.bounds.extents.y + 0.5f;
        topEdgeXOffset = coll.bounds.extents.x;
        topEdgeMoveForce = 3f;
    }

    private void DrawTopEdgeDebug() {
        int direction = (transform.localScale.x > 0) ? 1 : -1;

        Gizmos.DrawLine(coll.bounds.center + Vector3.right * topEdgeXOffset * direction,
            coll.bounds.center + Vector3.right * topEdgeXOffset * direction + Vector3.up * topEdgeDistance);

        Gizmos.DrawLine(coll.bounds.center + Vector3.right * (topEdgeXOffset - topEdgeCheckDistance) * direction,
            coll.bounds.center + Vector3.right * (topEdgeXOffset - topEdgeCheckDistance) * direction + Vector3.up * topEdgeDistance);
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
}
