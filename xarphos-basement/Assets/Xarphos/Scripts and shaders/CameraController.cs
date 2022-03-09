using UnityEngine;
using UnityEngine.XR;
using InputContext = UnityEngine.InputSystem.InputAction.CallbackContext;

namespace Xarphos.Scripts
{
    public class CameraController : MonoBehaviour
    {

        [SerializeField] private float moveSpeed;
        [SerializeField] private float scrollSpeed;
        [SerializeField] private float rotationSpeed;
        Vector3 orientation = new Vector3(0, 90, 0);

        private Vector3 _directionIn;
        private Vector3 _rotationIn;


        private void Start()
        {
            transform.eulerAngles = orientation;
        }

        public void Move(InputContext ctx)
        {
            var dir = ctx.ReadValue<Vector2>();
            _directionIn = moveSpeed * Time.deltaTime * (transform.forward * dir.y + transform.right * dir.x);
            _directionIn.y = 0;
        }

        public void Rotate(InputContext ctx)
        {
            var rot = ctx.ReadValue<Vector2>();
            _rotationIn = rotationSpeed * Time.deltaTime * (transform.up * rot.x + -transform.forward * rot.y);
            _rotationIn.z = 0;
        }

        void Update()
        {
            transform.position += _directionIn;
            transform.eulerAngles += _rotationIn;
        }
    }
}