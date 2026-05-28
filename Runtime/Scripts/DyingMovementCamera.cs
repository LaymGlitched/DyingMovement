using UnityEngine;
using UnityEngine.InputSystem;

public class DyingMovementCamera : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference look;

    [Header("References")]
    public DyingMovement movement;
    public Camera cam;

    [Header("Sensitivity")]
    public float mouseSensitivity = 0.15f;
    public float gamepadSensitivity = 180f;

    [Header("Gamepad Smoothing")]
    [Range(0.01f, 0.2f)]
    public float gamepadSmoothing = 0.06f;

    [Header("Look Clamp")]
    public float pitchMin = -85f;
    public float pitchMax = 85f;

    [Header("FOV")]
    public float baseFOV = 75f;
    public float sprintFOVAdd = 10f;
    public float fovLerpSpeed = 9f;

    [Header("Crouch")]
    public float crouchCameraOffset = 0.5f;
    public float crouchCameraSpeed = 10f;

    [Header("Wall Run")]
    public float wallRunTiltAngle = 18f;
    public float wallRunTiltSpeed = 7f;
    public float wallRunFOVAdd = 7f;

    [Header("Wall Bounce")]
    public float wallBounceFOVKick = 14f;

    [Header("Stair Smoothing")]
    [Tooltip("The maximum speed the camera can catch up vertically when stepping up stairs.")]
    public float maxStairSnapSpeed = 6f;

    float _pitch;
    float _yaw;
    Vector2 _gpSmoothed;
    Vector2 _gpVelocity;
    Vector3 _camRestLocalPos;

    float _springPos;
    float _springVel;
    const float SpringK = 220f;
    const float SpringDamp = 22f;

    float _currentTilt;
    float _slideTilt;
    float _stairSmoothVelocity;
    float _crouchOffset;
    float _wallRunTilt;
    float _wallBouncePulse;
    float _wallBounceSpringPos;
    float _wallBounceSpringVel;

    float _smoothTargetWorldY;

    void Awake()
    {
        if (cam == null)
            cam = GetComponentInChildren<Camera>();

        _camRestLocalPos = transform.localPosition;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnEnable() => look.action.Enable();

    void OnDisable() => look.action.Disable();

    void Start()
    {
        if (movement != null)
        {
            _smoothTargetWorldY = movement.transform.position.y + _camRestLocalPos.y;
        }
    }

    void LateUpdate()
    {
        if (movement == null)
            return;

        TickLook();
        TickFOV();
        TickPositionAndBob();
    }

    void TickLook()
    {
        Vector2 raw = look.action.ReadValue<Vector2>();
        bool isMouse = look.action.activeControl?.device is Mouse;
        Vector2 delta;

        if (isMouse)
        {
            delta = raw * mouseSensitivity;
        }
        else
        {
            Vector2 target = raw * (gamepadSensitivity * Time.deltaTime);
            _gpSmoothed = Vector2.SmoothDamp(
                _gpSmoothed,
                target,
                ref _gpVelocity,
                gamepadSmoothing
            );
            delta = _gpSmoothed;
        }

        _yaw += delta.x;
        _pitch -= delta.y;
        _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);

        movement.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
    }

    void TickFOV()
    {
        if (cam == null || movement == null)
            return;

        if (movement.wallBounced)
            _wallBouncePulse = 1f;
        _wallBouncePulse = Mathf.Lerp(_wallBouncePulse, 0f, 7f * Time.deltaTime);

        float targetFOV =
            baseFOV
            + movement.sprintLerp * sprintFOVAdd
            + (movement.IsWallRunning ? wallRunFOVAdd : 0f)
            + _wallBouncePulse * wallBounceFOVKick;

        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, fovLerpSpeed * Time.deltaTime);
    }

    void TickPositionAndBob()
    {
        if (movement.landingImpact > 0f)
            _springVel -= movement.landingImpact * 3.5f;

        _springVel += (-SpringK * _springPos - SpringDamp * _springVel) * Time.deltaTime;
        _springPos += _springVel * Time.deltaTime;
        _springPos = Mathf.Clamp(_springPos, -0.4f, 0.4f);

        TickSlopeTilt();

        float targetSlideTilt = movement.IsSliding ? -10f : 0f;
        _slideTilt = Mathf.Lerp(_slideTilt, targetSlideTilt, 8f * Time.deltaTime);

        float climbLean = movement.IsClimbing ? movement.climbProgress * 0.06f : 0f;
        Vector3 climbOffset = movement.IsClimbing ? transform.forward * climbLean : Vector3.zero;

        float targetCrouchOffset = movement._isCrouching ? -crouchCameraOffset : 0f;
        _crouchOffset = Mathf.Lerp(
            _crouchOffset,
            targetCrouchOffset,
            crouchCameraSpeed * Time.deltaTime
        );

        Vector3 localProceduralOffset =
            movement.headBobOffset
            + Vector3.up * _springPos
            + Vector3.up * _crouchOffset
            + climbOffset;

        float rigidTargetWorldY = movement.transform.position.y + _camRestLocalPos.y;
        CharacterController cc = movement.GetComponent<CharacterController>();
        bool stairSmooth = cc.isGrounded && !movement.IsClimbing;

        float smoothTime = stairSmooth ? 0.05f : 0.015f;
        float maxSpeed =
            stairSmooth && rigidTargetWorldY > _smoothTargetWorldY
                ? maxStairSnapSpeed
                : Mathf.Infinity;

        _smoothTargetWorldY = Mathf.SmoothDamp(
            _smoothTargetWorldY,
            rigidTargetWorldY,
            ref _stairSmoothVelocity,
            smoothTime,
            maxSpeed
        );

        Vector3 finalWorldPos = new Vector3(
            movement.transform.position.x,
            _smoothTargetWorldY,
            movement.transform.position.z
        );
        finalWorldPos += movement.transform.TransformDirection(localProceduralOffset);

        transform.position = finalWorldPos;
    }

    void TickSlopeTilt()
    {
        CharacterController cc = movement.GetComponent<CharacterController>();

        Vector3 normal = movement.groundNormal;
        bool onSteepSlope = Vector3.Angle(normal, Vector3.up) > cc.slopeLimit;
        float slopeTarget = onSteepSlope ? Vector3.Dot(normal, transform.right) * 12f : 0f;
        _currentTilt = Mathf.Lerp(_currentTilt, slopeTarget, 6f * Time.deltaTime);

        float slideStrength = Mathf.Clamp01(cc.velocity.magnitude / movement.slideSpeed);
        float slideTarget = movement.IsSliding ? -10f * slideStrength : 0f;
        _slideTilt = Mathf.Lerp(_slideTilt, slideTarget, 8f * Time.deltaTime);

        float wallTarget = movement.IsWallRunning ? movement.wallSide * wallRunTiltAngle : 0f;
        _wallRunTilt = Mathf.Lerp(_wallRunTilt, wallTarget, wallRunTiltSpeed * Time.deltaTime);

        if (movement.wallBounced)
            _wallBounceSpringVel = 0.18f;

        _wallBounceSpringVel +=
            (-200f * _wallBounceSpringPos - 20f * _wallBounceSpringVel) * Time.deltaTime;
        _wallBounceSpringPos += _wallBounceSpringVel * Time.deltaTime;

        float totalTilt = _currentTilt + _slideTilt + _wallRunTilt + _wallBounceSpringPos * 25f;
        transform.localRotation = Quaternion.Euler(_pitch, 0f, totalTilt);
    }
}
