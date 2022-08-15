using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace ExperimentControl
{
    public class ControllerVibrator : MonoBehaviour
    {
        public XRBaseController controller;
        [Range(0.01f, 0.99f)]
        public float amplitude = .7f;

        public float boxVibrationFrequency = 5f;
        public float wallVibrationFrequency = 2f;
    
        [SerializeField] private LayerMask boxLayer;
        [SerializeField] private LayerMask wallLayer;

        private bool externalVibrationOn = false;
        private float oldAmp = float.NegativeInfinity;

        internal bool inBox;
        internal bool inWall;

        private void Start()
        {
            controller = GetComponentInParent<XRBaseController>();
            if (controller == null)
            {
                Debug.LogWarning("Found no controller in parent;");
                gameObject.SetActive(false);
            }
            SenorSummarySingletons.GetInstance<InputHandler>().RegisterControllerReference(this, transform.parent.name.ToLower().StartsWith("right"));
        }

        public void ExternalVibrationStart(float frequency, float amplitude=.7f)
        {
            if ( !isActiveAndEnabled ) return;
            
            externalVibrationOn = true;
            if (amplitude is > 0.0f and < 1.0f)
            {
                oldAmp = amplitude;
                this.amplitude = amplitude;
            }

            StartCoroutine(VibrationPattern(frequency));
        }
        public void ExternalVibrationStop()
        {
            externalVibrationOn = false;
            if (oldAmp is > 0.0f and < 1.0f)
            {
                amplitude = oldAmp;
                oldAmp = float.PositiveInfinity;
            }

            StopAllCoroutines();
        }

        private IEnumerator VibrationPattern(float frequency)
        {
            // Trigger haptics every second
            var delay = new WaitForSeconds(1f / frequency);
 
            while (true)
            {
                yield return delay;
                SendHaptics();
            }
        }
 
        void SendHaptics()
        {
            controller.SendHapticImpulse(amplitude, 0.1f);
        }
    
        private void OnCollisionEnter(Collision collision)
        {
            if (externalVibrationOn) return;
            if (collision.contactCount < 1) return;
            
            var triggeringCollider = collision.GetContact(0).thisCollider;
            if (!triggeringCollider.gameObject.CompareTag(gameObject.tag)) return;
            
            var other = collision.collider;
            var oLayer = other.gameObject.layer;
            // Debug.Log($"Controller Collision with {other.gameObject.name} on layer {LayerMask.LayerToName(oLayer)}.");
            if (boxLayer == (boxLayer | (1 << oLayer)))
            {
                StopAllCoroutines();
                inBox = true;
                StartCoroutine(VibrationPattern(boxVibrationFrequency));
            }
            else if (wallLayer == (wallLayer | (1 << oLayer)))
            {
                StopAllCoroutines();
                inWall = true;
                StartCoroutine(VibrationPattern(wallVibrationFrequency));
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            if (externalVibrationOn) return;
            if (!(inBox || inWall)) return;

            inBox = false;
            inWall = false;
            var o = collision.collider.gameObject;
            // Debug.Log($"End of Controller Collision with {o.name} on layer {LayerMask.LayerToName(o.layer)}.");
            StopAllCoroutines();
        }
    }
}
