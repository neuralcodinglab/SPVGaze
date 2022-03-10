using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using CallbackContext = UnityEngine.InputSystem.InputAction.CallbackContext;

namespace Xarphos.Scripts
{
    public class InputHandler : MonoBehaviour
    {
        public float moveSpeedScale = 0.2f;
        public float rotateSpeedScale = 0.2f;
        private Vector3 _move;
        private float _scale, _rotate;

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
