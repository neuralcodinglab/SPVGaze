using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using Valve.VR;

public class InputHandler : MonoBehaviour
{
    public XRController rightController;
    public XRController leftController;

    public bool useRightInput = true;

    private bool rightAvailable = true, leftAvailable = true;

    private void Start()
    {
        if (rightController == null)
        {
            rightAvailable = false;
            useRightInput = false;
            Debug.LogWarning("InputHandler has no right controller assigned.");
        }

        if (leftController == null)
        {
            leftAvailable = false;
            useRightInput = true;
            Debug.LogWarning("InputHandler has no left controller assigned.");
        }

        if (!leftAvailable && !rightAvailable)
        {
            Debug.LogError("InputHandler has no controller assigned. Cannot process input.");
        }
    }

    private void Update()
    {
        var controller = useRightInput ? rightController : leftController;
        var primary = controller.inputDevice.TryGetFeatureValue(new InputFeatureUsage<Vector2>("Primary2DAxis"), out var inputPrimary);
        var secondary = controller.inputDevice.TryGetFeatureValue(new InputFeatureUsage<Vector2>("Secondary2DAxis"), out var inputSecondary);

        
        
        if (primary)
        {
            Debug.Log($"Primary Input: {inputPrimary.x} ; {inputPrimary.y}");
        }
        if (secondary)
        {
            Debug.Log($"Primary Input: {inputSecondary.x} ; {inputSecondary.y}");
        }
    }
}
