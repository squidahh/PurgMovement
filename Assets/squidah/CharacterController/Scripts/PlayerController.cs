using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.iOS;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ThePlague.FinalCharacterController
{
    [DefaultExecutionOrder(-1)]
    public class PlayerController : MonoBehaviour
    {
        #region Class Variables
        [Header("Components")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private Camera playerCamera;
        public float RotationMismatch { get; private set; } = 0f;
        public bool IsRotatingToTarget { get; private set; } = false;

        [Header("Base Movements")]
        public float walkAcceleration = 25f;
        public float walkSpeed = 5f;
        public float runAcceleration = 25f;
        public float runSpeed = 8f;
        public float sprintAcceleration = 50f;
        public float sprintSpeed = 12f;
        public float sprintSpeedIncrease = 0.02f;
        public float inAirAcceleration = 25f;
        public float drag = 15f;
        public float inAirDrag = 5f;
        public float gravity = 25f;
        public float terminalVelocity = 50f;
        public float jumpSpeed = 1.0f;
        public float movingThreshold = 0.01f;
        public float maxGrappleDistance;

        [Header("Animation")]
        public float playerModelRotationSpeed = 10f;
        public float rotateToTargetTime = 0.25f;


        [Header("Camera Settings")]
        public float lookSenseH = 0.1f;
        public float lookSenseV = 0.1f;
        public float lookLimitV = 89f;

        [Header("Environment Details")]
        [SerializeField] private LayerMask groundLayers;
        public Transform gunTip;
        public Transform player;
        public Vector3 currentGrapplePosition;
        public LineRenderer lineRenderer;
        public LayerMask aimColliderLayerMask = new LayerMask();
        public RawImage grappleAim;

        private PlayerLocomotionInput playerLocomotionInput;
        private PlayerState playerState;
        private PlayerActionsInput playerActionsInput;

        private Vector2 cameraRotation = Vector2.zero;
        private Vector2 playerTargetRotation = Vector2.zero;
        public Vector3 grappleHookPosition;
        private Vector3 characterVelocityMomentum;


        private bool jumpedLastFrame = false;
        private bool isRotatingClockwise = false;
        private bool activeGrapple = false;
        private bool activeSwing = false;
        private bool canGrapple = false;
        private bool grappleThrow = false;
        private bool ropeOut = false;
        private float rotatingToTargetTimer = 0f;
        private float verticalVelocity = 0f;
        private float grappleHookSize = 0f;
        private float antiBump;
        private float stepOffset;
        private float ropeLength;

        private PlayerMovementState lastMovementState = PlayerMovementState.Falling;
        #endregion

        #region Startup
        private void Awake()
        {
            playerActionsInput = GetComponent<PlayerActionsInput>();
            playerLocomotionInput = GetComponent<PlayerLocomotionInput>();
            playerState = GetComponent<PlayerState>();

            antiBump = sprintSpeed;
            stepOffset = characterController.stepOffset;
            Cursor.lockState = CursorLockMode.Locked;
        }
        #endregion

        #region Update Logic
        private void Update()
        {
            UpdateMovementState();

            if (!activeGrapple && !activeSwing)
            {
                HandleVerticalMovement();
                HandleLateralMovement();
                HandleGrappleHookStart();
                if (grappleThrow)
                {
                    HandleGrappleThrow();
                }
            }

            if (activeGrapple)
            {
                HandleGrappleMovement();
            }

            if (activeSwing)
            {
                HandleSwingMovement();
            }
        }

        private void UpdateMovementState()
        {
            lastMovementState = playerState.CurrentPlayerMovementState;

            bool canRun = CanRun();
            bool isMovingInput = playerLocomotionInput.MovementInput != Vector2.zero;
            bool isMovingLaterally = IsMovingLaterally();
            bool isSprinting = runSpeed >= sprintSpeed && isMovingLaterally;
            bool isWalking = isMovingLaterally && (!canRun || playerLocomotionInput.WalkToggleOn);
            bool isGroudned = IsGrounded();

            PlayerMovementState lateralState = isWalking ? PlayerMovementState.Walking :
                                               isSprinting ? PlayerMovementState.Sprinting :
                                               isMovingLaterally || isMovingInput ? PlayerMovementState.Running : PlayerMovementState.Idling;

            playerState.SetPlayerMovementState(lateralState);

            if (activeGrapple)
            {
                playerState.SetPlayerMovementState(PlayerMovementState.Grappling);
            }
            else if (activeSwing)
            {
                playerState.SetPlayerMovementState(PlayerMovementState.Swinging);
            }
            else if ((!isGroudned || jumpedLastFrame) && characterController.velocity.y > 0f)
            {
                playerState.SetPlayerMovementState(PlayerMovementState.Jumping);
                jumpedLastFrame = false;
                characterController.stepOffset = 0f;
            }
            else if ((!isGroudned || jumpedLastFrame) && characterController.velocity.y <= 0f)
            {
                playerState.SetPlayerMovementState(PlayerMovementState.Falling);
                jumpedLastFrame = false;
                characterController.stepOffset = 0f;
            }
            else
            {
                characterController.stepOffset = stepOffset;
            }
        }

        private void HandleVerticalMovement()
        {
            bool isGrounded = playerState.InGroundedState();

            verticalVelocity -= gravity * Time.deltaTime;

            if (isGrounded && verticalVelocity < 0)
                verticalVelocity =- antiBump;

            if (playerLocomotionInput.JumpPressed && isGrounded)
            {
                verticalVelocity += Mathf.Sqrt(jumpSpeed * 3 * gravity);
                jumpedLastFrame = true;
            }
            
            if (playerState.IsStateGroundedState(lastMovementState) && isGrounded)
            {
                verticalVelocity += antiBump;
            }

            if (Mathf.Abs(verticalVelocity) > Mathf.Abs(terminalVelocity))
            {
                verticalVelocity = -1f * Mathf.Abs(terminalVelocity);
            }
        }

        private void HandleLateralMovement()
        {
            bool isSprinting = playerState.CurrentPlayerMovementState == PlayerMovementState.Sprinting;
            bool isGrounded = playerState.InGroundedState();
            bool isWalking = playerState.CurrentPlayerMovementState == PlayerMovementState.Walking;
            bool isRunning = playerState.CurrentPlayerMovementState == PlayerMovementState.Running;
            bool isJumping = playerState.CurrentPlayerMovementState == PlayerMovementState.Jumping;
            bool isFalling = playerState.CurrentPlayerMovementState == PlayerMovementState.Falling;

            if (isRunning || isSprinting || isJumping || isFalling)
            {
                runSpeed += sprintSpeedIncrease * Time.deltaTime;
            }
            else if (!isRunning || !isSprinting || !isJumping || !isFalling)
            {
                runSpeed = 8f;
            }

            if (runSpeed >= sprintSpeed)
            {
                runSpeed = sprintSpeed;
            }

            float lateralAcceleration = !isGrounded ? inAirAcceleration :
                                        isWalking ? walkAcceleration :
                                        isSprinting ? sprintAcceleration : runAcceleration;
            float clampLateralMagnitude = !isGrounded ? sprintSpeed :
                                          isWalking ? walkSpeed :
                                          isSprinting ? sprintSpeed : runSpeed;

            Vector3 cameraForwardXZ = new Vector3(playerCamera.transform.forward.x, 0f, playerCamera.transform.forward.z).normalized;
            Vector3 cameraRightXZ = new Vector3(playerCamera.transform.right.x, 0f, playerCamera.transform.right.z).normalized;
            Vector3 movementDirection = cameraRightXZ * playerLocomotionInput.MovementInput.x + cameraForwardXZ * playerLocomotionInput.MovementInput.y;

            Vector3 movementDelta = movementDirection * lateralAcceleration * Time.deltaTime;
            Vector3 newVelocity = characterController.velocity + movementDelta;

            float dragMagnitude = isGrounded ? drag : inAirDrag;
            Vector3 currentDrag = newVelocity.normalized * dragMagnitude * Time.deltaTime;
            newVelocity = (newVelocity.magnitude > dragMagnitude * Time.deltaTime) ? newVelocity - currentDrag : Vector3.zero;
            newVelocity = Vector3.ClampMagnitude(new Vector3(newVelocity.x, 0f, newVelocity.z), clampLateralMagnitude);
            newVelocity.y = verticalVelocity;
            newVelocity = !isGrounded ? HandleSteepWalls(newVelocity) : newVelocity;
            newVelocity += characterVelocityMomentum;

            characterController.Move(newVelocity * Time.deltaTime);

            if (characterVelocityMomentum.magnitude >= 0f)
            {
                float momentumDrag = 5f;
                characterVelocityMomentum -= characterVelocityMomentum * momentumDrag * Time.deltaTime;
                if (characterVelocityMomentum.magnitude < 0f)
                {
                    characterVelocityMomentum = Vector3.zero;
                }
            }
        }

        private void HandleGrappleHookStart()
        {

            Vector2 screenCenterPoint = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Ray ray = Camera.main.ScreenPointToRay(screenCenterPoint);

            if (TestInputDownGrapplehook() && !canGrapple)
            {
                grappleAim.enabled = true;

                canGrapple = true;
            }
            else if (!TestInputDownGrapplehook() && canGrapple)
            {
                if (Physics.Raycast(ray, out RaycastHit raycastHit, maxGrappleDistance, aimColliderLayerMask))
                {
                    grappleHookPosition = raycastHit.point;
                    grappleThrow = true;
                    ropeOut = true;
                    ropeLength = raycastHit.distance;
                }
                else
                {
                    grappleAim.enabled = false;

                    canGrapple = false;
                }
            }

            lineRenderer.positionCount = 2;
            currentGrapplePosition = gunTip.position;
        }

        private void HandleGrappleThrow()
        {
            bool activateSwing = playerActionsInput.SwingToggleOn;

            float grappleThrowSpeed = 70f;

            grappleHookSize += grappleThrowSpeed * Time.deltaTime;

            if (grappleHookSize >= Vector3.Distance(player.position, grappleHookPosition) && !activateSwing)
            {
                activeGrapple = true;
            }
            else if (grappleHookSize >= Vector3.Distance(player.position, grappleHookPosition) && activateSwing)
            {
                activeSwing = true;
            }

        }

        private void HandleGrappleMovement()
        {
            grappleAim.enabled = false;
            canGrapple = false;

            Vector3 grappleDirection = (grappleHookPosition - player.position).normalized;

            float grappleSpeedMin = 10f;
            float grappleSpeedMax = 40f;
            float grappleSpeed = Mathf.Clamp(Vector3.Distance(player.position, grappleHookPosition), grappleSpeedMin, grappleSpeedMax);
            float grappleSpeedMultiplier = 2f;

            characterController.Move(grappleDirection * grappleSpeed * grappleSpeedMultiplier * Time.deltaTime);

            float reachedGrapplePositionDistance = 3f;

            if (Vector3.Distance(player.position, grappleHookPosition) < reachedGrapplePositionDistance)
            {
                Debug.Log("stopped");
                HandleGrappleHookStop();
            }

            if (TestInputDownGrapplehook())
            {
                float airMomentum = 1.5f;
                characterVelocityMomentum = grappleDirection * grappleSpeed * airMomentum;
                HandleGrappleHookStop();
            }

            if (TestInputJump() && IsGroundedWhileGrounded())
            {
                float momentumExtraSpeed = 3f;
                characterVelocityMomentum = grappleDirection * grappleSpeed * momentumExtraSpeed;
                float grappleJumpSpeed = 40f;
                characterVelocityMomentum += Vector3.up * grappleJumpSpeed;
                HandleGrappleHookStop();
            }
        }

        private void HandleSwingMovement()
        {

        }

        private void HandleGrappleHookStop()
        {
            activeGrapple = false;
            activeSwing = false;
            grappleThrow = false;
            ropeOut = false;
            verticalVelocity = 0f;
            lineRenderer.positionCount = 0;
            playerState.SetPlayerMovementState(PlayerMovementState.Falling);
        }

        private Vector3 HandleSteepWalls(Vector3 velocity)
        {
            Vector3 normal = CharacterControllerUtils.GetNormalWithSphereCast(characterController, groundLayers);
            float angle = Vector3.Angle(normal, Vector3.up);
            bool validAngle = angle <= characterController.slopeLimit;

            if (!validAngle && verticalVelocity < 0f)
                velocity = Vector3.ProjectOnPlane(velocity, normal);

            return velocity;
        }

        private void DrawRope()
        {
            if (!ropeOut)
            {
                return;
            }

            currentGrapplePosition = Vector3.Lerp(currentGrapplePosition, grappleHookPosition, Time.deltaTime * 4f);

            lineRenderer.SetPosition(0, gunTip.position);
            lineRenderer.SetPosition(1, currentGrapplePosition);
        }
        #endregion

        #region Late Update Logic
        private void LateUpdate()
        {
            UpdateCameraRotation();

            DrawRope();
        }

        private void UpdateCameraRotation()
        {
            cameraRotation.x += lookSenseH * playerLocomotionInput.LookInput.x;
            cameraRotation.y = Mathf.Clamp(cameraRotation.y - lookSenseV * playerLocomotionInput.LookInput.y, -lookLimitV, lookLimitV);

            playerTargetRotation.x += transform.eulerAngles.x + lookSenseH * playerLocomotionInput.LookInput.x;

            float rotationTolerance = 90f;
            bool isIdling = playerState.CurrentPlayerMovementState == PlayerMovementState.Idling;
            bool isGrappling = playerState.CurrentPlayerMovementState == PlayerMovementState.Grappling;
            IsRotatingToTarget = rotatingToTargetTimer > 0f;

            if (isGrappling)
            {
                float distance = Vector3.Distance(transform.position, grappleHookPosition);

                if (distance >= 3)
                {
                    transform.rotation = Quaternion.LookRotation(transform.forward);
                }
                else
                {
                    RotatePlayerToTarget();
                }
            }
            if (!isIdling && !isGrappling)
            {
                RotatePlayerToTarget();
            }
            else if (Mathf.Abs(RotationMismatch) > rotationTolerance || IsRotatingToTarget)
            {
                UpdateIdleRotation(rotationTolerance);
            }
         

            playerCamera.transform.rotation = Quaternion.Euler(cameraRotation.y, cameraRotation.x, 0f);

            Vector3 camForwardProjectedXZ = new Vector3(playerCamera.transform.forward.x, 0f, playerCamera.transform.forward.z).normalized;
            Vector3 crossProduct = Vector3.Cross(transform.forward, camForwardProjectedXZ);
            float sign = Mathf.Sign(Vector3.Dot(crossProduct, transform.up));
            RotationMismatch = sign * Vector3.Angle(transform.forward, camForwardProjectedXZ);
        }

        private void UpdateIdleRotation(float rotationTolerance)
        {

            if (Mathf.Abs(RotationMismatch) > rotationTolerance)
            {
                rotatingToTargetTimer = rotateToTargetTime;
                isRotatingClockwise = RotationMismatch > rotationTolerance;
            }
            rotatingToTargetTimer -= Time.deltaTime;

            if (isRotatingClockwise && RotationMismatch > 0f ||
                !isRotatingClockwise && RotationMismatch < 0f)
            {
                RotatePlayerToTarget();
            }

        }

        private void RotatePlayerToTarget()
        {
            Quaternion targetRotationX = Quaternion.Euler(0f, playerTargetRotation.x, 0f);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotationX, playerModelRotationSpeed * Time.deltaTime);
        }
        #endregion

        #region State Checks
        private bool IsMovingLaterally()
        {
            Vector3 lateralVelcity = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);

            return lateralVelcity.magnitude > movingThreshold;
        }

        private bool IsGrounded()
        {
            bool grounded = playerState.InGroundedState() ? IsGroundedWhileGrounded() : IsGroundedWhileAirborne();

            return grounded;
        }

        private bool IsGroundedWhileGrounded()
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - characterController.radius, transform.position.z);

            bool grounded = Physics.CheckSphere(spherePosition, characterController.radius, groundLayers, QueryTriggerInteraction.Ignore);

            return grounded;
        }

        private bool IsGroundedWhileAirborne()
        {
            Vector3 normal = CharacterControllerUtils.GetNormalWithSphereCast(characterController, groundLayers);
            float angle = Vector3.Angle(normal, Vector3.up);
            bool validAngle = angle <= characterController.slopeLimit;

            return characterController.isGrounded && validAngle;
        }

        private bool CanRun()
        {
            return playerLocomotionInput.MovementInput.y >= Mathf.Abs(playerLocomotionInput.MovementInput.x);
        }

        private bool TestInputDownGrapplehook()
        {
            return playerActionsInput.GrappleToggleOn;
        }

        private bool TestInputJump()
        {
            return playerLocomotionInput.JumpPressed;
        }
        #endregion
    }
}

