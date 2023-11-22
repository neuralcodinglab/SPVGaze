using System;
using Xarphos;
using DataHandling;
using UnityEngine;

namespace ExperimentControl
{
    public class CollisionHandler : MonoBehaviour
    {
        public Transform head;
        public Collider coll;
        
        private InputHandler inputHandler;
        internal bool InBox;
        internal bool inPlayground;

        private void Start()
        {
            coll ??= GetComponent<Collider>();
            inputHandler = SingletonRegister.GetInstance<InputHandler>();
            inputHandler.onChangeHallway.AddListener( hw => inPlayground = hw.Name == HallwayCreator.Hallways.Playground.ToString());
        }

        private void LateUpdate()
        {
            var myPos = transform.position;
            var headPos = head.position;
            if (Math.Abs(myPos.x - headPos.x) < 1e-5 && Math.Abs(myPos.z - headPos.z) < 1e-5)
                return;
            
            headPos.y = myPos.y;
            transform.position = headPos;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (RunExperiment.Instance.recordingPaused && !inPlayground) return;
            if (collision.contactCount < 1) return;
            if (InBox)
            {
                Debug.LogWarning("Walked into a box, while we thought we are in a box.");
            }
            InBox = true;
            StaticDataReport.CollisionCount += 1;
        
            // start vibration pattern
            inputHandler.StartCollisionVibration();
        }

        private void OnCollisionExit(Collision other)
        {
            if (RunExperiment.Instance.recordingPaused && !inPlayground) return;
            InBox = false;
            // stop vibration pattern
            inputHandler.StopCollisionVibration();
        }
    }
}
