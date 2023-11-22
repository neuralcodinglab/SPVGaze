using System;
using DataHandling;
using UnityEngine;
using Xarphos;

namespace ExperimentControl
{
    public class CheckpointHandler : MonoBehaviour
    {
        public Transform head;
        public Collider coll;
        
        internal bool InCheckpoint => checkpointID != int.MaxValue;
        private int checkpointID = int.MaxValue;
        private bool checkpointInFrontOnEnter = false;
        private float lastCheckPointZ = float.MinValue;

        private void Start()
        {
            SingletonRegister.GetInstance<InputHandler>().onChangeHallway.AddListener(_ =>
            {
                checkpointID = int.MaxValue;
                checkpointInFrontOnEnter = false;
                lastCheckPointZ = float.MinValue;
            });
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
            if (RunExperiment.Instance.recordingPaused) return;
            
            var other = collision.gameObject;
            // starting to cross checkpoint
            if (checkpointID != int.MaxValue)
            {
                Debug.LogWarning("Checkpoint ID was not reset correctly.");
            }
            checkpointID = other.GetInstanceID();
            var prevCpZ = lastCheckPointZ;
            lastCheckPointZ = other.transform.position.z;

            if (Math.Abs(prevCpZ - float.MinValue) > 1e-5 && 
                Mathf.Abs(Mathf.Abs(prevCpZ - lastCheckPointZ) - HallwayCreator.SectionSize) > 1e-5)
            {
                Debug.LogWarning("It seems we missed a checkpoint? Increasing counter;");
                StaticDataReport.InZone += Math.Sign(lastCheckPointZ - prevCpZ);
            }
            
            checkpointInFrontOnEnter = Mathf.Sign(lastCheckPointZ - transform.position.z) > 0;
        }

        private void OnCollisionExit(Collision collision)
        {
            if (RunExperiment.Instance.recordingPaused) return;
            
            var other = collision.gameObject;
            if (other.GetInstanceID() != checkpointID)
            {
                Debug.LogWarning("Exited a Checkpoint with a different ID than last entered. What?");
                checkpointID = int.MaxValue;
                return;
            }
            checkpointID = int.MaxValue;

            var checkpointIsBehind = Mathf.Sign(other.transform.position.z - transform.position.z) < 0;
            switch (checkpointInFrontOnEnter)
            {
                // moved forward in the hallway
                case true when checkpointIsBehind:
                    StaticDataReport.InZone += 1;
                    break;
                // moved backwards through checkpoint
                case false when !checkpointIsBehind:
                    StaticDataReport.InZone -= 1;
                    break;
                default:
                    break; // did not cross, but entered and moved back out            
            }
            // Debug.Log($"Passed checkpoint! Now in zone {StaticDataReport.InZone}");
        }
    }
}
