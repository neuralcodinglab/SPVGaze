using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
 
public class HapticsTest : MonoBehaviour
{
    // Adding the SerializeField attribute to a field will make it appear in the Inspector window
    // where a developer can drag a reference to the controller that you want to send haptics to.
    [SerializeField]
    XRBaseController controller;

    public float amp = .7f;
 
    protected void Start()
    {
        if (controller == null)
            Debug.LogWarning("Reference to the Controller is not set in the Inspector window, this behavior will not be able to send haptics. Drag a reference to the controller that you want to send haptics to.", this);
    }

    private void Update()
    {
        SendHaptics();
    }

    void SendHaptics()
    {
        if (controller != null)
            controller.SendHapticImpulse(amp, .1f);
    }
}