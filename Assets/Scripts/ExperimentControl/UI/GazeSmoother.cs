using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ViveSR;
using ViveSR.anipal.Eye;

namespace ExperimentControl.UI
{
    public class GazeSmoother : MonoBehaviour
    {
        public Slider input;
        public TMP_Text text;
        
        private void Awake()
        {
            input.onValueChanged.AddListener(OnChange);
            Invoke(nameof(UpdateSlider), 1f);
        }

        private void Update()
        {
            var delta = 0f;
            if (Keyboard.current.downArrowKey.isPressed)
            {
                delta -= .1f * Time.deltaTime;
            }
            if (Keyboard.current.upArrowKey.isPressed)
            {
                delta += .1f * Time.deltaTime;
            }
            if (delta == 0f) return;
            
            OnChange(input.value + delta);
            UpdateSlider();
        }

        private void UpdateSlider()
        {
            EyeParameter param = default;
            var res = SRanipal_Eye_API.GetEyeParameter(ref param);
            if (res != Error.WORK)
            {
                Debug.LogWarning("Failed to retrieve gaze parameters.");
                return;
            }
            var val = param.gaze_ray_parameter.sensitive_factor;
            input.value = (float)val;
            text.text = $"{val:F}";
        }

        private void OnChange(float val)
        {
            var param = new EyeParameter();
            param.gaze_ray_parameter.sensitive_factor = val;
            var res = SRanipal_Eye_API.SetEyeParameter(param);
            if (res != Error.WORK)
            {
                Debug.LogWarning($"Failed to update Eye Parameters with error: {Enum.GetName(typeof(Error), res)}");
                UpdateSlider();
                return;
            }
            text.text = $"{val:F}";
        }
    }
}
