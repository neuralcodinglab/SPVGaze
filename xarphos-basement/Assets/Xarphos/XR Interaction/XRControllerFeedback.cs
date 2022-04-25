using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;

namespace Xarphos.XR_Interaction
{
    [RequireComponent(typeof(Collider))]
    public class XRControllerFeedback : MonoBehaviour
    {
        private XRBaseController _controller;
        private bool _inCollison = false;

        private void Start()
        {
            _controller = GetComponentInParent<XRBaseController>();
            _controller.SendHapticImpulse(1, 2);
        }

        private void Update()
        {
            if (_inCollison)
            {
                _controller.SendHapticImpulse(.5f, Time.deltaTime * 5);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            _inCollison = true;
        }

        private void OnTriggerExit(Collider other)
        {
            _inCollison = false;
            _controller.SendHapticImpulse(0,0);
        }
    }
}
