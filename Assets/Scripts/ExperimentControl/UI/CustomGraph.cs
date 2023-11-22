using System;
using System.Globalization;
using System.Linq;
using Xarphos.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI.Extensions;

// For a discussion of the code, see: https://www.hallgrimgames.com/blog/2018/11/25/custom-unity-ui-meshes

namespace ExperimentControl.UI
{
    public class CustomGraph : MonoBehaviour
    {
        [SerializeField] [Range(float.Epsilon, float.MaxValue)] private float secondsToDisplay = 10f;
        [SerializeField] [Range(50, int.MaxValue)] private int xPoints = 250;
        private float deltaX;
        private int lastIdx;
        private int updateSize;
        private float lastUpdatedValue;
        private float x0, x1, y0, y1;
        [Range(0, .5f-float.Epsilon)]
        public float padding = .01f;

        private Vector2[] points;
        private float tickAtStart;
        private bool initialised;

        public float xAxisLength
        {
            get => secondsToDisplay;
            set => UpdateXAxisLength(value);
        }

        public int xAxisPointCount
        {
            get => xPoints;
            set => UpdateXAxisResolution(value);
        }

        [Header("References")]
        public UILineRenderer graph;
        public TMP_Text yMin, yMax, xMin, xMax;

        private void Awake()
        {
            initialised = true;
            
            points = Enumerable.Repeat(new Vector2(1,0), xPoints).ToArray();
            lastIdx = 0;
            
            var now = DateTime.Now;
            var tick = now.Hour * 3600f + now.Minute * 60f + now.Second + now.Millisecond / 1000f;
            tickAtStart = tick - Time.realtimeSinceStartup;

            x0 = 0f;
            xMin.text = $"{x0:F}";
            x1 = x0 + secondsToDisplay;
            xMax.text = $"{x1:F}";
            y0 = int.Parse(yMin.text, NumberStyles.Float);
            y1 = int.Parse(yMax.text, NumberStyles.Float);
        }

        private void FixedUpdate()
        {
            GetNewData();
            UpdateGraph();
        }

        private void GetNewData()
        {
            if (!(Math.Abs(EyeTracking.Timings[0] - float.MinValue) > float.Epsilon * 3)) return;

            Vector2[] tmp;
            
            float[] copy = new float[EyeTracking.Timings.Length];
            EyeTracking.Timings.CopyTo(copy, 0);
            int idx = EyeTracking.TimingIdx;
            for (int i = 0; i < copy.Length; i += 1)
            {
                var prev = (idx - 1 + copy.Length) % copy.Length;
                if (copy[idx] - copy[prev] < 0)
                    break;
                idx = (idx - 1 + copy.Length) % copy.Length;
            }
            idx = (idx + 1) % copy.Length;
            if (lastIdx > 0)
            {
                tmp = new Vector2[EyeTracking.Timings.Length];
                tmp[0] = new Vector2(copy[idx], 1f / (copy[idx] - points[lastIdx].x));
            }
            else
            {
                tmp = new Vector2[EyeTracking.Timings.Length - 1];
            }
            
            for (int i = 0; i < tmp.Length; i += 1)
            {
                var next = (idx + i) % copy.Length;
                var prev = (next - 1 + copy.Length) % copy.Length;

                var x = copy[next];
                var y = 1f / (x - copy[prev]);
                
                tmp[i] = new Vector2(x, y);
            }

            if (xPoints - lastIdx > tmp.Length)
            {
                tmp.CopyTo(points, lastIdx + 1);
                lastIdx += tmp.Length;
            }
            else
            {
                var discard = tmp.Length - (xPoints - lastIdx);
                var remain = points.Length - discard ;
                Array.Copy(points, discard, points, 0, remain);
                tmp.CopyTo(points, xPoints - tmp.Length);
                lastIdx = xPoints - 1;
            }
        }

        private void UpdateGraph()
        {
            x0 = points[0].x - tickAtStart;
            xMin.text = $"{x0:F}";
            x1 = points[lastIdx].x - tickAtStart;
            xMax.text = $"{x1:F}";

            graph.Points = points.Select(
                v => new Vector2(
                    Rescale(v.x - tickAtStart, x0, x1),
                    Rescale(v.y, y0, y1))
                ).ToArray(); 
        }

        private float Rescale(float val, float oldMin, float oldMax)
        {
            float newMin = padding;
            float newMax = 1 - padding;

            return (newMax - newMin) * (val - oldMin) / (oldMax - oldMin) + newMin;
        }
        
        private void UpdateXAxisResolution(int value)
        {
            if (value == xPoints || value < 1) return;
            value = Mathf.Clamp(value, EyeTracking.Timings.Length, int.MaxValue);
            if (value < xPoints)
            {
                points = points.AsSpan(xPoints - value - 1).ToArray();
            }
            else
            {
                var tmp = new Vector2[value];
                points.CopyTo(tmp, 0);
                points = tmp;
            }
            
            xPoints = value;
            ResetScaling();
        }

        private void UpdateXAxisLength(float len)
        {
            if (len < float.Epsilon) return;
            secondsToDisplay = len;
            ResetScaling();
        }

        public void ResetScaling()
        {
            if (!initialised) Awake();
            UpdateGraph();
        }
    }
}