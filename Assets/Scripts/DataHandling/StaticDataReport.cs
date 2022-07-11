using System.IO;
using UnityEngine.Device;
using UnityEngine.Events;

namespace DataHandling
{
    public static class StaticDataReport
    {
        // public static readonly string DataDir = Path.Join(Application.persistentDataPath, "Data");

        private static int _inZone = 0;
        public static UnityEvent<int> OnChangeInZone = new();
        public static int InZone
        {
            get => _inZone;
            set
            {
                if (value == _inZone) return;
                _inZone = value;
                OnChangeInZone ??= new UnityEvent<int>();
                OnChangeInZone.Invoke(_inZone);
            }
        }
        
        private static int _collisionCount = 0;
        public static UnityEvent<int> OnChangeCollisionCount = new();
        public static int CollisionCount
        {
            get => _inZone;
            set
            {
                if (value == _collisionCount) return;
                _collisionCount = value;
                OnChangeCollisionCount ??= new UnityEvent<int>();
                OnChangeCollisionCount.Invoke(_collisionCount);
            }
        }
    }
}
