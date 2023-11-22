namespace Xarphos.Simulation.Utility
{
    public static class MathUtilities
    {
        /// <summary>
        /// Returns an evenly spaced ranged of doubles
        /// </summary>
        /// <param name="count">Number of elements in the array.</param>
        /// <param name="start">First value of range.</param>
        /// <param name="stop">Last value of range. Included if endpoint=True, excluded if false</param>
        /// <param name="endpoint">If true, stop is the last value in the range. If false, stop is not included.</param>
        /// <returns></returns>
        public static double[] CreateLinearlySpacedRange(int count, double start, double stop, bool endpoint=true)
        {
            var range = new double[count];
            var step = (stop - start) / (endpoint ? count - 1 : count);

            for (var i = 0; i < count; i++)
            {
                range[i] = start + step * i;
            }

            return range;
        }
    }
}