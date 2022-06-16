using System;
using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Assertions;
using Valve.VR;

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

    private int inZone = 0;
    private bool crossingCheckpoint = false;
    private float checkpointPos = 0;

    private void Start()
    {
        xrOrigin = GetComponent<XROrigin>();
        coll ??= GetComponent<Collider>();

        // calculate boundaries player may not cross
        xBoundLeft = leftWall.GetComponent<Collider>().bounds.max.x + coll.bounds.extents.x;
        xBoundRight = rightWall.GetComponent<Collider>().bounds.min.x - coll.bounds.extents.x;
        zBoundBack = startWall.GetComponent<Collider>().bounds.max.z + coll.bounds.extents.z;
        zBoundForward = endWall.GetComponent<Collider>().bounds.min.z - coll.bounds.extents.z;
        
        // coll.isTrigger = true;
        // StartCoroutine(CheckInput());
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

    private void OnTriggerEnter(Collider other)
    {
        var oLayer = other.gameObject.layer;
        // walked into box
        if (boxLayer == (boxLayer | (1 << oLayer)))
        {
            
        }
        // walked into a checkpoint
        else if (checkpointLayer == (checkpointLayer | (1 << oLayer)))
        {
            // starting to cross checkpoint
            crossingCheckpoint = true;
            checkpointPos = other.transform.position.z;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var oLayer = other.gameObject.layer;
        if (checkpointLayer == (checkpointLayer | (1 << oLayer)))
        {
            // starting to cross checkpoint
            crossingCheckpoint = false;
            if (transform.position.z > checkpointPos)
            {
                inZone += 1;
            }
            else // went backwards
            {
                
            }
        }
    }

    private IEnumerator CheckInput()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            var set = SteamVR_Actions.gazetracking.Move;

            Debug.Log("Move Set Attributes:\n" +
                      $"Is Active: {set.active}\n" + 
                      $"Is Bound: {set.activeBinding}\n" + 
                      $"Axis Value: {set.axis.x} / {set.axis.y}");
            
        }
    }
}
