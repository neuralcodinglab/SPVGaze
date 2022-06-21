using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using CallbackContext = UnityEngine.InputSystem.InputAction.CallbackContext;

namespace Xarphos.Scripts
{
    public class InputHandler : MonoBehaviour
    {
        public InputActionManager actionManagerRef;
        public float moveSpeedScale = 0.2f;
        public float rotateSpeedScale = 0.2f;
        private Vector3 _move;
        private float _scale, _rotate;
        
#region Setting up control references in Editor        
        [SerializeField]
        private InputActionProperty movePlayer;
        /// <summary>
        /// The Input System action to use for moving the player along the hallway. Must be a <see cref="Vector2Control"/> Control.
        /// </summary>
        public InputActionProperty MoveAction
        {
            get => movePlayer;
            set => SetInputActionProperty(ref movePlayer, value);
        }
        [SerializeField]
        private InputActionProperty rotatePlayer;
        /// <summary>
        /// The Input System action to use for rotating the player along the hallway. Must be a <see cref="Vector2Control"/> Control.
        /// </summary>
        public InputActionProperty RotateAction
        {
            get => rotatePlayer;
            set => SetInputActionProperty(ref rotatePlayer, value);
        }
        [SerializeField]
        private InputActionProperty phospheneSimToggle;
        /// <summary>
        /// The Input System action to use to toggle the phosphene simulation. Must be a <see cref="ButtonControl"/> Control.
        /// </summary>
        public InputActionProperty TogglePhospheneSimulationAction
        {
            get => phospheneSimToggle;
            set => SetInputActionProperty(ref phospheneSimToggle, value);
        }
        [SerializeField]
        private InputActionProperty edgeDetectionToggle;
        /// <summary>
        /// The Input System action to use to toggle the edge detection filter. Must be a <see cref="ButtonControl"/> Control.
        /// </summary>
        public InputActionProperty ToggleEdgeDetectionAction
        {
            get => edgeDetectionToggle;
            set => SetInputActionProperty(ref edgeDetectionToggle, value);
        }
        [SerializeField]
        private InputActionProperty iterateSurfaceReplacements;
        /// <summary>
        /// The Input System action to use for iterating the different surface replacement shaders. Must be a <see cref="ButtonControl"/> Control.
        /// </summary>
        public InputActionProperty IterateSurfaceReplacementsAction
        {
            get => iterateSurfaceReplacements;
            set => SetInputActionProperty(ref iterateSurfaceReplacements, value);
        }
        [SerializeField]
        private InputActionProperty iterateEyeTrackingState;
        /// <summary>
        /// The Input System action to use for iterating the different surface replacement shaders. Must be a <see cref="ButtonControl"/> Control.
        /// </summary>
        public InputActionProperty IterateEyeTrackingStateAction
        {
            get => iterateEyeTrackingState;
            set => SetInputActionProperty(ref iterateEyeTrackingState, value);
        }
        [SerializeField]
        private InputActionProperty camLocking;
        /// <summary>
        /// The Input System action to use for locking the view to camera position. Must be a <see cref="ButtonControl"/> Control.
        /// </summary>
        public InputActionProperty LockToCameraAction
        {
            get => camLocking;
            set => SetInputActionProperty(ref camLocking, value);
        }
        [SerializeField]
        private InputActionProperty gazeLocking;
        /// <summary>
        /// The Input System action to use for locking the view to be centred on gaze. Must be a <see cref="ButtonControl"/> Control.
        /// </summary>
        public InputActionProperty LockToGazeAction
        {
            get => gazeLocking;
            set => SetInputActionProperty(ref gazeLocking, value);
        }

        /// <summary>
        /// Taken from <see cref="ActionBasedController"/> to handle the input actions
        /// </summary>
        /// <param name="property"></param>
        /// <param name="value"></param>
        private void SetInputActionProperty(ref InputActionProperty property, InputActionProperty value)
        {
            if (Application.isPlaying)
                property.DisableDirectAction();

            property = value;

            if (Application.isPlaying && isActiveAndEnabled)
                property.EnableDirectAction();
        }
#endregion

        /// <summary>
        /// Start is called on the frame when a script is enabled just before
        /// any of the Update methods is called the first time.
        /// </summary>
        void Start()
        {
            var simulator = FindObjectOfType<PhospheneSimulator>();
            
            MoveAction.action.performed += Move;
            MoveAction.action.canceled += Move;
            RotateAction.action.performed += Rotate;
            RotateAction.action.canceled += Rotate;
            ToggleEdgeDetectionAction.action.performed += simulator.ToggleEdgeDetection;
            TogglePhospheneSimulationAction.action.performed += simulator.TogglePhospheneSim;
            IterateSurfaceReplacementsAction.action.performed += simulator.NextSurfaceReplacementMode;
            IterateEyeTrackingStateAction.action.performed += simulator.NextEyeTrackingCondition;
        }

        private void Update()
        {
            _scale = Time.deltaTime * moveSpeedScale;
            transform.Translate(_scale * _move);
            _scale = Time.deltaTime * rotateSpeedScale;
            transform.Rotate(Vector3.up, _rotate * _scale);
        }

        public void Move(CallbackContext ctx)
        {
            if (ctx.performed)
            {
                var input = ctx.ReadValue<Vector2>();
                _move.x = input.x;
                _move.z = input.y;
            }
            else //if (ctx.canceled)
                _move = Vector3.zero;
        }
        
        public void Rotate(CallbackContext ctx)
        {
            if (ctx.control.device == Mouse.current) return;
            if (ctx.performed)
            {
                _rotate = ctx.ReadValue<Vector2>().x;                
            }
            else
            {
                _rotate = 0;
            }
        }
    }
}
