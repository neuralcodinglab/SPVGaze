using System;
using UnityEngine;
using UnityEngine.XR;

namespace Xarphos.XR_Interaction
{
    [RequireComponent(typeof(Collider))]
    public class XRControllerFeedback : MonoBehaviour
    {
        private InputDevice _controller;
        private bool _inCollison = false;

        private void Awake()
        {
            _controller = InputDevices.GetDeviceAtXRNode(gameObject.name.ToLower().Contains("right") ? XRNode.RightHand : XRNode.LeftHand);
        }

        private void Start()
        {
            if (!_controller.TryGetHapticCapabilities(out var capabalities)) return;
            Debug.Log(capabalities);
        }

        private void Update()
        {
            if (_inCollison)
            {
                _controller.SendHapticImpulse(0, .5f, Time.deltaTime * 5);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            _inCollison = true;
            Debug.Log("Entering Controller Collision.");
        }

        private void OnTriggerExit(Collider other)
        {
            _inCollison = false;
            _controller.StopHaptics();
            Debug.Log("Exiting Controller Collision.");
        }
    }
}
