using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarkerManager : MonoBehaviour
{
    public Camera targetCam;
    
    public GameObject model;
    public int n = 9;
    public float dist = 5;
    public float radius = .3f;

    private GameObject[] markers;

    private void OnEnable()
    {
        markers = new GameObject[n+1];
        
        var parent = new GameObject("MarkerCollection").transform;
        parent.SetParent(targetCam.transform);
        parent.localPosition = Vector3.zero;
        markers[0] = parent.gameObject;

        var dir = targetCam.transform.forward;
        
        var marker = Instantiate(model, parent);
        marker.transform.localPosition = dir * 10;
        marker.transform.LookAt(transform);
        marker.SetActive(true);
        markers[1] = marker;

        var angle = 2 * Mathf.PI / (n - 1);
        var z = (2 * dist * dist - radius * radius) / (2 * dist);
        for (var i = 0; i < n - 1; i += 1)
        {
            var theta = angle * i;
            marker = Instantiate(model, parent);
            var x = radius * Mathf.Cos(theta);
            var y = radius * Mathf.Sin(theta);
            marker.transform.localPosition = new Vector3(x, y, z);
            marker.transform.LookAt(parent);
            marker.SetActive(true);
            markers[i + 2] = marker;
        }
    }

    private void OnDisable()
    {
        foreach (var marker in markers)
        {
            Destroy(marker);
        }
    }
}
