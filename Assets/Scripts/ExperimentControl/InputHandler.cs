using System;
using Simulation;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace ExperimentControl
{
    [RequireComponent(typeof(XROrigin))]
    public class InputHandler : MonoBehaviour
    {
        [Header("Subject Movement Control")]
        public float moveSpeed= 3f;
        public bool forwardIsHeadRotation = true;

        private Vector3 _move;
        private float _scale, _rotate;

        [Header("Collision Detection")]
        public Collider coll;

        private XROrigin xrOrigin;
        private float xBoundLeft = float.NegativeInfinity, xBoundRight=float.PositiveInfinity,
            zBoundBack=float.NegativeInfinity, zBoundForward = float.PositiveInfinity;

        private ControllerVibrator rightController, leftController;
        
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
        private InputActionProperty moveToHallway1;
        /// <summary>
        /// The Input System action to use for iterating the different surface replacement shaders. Must be a <see cref="ButtonControl"/> Control.
        /// </summary>
        public InputActionProperty MoveToHallway1Action
        {
            get => moveToHallway1;
            set => SetInputActionProperty(ref moveToHallway1, value);
        }
        [SerializeField]
        private InputActionProperty moveToHallway2;
        /// <summary>
        /// The Input System action to use for iterating the different surface replacement shaders. Must be a <see cref="ButtonControl"/> Control.
        /// </summary>
        public InputActionProperty MoveToHallway2Action
        {
            get => moveToHallway2;
            set => SetInputActionProperty(ref moveToHallway2, value);
        }
        [SerializeField]
        private InputActionProperty moveToHallway3;
        /// <summary>
        /// The Input System action to use for iterating the different surface replacement shaders. Must be a <see cref="ButtonControl"/> Control.
        /// </summary>
        public InputActionProperty MoveToHallway3Action
        {
            get => moveToHallway3;
            set => SetInputActionProperty(ref moveToHallway3, value);
        }
        [SerializeField]
        private InputActionProperty moveToPlayground;
        /// <summary>
        /// The Input System action to use for iterating the different surface replacement shaders. Must be a <see cref="ButtonControl"/> Control.
        /// </summary>
        public InputActionProperty MoveToPlaygroundAction
        {
            get => moveToPlayground;
            set => SetInputActionProperty(ref moveToPlayground, value);
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


        private void Awake()
        {
            SenorSummarySingletons.RegisterType(this);
        }

        private void Start()
        {
            xrOrigin = GetComponent<XROrigin>();
            coll ??= GetComponent<Collider>();

            var simulator = FindObjectOfType<PhospheneSimulator>();
            
            MoveAction.action.performed += Move;
            MoveAction.action.canceled += Move;
            ToggleEdgeDetectionAction.action.performed += simulator.ToggleEdgeDetection;
            TogglePhospheneSimulationAction.action.performed += simulator.TogglePhospheneSim;
            IterateSurfaceReplacementsAction.action.performed += simulator.NextSurfaceReplacementMode;
            IterateEyeTrackingStateAction.action.performed += simulator.NextEyeTrackingCondition;
            MoveToHallway1Action.action.performed += ctx =>
                MoveToNewHallway(HallwayCreator.HallwayObjects[HallwayCreator.Hallways.Hallway1]);
            MoveToHallway2Action.action.performed += ctx =>
                MoveToNewHallway(HallwayCreator.HallwayObjects[HallwayCreator.Hallways.Hallway2]);
            MoveToHallway3Action.action.performed += ctx =>
                MoveToNewHallway(HallwayCreator.HallwayObjects[HallwayCreator.Hallways.Hallway3]);
            MoveToPlaygroundAction.action.performed += ctx =>
                MoveToNewHallway(HallwayCreator.HallwayObjects[HallwayCreator.Hallways.Playground]);
        }

        public UnityEvent<HallwayCreator.Hallway> onChangeHallway;

        public void MoveToNewHallway(HallwayCreator.Hallway config)
        {
            transform.position = new Vector3(config.StartX, 0, 0);
            transform.rotation = Quaternion.Euler(Vector3.zero);
            SetBoundaries(config.WallLeft, config.WallRight, config.WallStart, config.WallEnd);
            
            onChangeHallway?.Invoke(config);
        }

        public void SetBoundaries(GameObject wallLeft, GameObject wallRight, GameObject wallStart, GameObject wallEnd)
        {
            // calculate boundaries player may not cross
            xBoundLeft = wallLeft.GetComponent<Collider>().bounds.max.x + coll.bounds.extents.x;
            xBoundRight = wallRight.GetComponent<Collider>().bounds.min.x - coll.bounds.extents.x;
            zBoundBack = wallStart.GetComponent<Collider>().bounds.max.z + coll.bounds.extents.z;
            zBoundForward = wallEnd.GetComponent<Collider>().bounds.min.z - coll.bounds.extents.z;
        }

        private void Update()
        {
            Move();
        }

        private void Move()
        {
            var move = _move * (Time.deltaTime * moveSpeed);

            if (forwardIsHeadRotation)
            {
                var fwd = Vector3.forward;
                var dir = xrOrigin.Camera.transform.forward;
                dir.y = 0;
                dir.Normalize();
                var cos = Vector3.Dot(dir, fwd);
                var sin = Vector3.Cross(dir, fwd).magnitude;

                // apply rotation to align with camera forward vector
                move = new Vector3(move.x * cos - move.z * sin, 0, move.x * sin + move.z * cos);
            }

            var newPos = xrOrigin.Origin.transform.localPosition + move;
            // ensure new position stays within wall bounds
            newPos.x = Mathf.Clamp(newPos.x, xBoundLeft, xBoundRight);
            newPos.z = Mathf.Clamp(newPos.z, zBoundBack, zBoundForward);
        
            xrOrigin.Origin.transform.localPosition = newPos;
        }
        
        public void Move(InputAction.CallbackContext ctx)
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

        internal void StartCollisionVibration()
        {
            if (leftController != null && leftController.gameObject.activeSelf)
                leftController.ExternalVibrationStart(120);
            if (rightController != null)
                rightController.ExternalVibrationStart(120);
        }
        internal void StopCollisionVibration()
        {
            if (leftController != null)
                leftController.ExternalVibrationStop();
            if (rightController != null)
                rightController.ExternalVibrationStop();
        }

        public void RegisterControllerReference(ControllerVibrator controller, bool isRight)
        {
            if (isRight)
            {
                rightController = controller;
                RunExperiment.Instance.RightController = controller;
            }
            else
            {
                leftController = controller;
                RunExperiment.Instance.LeftController = controller;
            }
        }
    }
}
