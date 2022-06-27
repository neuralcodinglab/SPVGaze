using Unity.XR.CoreUtils;
using UnityEngine;

[RequireComponent(typeof(XROrigin))]
public class InputHandler : MonoBehaviour
{
    public float speed = 3f;
    public bool forwardIsHeadRotation = true;

    public GameObject leftWall, rightWall, startWall, endWall;
    public Collider coll;
    
    [SerializeField] private LayerMask boxLayer;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private LayerMask checkpointLayer;

    private XROrigin xrOrigin;
    private float xBoundLeft, xBoundRight, zBoundBack, zBoundForward;

    private ControllerVibrator rightController, leftController;

    // checkpoint tracking
    private int inZone = 0;
    private bool crossingCheckpoint = false;
    private bool checkpointInFrontOnEnter;
    private int checkpointID = int.MaxValue;
    
    // box collision tracking
    private bool inBox = false;
    private int collisionCount = 0;

    
    private void Start()
    {
        xrOrigin = GetComponent<XROrigin>();
        coll ??= GetComponent<Collider>();
        coll.isTrigger = true;

        // calculate boundaries player may not cross
        xBoundLeft = leftWall.GetComponent<Collider>().bounds.max.x + coll.bounds.extents.x;
        xBoundRight = rightWall.GetComponent<Collider>().bounds.min.x - coll.bounds.extents.x;
        zBoundBack = startWall.GetComponent<Collider>().bounds.max.z + coll.bounds.extents.z;
        zBoundForward = endWall.GetComponent<Collider>().bounds.min.z - coll.bounds.extents.z;
    }

    private void Update()
    {
        Move();
    }

    private void Move()
    {
        var hrz = Input.GetAxis("Horizontal");
        var vrt = Input.GetAxis("Vertical");
        var scale = Time.deltaTime * speed;
        var move = new Vector3(hrz * scale, 0, vrt * scale);

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

    private bool IsLayerInLayerMask(LayerMask mask, int layer) => mask == (mask | (1 << layer));
    private bool LayerInBoxMask(int layer) => IsLayerInLayerMask(boxLayer, layer);
    private bool LayerInCheckpointMask(int layer) => IsLayerInLayerMask(checkpointLayer, layer);

    private void StartCollisionVibration()
    {
        if (leftController != null)
            leftController.ExternalVibrationStart(120);
        if (rightController != null)
            rightController.ExternalVibrationStart(120);
    }
    private void StopCollisionVibration()
    {
        if (leftController != null)
            leftController.ExternalVibrationStop();
        if (rightController != null)
            rightController.ExternalVibrationStop();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        var oLayer = other.gameObject.layer;
        // walked into box
        if (LayerInBoxMask(oLayer))
        {
            if (inBox)
            {
                Debug.LogWarning("Walked into a box, while we thought we are in a box.");
            }
            inBox = true;
            collisionCount += 1;
            
            // start vibration pattern
            StartCollisionVibration();
        }
        // walked into a checkpoint
        else if (LayerInCheckpointMask(oLayer))
        {
            // starting to cross checkpoint
            crossingCheckpoint = true;
            if (checkpointID != int.MaxValue)
            {
                Debug.LogWarning("Checkpoint ID was not reset correctly.");
            }
            checkpointID = other.GetInstanceID();
            checkpointInFrontOnEnter = Mathf.Sign(other.transform.position.z - transform.position.z) > 0;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var oLayer = other.gameObject.layer;
        if (LayerInBoxMask(oLayer))
        {
            inBox = false;
            // stop vibration pattern
            StopCollisionVibration();
        }
        else if (LayerInCheckpointMask(oLayer))
        {
            checkpointID = int.MaxValue;
            if (other.GetInstanceID() != checkpointID)
            {
                Debug.LogWarning("Exited a Checkpoint with a different ID than last entered. What?");
                checkpointID = int.MaxValue;
                return;
            }

            var checkpointIsBehind = Mathf.Sign(other.transform.position.z - transform.position.z) < 0;
            switch (checkpointInFrontOnEnter)
            {
                // moved forward in the hallway
                case true when checkpointIsBehind:
                    inZone += 1;
                    break;
                // moved backwards through checkpoint
                case false when !checkpointIsBehind:
                    inZone -= 1;
                    break;
                default:
                    break; // did not cross, but entered and moved back out            
            }
        }
    }

    public void RegisterControllerReference(ControllerVibrator controller, bool isRight)
    {
        if (isRight)
            rightController = controller;
        else
            leftController = controller;
    }
}
