using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ThePlague.FinalCharacterController
{
    [DefaultExecutionOrder(-2)]
    public class ThirdPersonInput : MonoBehaviour, PlayerControls.IThirdPersonMapActions
    {
        #region Classe Variables
        public Vector2 ScrollInput {  get; private set; }

        [SerializeField] private CinemachineVirtualCamera virtualCamera;
        [SerializeField] private float cameraZoomSpeed = 0.1f;
        [SerializeField] private float cameraMinZoom = 1f;
        [SerializeField] private float cameraMaxZoom = 5f;

        private Cinemachine3rdPersonFollow thirdPersonFollow;
        #endregion

        #region Startup
        private void Awake()
        {
            thirdPersonFollow = virtualCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        }
        private void OnEnable()
        {
            if (PlayerInputManager.Instance?.PlayerControls == null)
            {
                Debug.Log("Player controls is not initialized - cannot enable");
                return;
            }

            PlayerInputManager.Instance.PlayerControls.ThirdPersonMap.Enable();
            PlayerInputManager.Instance.PlayerControls.ThirdPersonMap.SetCallbacks(this);
        }
        private void OnDisable()
        {
            if (PlayerInputManager.Instance?.PlayerControls == null)
            {
                Debug.Log("Player controls is not initialized - cannot disable");
                return;
            }

            PlayerInputManager.Instance.PlayerControls.ThirdPersonMap.Disable();
            PlayerInputManager.Instance.PlayerControls.ThirdPersonMap.RemoveCallbacks(this);
        }


        #endregion

        #region Update Logic
        private void Update()
        {
            thirdPersonFollow.CameraDistance = Mathf.Clamp(thirdPersonFollow.CameraDistance + ScrollInput.y, cameraMinZoom, cameraMaxZoom);
        }

        private void LateUpdate()
        {
            ScrollInput = Vector2.zero;
        }

        #endregion

        #region Input Callbacks
        public void OnScrollCamera(InputAction.CallbackContext context)
        {
            if (!context.performed) 
                return;

            Vector2 scrollInput = context.ReadValue<Vector2>();
            ScrollInput = -1f * scrollInput.normalized * cameraZoomSpeed;
        }
        #endregion
    }
}


