using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class DyingMovement : MonoBehaviour
{
    public enum MovementState
    {
        Normal,
        Sliding,
        Vaulting,
        WallRunning,
        Climbing,
        Mantling,
        Dash,
    }

    [Header("Input")]
    public InputActionReference move;
    public InputActionReference jump;
    public InputActionReference sprint;
    public InputActionReference crouch;
    public InputActionReference dash;

    [Header("References")]
    public Transform cameraTransform;

    [Header("State")]
    public MovementState currentState = MovementState.Normal;

    [Header("Speed")]
    public float walkSpeed = 4.5f;
    public float sprintSpeed = 8.5f;
    public float crouchSpeed = 2.2f;
    public float acceleration = 14f;
    public float deceleration = 18f;

    [Tooltip(
        "How quickly the player bleeds speed when there is no input. Lower = more momentum when stopping. Does not affect turning response."
    )]
    public float groundFriction = 8f;
    public float airControl = 0.25f;

    [Header("Jump")]
    public float jumpHeight = 1.8f;
    public float gravity = -22f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float staminaDrain = 22f;
    public float staminaRegen = 15f;
    public float staminaRegenDelay = 1.5f;

    [Header("Crouch")]
    public float standHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchTransitionSpeed = 10f;

    [Header("Slide")]
    public float slideBoost = 2.5f;
    public float slideSpeed = 11f;
    public float slideFriction = 6f;
    public float slideMinSpeed = 3f;
    public float zingusBoost = 2f;
    public float zingusWindow = 0.2f;
    public float fallSlideBoost = 4f;
    public float slideGravityMultiplier = 2.2f;

    [Header("Vault")]
    public float vaultHeight = 1.4f;
    public float vaultForwardCheck = 0.45f;
    public float vaultUpSpeed = 7f;
    public float vaultForwardSpeed = 3f;
    public LayerMask vaultMask = ~0;

    [Header("Head Bob")]
    public float bobFrequency = 2.4f;
    public float bobAmplitude = 0.045f;
    public float sprintBobMultiplier = 1.7f;
    public float landingBobStrength = 0.9f;

    [Header("Slope")]
    public float slopeSlideSpeed = 9f;

    [Header("Wall Run")]
    public float wallRunSpeed = 7.5f;
    public float wallRunDuration = 1.8f;
    public float wallRunGravity = -2f;
    public float wallRunMinSpeed = 3.5f;
    public float wallRunAttraction = 4f;
    public float wallJumpUpForce = 6.5f;
    public float wallJumpAwayForce = 6f;
    public float wallDetectDist = 0.35f;
    public LayerMask wallMask = ~0;

    [Header("Wall Bounce")]
    public float bounceTriggerSpeed = 5f;
    public float bounceBoost = 1.3f;
    public float bounceMinAngle = 15f;
    public float bounceMaxAngle = 65f;

    [Header("Wall Climb")]
    public bool enableClimb = true;
    public float climbMaxHeight = 4f;
    public float climbSpeed = 4.5f;
    public float climbAttraction = 3f;
    public float climbBufferTime = 0.18f;

    [Tooltip(
        "How far in front of the player (beyond CC radius) to detect a climbable wall. Keep smaller than vaultForwardCheck."
    )]
    public float climbDetectDist = 0.2f;

    [Tooltip("Maximum time a single climb can last before being forcibly stopped.")]
    public float climbMaxDuration = 3f;

    [Tooltip("Cooldown before the player can start a new climb after finishing or cancelling one.")]
    public float climbCooldown = 0.5f;

    [Header("Ground Snap")]
    public float groundSnapDistance = 0.35f;
    public float groundStickForce = 2f;

    [Header("Dash")]
    public bool enableDash = true;
    public float dashSpeed = 18f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 0.45f;
    public float dashRollSpeed = 14f;
    public float dashGravityScale = 0.35f;

    [Header("Systems")]
    public bool enableVault = true;
    public bool enableSlide = true;
    public bool enableWallRun = true;
    public bool enableWallBounce = true;
    public bool enableStamina = true;

    // ── Public Camera Readables ───────────────────────────────────────────
    [HideInInspector]
    public Vector3 headBobOffset;

    [HideInInspector]
    public float sprintLerp;

    [HideInInspector]
    public float stamina;

    [HideInInspector]
    public bool isSprinting;

    [HideInInspector]
    public float landingImpact;

    [HideInInspector]
    public Vector3 groundNormal = Vector3.up;

    [HideInInspector]
    public bool IsWallRunning => currentState == MovementState.WallRunning;

    [HideInInspector]
    public int wallSide;

    [HideInInspector]
    public Vector3 wallNormal;

    [HideInInspector]
    public bool wallBounced;

    [HideInInspector]
    public bool IsClimbing =>
        currentState == MovementState.Climbing || currentState == MovementState.Mantling;

    [HideInInspector]
    public float climbProgress;

    [HideInInspector]
    public bool _isCrouching;
    public bool IsSliding => currentState == MovementState.Sliding;
    public float CrouchLerp => Mathf.InverseLerp(standHeight, crouchHeight, _cc.height);

    // ── Private ──────────────────────────────────────────────────────────
    CharacterController _cc;
    Vector3 _hVelocity;
    float _vVelocity;
    Vector3 _slopeVelocity;

    float _coyoteTimer;
    float _jumpBufferTimer;
    bool _jumpConsumed;

    float _currentStamina;
    float _staminaRegenTimer;

    float _bobTimer;
    Vector3 _bobSmoothed;

    bool _wasGrounded;
    float _preLandVVelocity;
    Vector3 _slopeVelSmoothed;

    Vector3 _slideVelocity;
    float _airTime;
    float _landingTimer;

    Vector3 _vaultVelocity;
    float _vaultTimer;

    float _wallRunTimer;
    Vector3 _wallRunNormal;
    int _wallRunSide;
    float _wallRunCooldown;
    float _wallBounceCooldown;

    float _climbTargetY;
    Vector3 _climbWallNormal;
    float _climbTimer;

    Vector3 _climbMantleTarget;
    float _climbBufferTimer;

    Vector3 _dashVelocity;
    float _dashTimer;
    float _dashCooldownTimer;
    bool _dashRolling;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _currentStamina = maxStamina;
    }

    void OnEnable()
    {
        move.action.Enable();
        jump.action.Enable();
        sprint.action.Enable();
        crouch.action.Enable();
        dash.action.Enable();
        jump.action.performed += OnJumpPerformed;
    }

    void OnDisable()
    {
        move.action.Disable();
        jump.action.Disable();
        sprint.action.Disable();
        crouch.action.Disable();
        dash.action.Disable();
        jump.action.performed -= OnJumpPerformed;
    }

    void OnJumpPerformed(InputAction.CallbackContext _) => _jumpBufferTimer = jumpBufferTime;

    void Update()
    {
        TickStamina();
        TickCrouch();
        TickVertical(); // Gravity & Jumping logic

        switch (currentState)
        {
            case MovementState.Mantling:
                TickMantle();
                break;

            case MovementState.Climbing:
                TickClimb();
                break;

            case MovementState.Vaulting:
                TickVault();
                break;

            case MovementState.WallRunning:
                TickWallRun();
                break;

            case MovementState.Sliding:
                TickSliding();
                break;

            case MovementState.Dash:
                TickDash();
                break;

            case MovementState.Normal:
                TryInitiateSpecialMovements();
                TickHorizontal();
                break;
        }

        TickHeadBob();

        // Apply final movement
        _cc.Move((_hVelocity + Vector3.up * _vVelocity + _slopeVelocity) * Time.deltaTime);
    }

    void SetState(MovementState newState)
    {
        currentState = newState;
    }

    void TryInitiateSpecialMovements()
    {
        if (CheckVault())
            return;
        if (CheckClimb())
            return;
        if (CheckWallRun())
            return;
        if (CheckDash())
            return;
        CheckSlide();
    }

    // ── Crouch ────────────────────────────────────────────────────────────
    void TickCrouch()
    {
        bool want = crouch.action.IsPressed();

        // Block uncrouching if ceiling is too low
        if (!want && _isCrouching)
        {
            if (
                Physics.SphereCast(
                    transform.position,
                    _cc.radius - 0.01f,
                    Vector3.up,
                    out _,
                    standHeight - crouchHeight + 0.05f
                )
            )
                want = true;
        }

        _isCrouching = want;
        float targetH = _isCrouching ? crouchHeight : standHeight;
        _cc.height = Mathf.Lerp(_cc.height, targetH, crouchTransitionSpeed * Time.deltaTime);
        _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);
    }

    // ── Vault ─────────────────────────────────────────────────────────────
    bool CheckVault()
    {
        if (!enableVault)
            return false;

        Vector2 input = move.action.ReadValue<Vector2>();
        if (input.y > 0.4f)
            _climbBufferTimer = climbBufferTime;
        else
            _climbBufferTimer -= Time.deltaTime;

        if (_climbBufferTimer <= 0f)
            return false;

        Vector3 fwd = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        float checkDist = _cc.radius + vaultForwardCheck + 0.45f;
        float[] heights = { 0.25f, 0.6f, 0.95f };
        bool hitWall = false;
        RaycastHit wallHit = default;

        foreach (float h in heights)
        {
            if (
                Physics.Raycast(
                    transform.position + Vector3.up * h,
                    fwd,
                    out wallHit,
                    checkDist,
                    vaultMask
                )
            )
            {
                hitWall = true;
                break;
            }
        }

        if (!hitWall)
            return false;

        float approachAngle = Vector3.Angle(-_hVelocity.normalized, wallHit.normal);
        if (approachAngle > 65f)
            return false;

        Vector3 topOrigin = wallHit.point + Vector3.up * (vaultHeight + 0.15f);
        if (
            !Physics.Raycast(
                topOrigin,
                Vector3.down,
                out RaycastHit topHit,
                vaultHeight + 0.3f,
                vaultMask
            )
        )
            return false;

        float relHeight = topHit.point.y - transform.position.y;
        if (relHeight < 0.2f || relHeight > vaultHeight)
            return false;

        Vector3 landingSpot = topHit.point + fwd * (_cc.radius + 0.3f) + Vector3.up * 0.05f;
        if (Physics.CheckSphere(landingSpot, _cc.radius * 0.9f, vaultMask))
            return false;

        SetState(MovementState.Vaulting);
        _vaultTimer = 0f;

        float clearance = relHeight + 0.25f;
        _vVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * clearance);
        float fwdSpeed = Mathf.Max(_hVelocity.magnitude, vaultForwardSpeed);
        _vaultVelocity = fwd * fwdSpeed;

        return true;
    }

    void TickVault()
    {
        _hVelocity = Vector3.Lerp(_hVelocity, _vaultVelocity, 20f * Time.deltaTime);
        _vaultTimer += Time.deltaTime;

        if ((_cc.isGrounded && _vaultTimer > 0.18f) || _vaultTimer > 0.8f)
            SetState(MovementState.Normal);
    }

    // ── Climb & Mantle ────────────────────────────────────────────────────
    bool CheckClimb()
    {
        if (!enableClimb || _cc.isGrounded)
            return false;
        if (_wallRunCooldown > 0f)
            return false;

        Vector2 input = move.action.ReadValue<Vector2>();
        if (input.y < 0.4f)
            return false;

        Vector3 moveDir = Vector3.ProjectOnPlane(_hVelocity, Vector3.up);
        if (moveDir.sqrMagnitude < 0.01f)
            moveDir = transform.forward;
        moveDir.Normalize();

        float checkDist = _cc.radius + climbDetectDist;

        if (
            !Physics.SphereCast(
                transform.position + Vector3.up * (_cc.height * 0.45f),
                _cc.radius * 0.9f,
                moveDir,
                out RaycastHit wallHit,
                checkDist,
                vaultMask
            )
        )
            return false;

        float approach = Vector3.Angle(-_hVelocity.normalized, wallHit.normal);
        if (approach > 85f)
            return false;

        Vector3 topOrigin = wallHit.point + Vector3.up * climbMaxHeight;
        if (
            !Physics.Raycast(
                topOrigin,
                Vector3.down,
                out RaycastHit topHit,
                climbMaxHeight,
                vaultMask
            )
        )
            return false;

        float rel = topHit.point.y - transform.position.y;
        if (rel <= vaultHeight || rel > climbMaxHeight)
            return false;

        SetState(MovementState.Climbing);
        _climbWallNormal = wallHit.normal;
        _climbTargetY = topHit.point.y;
        _climbTimer = 0f;
        _hVelocity = Vector3.zero;
        _vVelocity = climbSpeed;

        return true;
    }

    void TickClimb()
    {
        if (_jumpBufferTimer > 0f)
        {
            _jumpBufferTimer = 0f;
            _hVelocity = _climbWallNormal * wallJumpAwayForce;
            _vVelocity = wallJumpUpForce;
            StopClimb();
            _wallRunCooldown = 0.4f;
            return;
        }

        if (move.action.ReadValue<Vector2>().y < 0.1f)
        {
            StopClimb();
            return;
        }

        Vector3 wallCheck = transform.position + Vector3.up * (_cc.height * 0.5f);
        if (
            !Physics.SphereCast(
                wallCheck,
                _cc.radius * 0.85f,
                -_climbWallNormal,
                out RaycastHit wallHit,
                _cc.radius + 0.35f,
                vaultMask
            )
        )
        {
            StopClimb();
            return;
        }

        _climbWallNormal = wallHit.normal;
        Vector3 topOrigin = wallHit.point + Vector3.up * climbMaxHeight;

        if (
            !Physics.Raycast(
                topOrigin,
                Vector3.down,
                out RaycastHit topHit,
                climbMaxHeight,
                vaultMask
            )
        )
        {
            StopClimb();
            return;
        }

        float relHeight = topHit.point.y - transform.position.y;
        if (relHeight <= 0f || relHeight > climbMaxHeight)
        {
            StopClimb();
            return;
        }

        Vector3 crestPos =
            topHit.point - wallHit.normal * (_cc.radius + 0.25f) + Vector3.up * (_cc.height * 0.5f);
        if (
            Physics.CheckCapsule(
                crestPos + Vector3.up * _cc.radius,
                crestPos + Vector3.up * (_cc.height - _cc.radius),
                _cc.radius * 0.95f,
                vaultMask
            )
        )
        {
            StopClimb();
            return;
        }

        _climbTimer += Time.deltaTime;
        _vVelocity = Mathf.MoveTowards(_vVelocity, climbSpeed, climbSpeed * 5f * Time.deltaTime);
        _hVelocity = -_climbWallNormal * climbAttraction;

        climbProgress = Mathf.Clamp01(
            (transform.position.y - (_climbTargetY - climbMaxHeight)) / climbMaxHeight
        );

        if (transform.position.y + (_cc.height * 0.5f) >= topHit.point.y - 0.05f)
        {
            _climbMantleTarget = crestPos;
            SetState(MovementState.Mantling);
            return;
        }

        if (_climbTimer > climbMaxDuration)
            StopClimb();
    }

    void TickMantle()
    {
        Vector3 toTarget = _climbMantleTarget - transform.position;

        // Smooth continuous movement instead of snapping positions
        Vector3 desiredMove = Vector3.ClampMagnitude(toTarget * 12f, climbSpeed * 2f);
        _hVelocity = Vector3.ProjectOnPlane(desiredMove, Vector3.up);
        _vVelocity = desiredMove.y;

        if (toTarget.magnitude < 0.15f)
        {
            _hVelocity = Vector3.zero;
            _vVelocity = -2f; // Push down slightly to ensure ground contact
            StopClimb();
        }
    }

    void StopClimb()
    {
        climbProgress = 0f;
        // Prevent immediately re-triggering a climb the frame after finishing/cancelling one.
        // We reuse _wallRunCooldown since CheckClimb already gates on it.
        _wallRunCooldown = climbCooldown;
        SetState(MovementState.Normal);
    }

    // ── Wall Run ──────────────────────────────────────────────────────────
    bool CheckWallRun()
    {
        if (
            !enableWallRun
            || _cc.isGrounded
            || _hVelocity.magnitude < wallRunMinSpeed
            || _wallRunCooldown > 0f
        )
            return false;

        Vector3 origin = transform.position + Vector3.up * (_cc.height * 0.4f);
        float checkDist = _cc.radius + wallDetectDist;

        bool hitLeft = Physics.Raycast(
            origin,
            -transform.right,
            out RaycastHit leftHit,
            checkDist,
            wallMask
        );
        bool hitRight = Physics.Raycast(
            origin,
            transform.right,
            out RaycastHit rightHit,
            checkDist,
            wallMask
        );

        if (hitLeft)
            return StartWallRun(leftHit, -1);
        if (hitRight)
            return StartWallRun(rightHit, 1);

        return false;
    }

    bool StartWallRun(RaycastHit hit, int side)
    {
        Vector3 wallFwd = Vector3.Cross(hit.normal, Vector3.up);
        float parallelism = Mathf.Abs(Vector3.Dot(_hVelocity.normalized, wallFwd.normalized));

        if (parallelism < 0.5f)
            return false; // Too steep an angle to latch on

        SetState(MovementState.WallRunning);
        _wallRunSide = side;
        _wallRunNormal = hit.normal;
        wallNormal = hit.normal;
        wallSide = side;
        _wallRunTimer = wallRunDuration;

        if (Vector3.Dot(wallFwd, _hVelocity) < 0f)
            wallFwd = -wallFwd;
        _hVelocity = wallFwd * Mathf.Max(_hVelocity.magnitude, wallRunSpeed);
        if (_vVelocity < 0f)
            _vVelocity *= 0.25f;

        return true;
    }

    void TickWallRun()
    {
        wallBounced = false;
        _wallBounceCooldown -= Time.deltaTime;

        if (_cc.isGrounded || _jumpBufferTimer > 0f)
        {
            if (_jumpBufferTimer > 0f)
                DoWallJump();
            else
                StopWallRun(false);
            return;
        }

        Vector3 origin = transform.position + Vector3.up * (_cc.height * 0.4f);
        bool hit = Physics.Raycast(
            origin,
            _wallRunSide == -1 ? -transform.right : transform.right,
            out RaycastHit currentHit,
            _cc.radius + wallDetectDist,
            wallMask
        );

        if (!hit || _wallRunTimer <= 0f)
        {
            StopWallRun(false);
            return;
        }

        _wallRunNormal = currentHit.normal;
        wallNormal = currentHit.normal;
        _wallRunTimer -= Time.deltaTime;

        Vector3 wallFwd = Vector3.Cross(currentHit.normal, Vector3.up);
        if (Vector3.Dot(wallFwd, _hVelocity) < 0f)
            wallFwd = -wallFwd;

        _hVelocity = Vector3.Lerp(_hVelocity, wallFwd * wallRunSpeed, 8f * Time.deltaTime);
        _hVelocity -= currentHit.normal * (wallRunAttraction * Time.deltaTime);
        _vVelocity = Mathf.Lerp(_vVelocity, wallRunGravity, 5f * Time.deltaTime);

        _slopeVelocity = Vector3.zero;
        _slopeVelSmoothed = Vector3.zero;
    }

    void StopWallRun(bool fromJump)
    {
        wallSide = 0;
        _wallRunCooldown = fromJump ? 0.5f : 0.25f;
        SetState(MovementState.Normal);
    }

    void DoWallJump()
    {
        _jumpBufferTimer = 0f;
        Vector3 wallFwd = Vector3.ProjectOnPlane(_hVelocity, _wallRunNormal);
        _hVelocity =
            _wallRunNormal * wallJumpAwayForce + (0.65f * wallRunSpeed * wallFwd.normalized);
        _vVelocity = wallJumpUpForce;
        StopWallRun(true);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (
            !enableWallBounce
            || currentState != MovementState.Normal
            || _cc.isGrounded
            || _wallBounceCooldown > 0f
        )
            return;

        float wallAngle = Vector3.Angle(hit.normal, Vector3.up);
        if (wallAngle < 50f || wallAngle > 130f)
            return;

        float speed = _hVelocity.magnitude;
        if (speed < bounceTriggerSpeed)
            return;

        float approachAngle = Vector3.Angle(-_hVelocity.normalized, hit.normal);
        if (approachAngle < bounceMinAngle || approachAngle > bounceMaxAngle)
            return;

        Vector3 reflected = Vector3.Reflect(_hVelocity, hit.normal);
        reflected.y = 0f;
        _hVelocity = reflected.normalized * (speed * bounceBoost);
        _vVelocity = Mathf.Max(_vVelocity + 2f, 2f);

        _wallBounceCooldown = 0.5f;
        wallBounced = true;
    }

    // ── Vertical & Core Movement ──────────────────────────────────────────
    void TickVertical()
    {
        bool grounded = _cc.isGrounded;

        if (!grounded)
        {
            _airTime += Time.deltaTime;
        }
        else
        {
            if (!_wasGrounded)
                _landingTimer = zingusWindow;

            _airTime = 0f;
        }

        // ── Landing detection ─────────────────────────────────────────────
        landingImpact = 0f;
        if (!_wasGrounded && grounded)
        {
            float impact = Mathf.InverseLerp(0f, -18f, _preLandVVelocity);
            landingImpact = impact * landingBobStrength;
        }
        _wasGrounded = grounded;

        // ── Grounded reset ────────────────────────────────────────────────
        if (grounded)
        {
            if (_vVelocity < 0f)
                _vVelocity = -groundStickForce;

            _coyoteTimer = coyoteTime;
            _jumpConsumed = false;

            // Extra stair snap
            if (
                Physics.Raycast(
                    transform.position + Vector3.up * 0.1f,
                    Vector3.down,
                    out RaycastHit snapHit,
                    (_cc.height * 0.5f) + groundSnapDistance
                )
            )
            {
                float dist = snapHit.distance - (_cc.height * 0.5f);

                if (dist > 0.02f && dist < groundSnapDistance)
                {
                    _cc.Move(Vector3.down * dist);
                }
            }
        }
        else
        {
            _coyoteTimer -= Time.deltaTime;
        }

        _jumpBufferTimer -= Time.deltaTime;
        _wallRunCooldown -= Time.deltaTime;
        _dashCooldownTimer -= Time.deltaTime;

        // Jump logic (Only execute if not in a special movement state that overrides it)
        bool canJump = _coyoteTimer > 0f && !_jumpConsumed && _jumpBufferTimer > 0f;
        if (canJump && currentState == MovementState.Normal)
        {
            _vVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _coyoteTimer = 0f;
            _jumpConsumed = true;
            _jumpBufferTimer = 0f;
        }

        _preLandVVelocity = _vVelocity;
        float grav = gravity;
        if (currentState == MovementState.Sliding && !_cc.isGrounded)
            grav *= slideGravityMultiplier;

        // Don't apply raw gravity if we are mantling or climbing
        if (
            currentState != MovementState.Mantling
            && currentState != MovementState.Climbing
            && currentState != MovementState.WallRunning
        )
            _vVelocity += grav * Time.deltaTime;

        groundNormal = Vector3.up;
        Vector3 slideTarget = Vector3.zero;

        if (
            grounded
            && Physics.Raycast(
                transform.position,
                Vector3.down,
                out RaycastHit hit,
                _cc.height * 0.5f + 0.4f
            )
        )
        {
            groundNormal = hit.normal;
            float angle = Vector3.Angle(hit.normal, Vector3.up);

            if (angle > _cc.slopeLimit)
            {
                Vector3 slide = Vector3.ProjectOnPlane(Vector3.down, hit.normal).normalized;
                float steepness = Mathf.InverseLerp(_cc.slopeLimit, 70f, angle);
                slideTarget = (1f + steepness) * slopeSlideSpeed * slide;
            }
        }

        _slopeVelSmoothed = Vector3.MoveTowards(
            _slopeVelSmoothed,
            slideTarget,
            30f * Time.deltaTime
        );
        _slopeVelocity = _slopeVelSmoothed;
        _landingTimer -= Time.deltaTime;
    }

    void CheckSlide()
    {
        if (
            enableSlide
            && _cc.isGrounded
            && isSprinting
            && _isCrouching
            && _hVelocity.magnitude > slideMinSpeed
        )
        {
            SetState(MovementState.Sliding);
            Vector3 dir = _hVelocity.normalized;
            float boost = slideBoost;

            if (_vVelocity < -2f)
            {
                float fallBoost = Mathf.InverseLerp(-2f, -20f, _vVelocity) * fallSlideBoost;
                boost += fallBoost;
            }

            if (_landingTimer > 0f)
                boost += zingusBoost;
            // Cap so spam-re-sliding can never stack speed beyond slideSpeed
            _slideVelocity = dir * Mathf.Min(_hVelocity.magnitude + boost, slideSpeed);
        }
    }

    void TickSliding()
    {
        _slideVelocity = Vector3.MoveTowards(
            _slideVelocity,
            Vector3.zero,
            slideFriction * Time.deltaTime
        );
        _hVelocity = _slideVelocity;

        if (!_isCrouching || _slideVelocity.magnitude < slideMinSpeed || !_cc.isGrounded)
            SetState(MovementState.Normal);
    }

    void TickHorizontal()
    {
        Vector2 input = move.action.ReadValue<Vector2>();
        bool grounded = _cc.isGrounded;
        bool sprinting = sprint.action.IsPressed();

        isSprinting = sprinting && !_isCrouching && _currentStamina > 0f && input.y > 0.1f;
        sprintLerp = Mathf.MoveTowards(sprintLerp, isSprinting ? 1f : 0f, 5f * Time.deltaTime);

        float targetSpeed = _isCrouching
            ? crouchSpeed
            : Mathf.Lerp(walkSpeed, sprintSpeed, sprintLerp);

        Vector3 fwd = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;

        Vector3 wish = (fwd * input.y + right * input.x).normalized;
        if (grounded)
            wish = Vector3.ProjectOnPlane(wish, groundNormal).normalized;

        bool hasInput = input.magnitude > 0.05f;

        if (hasInput)
        {
            if (grounded)
            {
                // Split current velocity into the component the player wants and the component they don't.
                float alongWish = Vector3.Dot(_hVelocity, wish);
                Vector3 perp = _hVelocity - wish * alongWish;

                // Rapidly kill the perpendicular drift so turning feels sharp, not slippery.
                perp = Vector3.MoveTowards(perp, Vector3.zero, deceleration * Time.deltaTime);

                // Independently accelerate in the desired direction.
                float newAlong = Mathf.MoveTowards(
                    alongWish,
                    targetSpeed,
                    acceleration * Time.deltaTime
                );

                _hVelocity = wish * newAlong + perp;
            }
            else
            {
                // Air: simple nudge toward desired velocity (air control unchanged).
                _hVelocity = Vector3.MoveTowards(
                    _hVelocity,
                    wish * targetSpeed,
                    acceleration * airControl * Time.deltaTime
                );
            }
        }
        else
        {
            // No input: coast to a stop using groundFriction (softer than deceleration so
            // the player carries momentum when they intentionally stop at speed).
            float frictionRate = grounded ? groundFriction : acceleration * airControl;
            _hVelocity = Vector3.MoveTowards(
                _hVelocity,
                Vector3.zero,
                frictionRate * Time.deltaTime
            );
        }
    }

    // ── Head Bob & Stamina ────────────────────────────────────────────────
    void TickHeadBob()
    {
        float speed = _hVelocity.magnitude;
        bool moving = speed > 0.15f && _cc.isGrounded && currentState != MovementState.Sliding;

        if (moving)
        {
            float sprintMult = Mathf.Lerp(1f, sprintBobMultiplier, sprintLerp);
            float freq = bobFrequency * sprintMult * Mathf.Clamp(speed / walkSpeed, 0.75f, 2f);

            _bobTimer += Time.deltaTime * freq * Mathf.PI * 2f;
            float x = Mathf.Cos(_bobTimer * 0.5f) * bobAmplitude * 0.45f;
            float y = Mathf.Sin(_bobTimer) * bobAmplitude - (bobAmplitude * 0.35f);

            _bobSmoothed = Vector3.Lerp(_bobSmoothed, new Vector3(x, y, 0f), 18f * Time.deltaTime);
        }
        else
            _bobSmoothed = Vector3.Lerp(_bobSmoothed, Vector3.zero, 10f * Time.deltaTime);

        headBobOffset = _bobSmoothed;
    }

    void TickStamina()
    {
        if (!enableStamina)
        {
            stamina = maxStamina;
            return;
        }

        if (isSprinting)
        {
            _currentStamina = Mathf.Max(0f, _currentStamina - staminaDrain * Time.deltaTime);
            _staminaRegenTimer = staminaRegenDelay;
        }
        else
        {
            _staminaRegenTimer -= Time.deltaTime;
            if (_staminaRegenTimer <= 0f)
                _currentStamina = Mathf.Min(
                    maxStamina,
                    _currentStamina + staminaRegen * Time.deltaTime
                );
        }

        stamina = _currentStamina;
    }

    bool CheckDash()
    {
        if (!enableDash)
            return false;

        if (_dashCooldownTimer > 0f)
            return false;

        if (!dash.action.WasPressedThisFrame())
            return false;

        Vector2 input = move.action.ReadValue<Vector2>();

        Vector3 fwd = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;

        Vector3 dir = (fwd * input.y + right * input.x).normalized;

        if (dir.sqrMagnitude < 0.01f)
            dir = transform.forward;

        _dashRolling = _isCrouching || _airTime > 0.25f;

        float speed = _dashRolling ? dashRollSpeed : dashSpeed;

        _dashVelocity = dir * speed;
        _dashTimer = dashDuration;
        _dashCooldownTimer = dashCooldown;

        SetState(MovementState.Dash);
        return true;
    }

    void TickDash()
    {
        _dashTimer -= Time.deltaTime;

        _hVelocity = _dashVelocity;

        if (!_cc.isGrounded)
            _vVelocity += gravity * dashGravityScale * Time.deltaTime;
        else
            _vVelocity = -groundStickForce;

        if (_dashRolling)
            _isCrouching = true;

        if (_dashTimer <= 0f)
        {
            if (_dashRolling && _cc.isGrounded)
            {
                SetState(MovementState.Sliding);
                _slideVelocity = _dashVelocity;
            }
            else
            {
                SetState(MovementState.Normal);
            }
        }
    }
}
