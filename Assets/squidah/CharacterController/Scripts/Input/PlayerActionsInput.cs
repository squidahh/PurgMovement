using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ThePlague.FinalCharacterController
{
    [DefaultExecutionOrder(-2)]
    public class PlayerActionsInput : MonoBehaviour, PlayerControls.IPlayerActionMapActions
    {
        #region Classe Variables
        [SerializeField] private bool holdToGrapple = true;
        [SerializeField] private bool holdToSwing = true;

        private PlayerLocomotionInput playerLocomotionInput;
        private PlayerState playerState;
        public bool AttackPressed { get; private set; }
        public bool GatherPressed { get; private set; }
        public bool GrappleToggleOn { get; private set; }
        public bool SwingToggleOn { get; private set; }
        #endregion

        #region Startup
        private void Awake()
        {
            playerLocomotionInput = GetComponent<PlayerLocomotionInput>();
            playerState = GetComponent<PlayerState>();
        }
        private void OnEnable()
        {
            if (PlayerInputManager.Instance?.PlayerControls == null)
            {
                Debug.Log("Player controls is not initialized - cannot enable");
                return;
            }

            PlayerInputManager.Instance.PlayerControls.PlayerActionMap.Enable();
            PlayerInputManager.Instance.PlayerControls.PlayerActionMap.SetCallbacks(this);
        }
        private void OnDisable()
        {
            if (PlayerInputManager.Instance?.PlayerControls == null)
            {
                Debug.Log("Player controls is not initialized - cannot disable");
                return;
            }

            PlayerInputManager.Instance.PlayerControls.PlayerActionMap.Disable();
            PlayerInputManager.Instance.PlayerControls.PlayerActionMap.RemoveCallbacks(this);
        }




        #endregion

        #region Update Logic
        private void Update()
        {
            if (playerLocomotionInput.MovementInput != Vector2.zero ||
                playerState.CurrentPlayerMovementState == PlayerMovementState.Jumping ||
                playerState.CurrentPlayerMovementState == PlayerMovementState.Falling)
            {
                GatherPressed = false;
            }
        }

        public void SetGatherPressedFalse()
        {
            GatherPressed = false;
        }

        private void SetAttackPressedFalse()
        {
            AttackPressed = false;
        }
        #endregion

        #region Input Callbacks
        public void OnAttack(InputAction.CallbackContext context)
        {
            if (!context.performed) 
                return;

            AttackPressed = true;
        }

        public void OnGather(InputAction.CallbackContext context)
        {
            if (!context.performed)
                return;

            GatherPressed = true;
        }

        public void OnGrapple(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                GrappleToggleOn = holdToGrapple || !GrappleToggleOn;
            }
            if (context.canceled)
            {
                GrappleToggleOn = !holdToGrapple && GrappleToggleOn;
            }

        }

        public void OnSwing(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                SwingToggleOn = holdToSwing || !SwingToggleOn;
            }

            if (context.canceled)
            {
                SwingToggleOn = !holdToSwing && SwingToggleOn;
            }
        }
        #endregion
    }
}