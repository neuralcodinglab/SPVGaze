using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class ControllerVibrator : MonoBehaviour
{
    public XRController controller;
    [Range(0.01f, 0.99f)]
    public float amplitude = .7f;

    public float boxVibrationFrequency = 5f;
    public float wallVibrationFrequency = 2f;
    
    [SerializeField] private LayerMask boxLayer;
    [SerializeField] private LayerMask wallLayer;

    private bool externalVibrationOn = false;
    private float oldAmp = float.NegativeInfinity;

    private void Start()
    {
        controller = GetComponentInParent<XRController>();
        if (controller == null)
        {
            Debug.LogWarning("Found no controller in parent;");
            gameObject.SetActive(false);
        }
        GetComponentInParent<InputHandler>().RegisterControllerReference(this, transform.parent.name.ToLower().StartsWith("right"));
    }

    public void ExternalVibrationStart(float frequency, float amplitude=.7f)
    {
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
    
    private void OnTriggerEnter(Collider other)
    {
        var oLayer = other.gameObject.layer;
        Debug.Log($"Controller Collision with {other.gameObject.name} on layer {LayerMask.LayerToName(oLayer)}.");
        if (boxLayer == (boxLayer | (1 << oLayer)))
        {
            StopAllCoroutines();
            StartCoroutine(VibrationPattern(boxVibrationFrequency));
        }
        else if (wallLayer == (wallLayer | (1 << oLayer)))
        {
            StopAllCoroutines();
            StartCoroutine(VibrationPattern(wallVibrationFrequency));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log($"End of Controller Collision with {other.gameObject.name} on layer {LayerMask.LayerToName(other.gameObject.layer)}.");
        StopAllCoroutines();
    }
}
