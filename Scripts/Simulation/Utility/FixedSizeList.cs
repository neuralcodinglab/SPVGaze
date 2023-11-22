using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace Xarphos.Simulation.Utility
{
    /// <summary>
    /// A list that has a fixed size. When the list is full, adding a new element will remove the first element.
    /// </summary> 
    public class FixedSizeList<T> : List<T>
    {
        public int Size { get; }
        
        public FixedSizeList(int size) : base(size)
        {
            Size = size;
        }

        public bool GetLast(out T obj)
        {
            obj = default;
            if (Count == 0) return false;
            obj = this.ElementAt(Count - 1);
            return true;
        }

        public List<T> GetRecentHalf()
        {
            if (Count <= 1) return null;
            var idx = Mathf.CeilToInt(Count / 2f);
            return GetRange(idx, Count - idx);
        }
        
        public IList<T> GetOlderHalf()
        {
            if (Count == 0) return null;
            var idx = Mathf.CeilToInt(Count / 2f);
            return GetRange(0, idx);
        }

        public new void Add(T obj)
        {
            if (Count == Size)
            {
                RemoveAt(0);
                base.Add(obj);
                if (Count != Capacity || Count != Size)
                {
                    Debug.LogError("FixedSizeList not so fixed.");
                }
            }
            else
                base.Add(obj);
        }
    }
}