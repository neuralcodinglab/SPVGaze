using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExperimentControl
{
    public class HallwayCreator : MonoBehaviour
    {
        private float sectionSize = 2f;
        private float wallHeight = 2.7f;
        public GameObject HW_Empty, HW_smL, HW_smC, HW_smR, HW_lgLC, HW_lgCR, HW_lgLR, HW_Wall;

        public struct Hallway
        {
            public string Name;
            public float StartX;
            public GameObject WallStart;
            public GameObject WallEnd;
            public GameObject WallLeft;
            public GameObject WallRight;
        }
        
        #region Hallway Definition & Mapping
        private enum HallwaySections
        {
            Empty, 
            SmallLeft, SmallRight, SmallCenter,
            LargeLeftCenter, LargeCenterRight, LargeLeftRight
        }

        internal enum Hallways
        {
            Playground, Hallway1, Hallway2, Hallway3
        }

        private Dictionary<HallwaySections, GameObject> section2Gameobject;
        internal static Dictionary<Hallways, Hallway> HallwayObjects;

        private void Awake()
        {
            section2Gameobject = new Dictionary<HallwaySections, GameObject>
            {
                { HallwaySections.Empty, HW_Empty },
                { HallwaySections.SmallLeft, HW_smL },
                { HallwaySections.SmallRight, HW_smR },
                { HallwaySections.SmallCenter, HW_smC },
                { HallwaySections.LargeLeftCenter, HW_lgLC },
                { HallwaySections.LargeCenterRight, HW_lgCR },
                { HallwaySections.LargeLeftRight, HW_lgLR }
            };
            HallwayObjects = new Dictionary<Hallways, Hallway>();
        }

        // map 1 & 5 ; 2 & 3 ; 6 & 7
        // 1 - 5
        private HallwaySections[] hallway1 = 
        {
            HallwaySections.SmallCenter,
            HallwaySections.SmallCenter,
            HallwaySections.LargeCenterRight,
            HallwaySections.SmallLeft,
            HallwaySections.SmallRight,
            HallwaySections.SmallCenter,
            HallwaySections.LargeLeftRight,
            HallwaySections.SmallLeft,
            HallwaySections.SmallLeft,
            HallwaySections.LargeLeftCenter, // transition to 5
            HallwaySections.SmallCenter,
            HallwaySections.SmallRight,
            HallwaySections.LargeLeftRight,
            HallwaySections.SmallCenter,
            HallwaySections.SmallLeft,
            HallwaySections.SmallRight,
            HallwaySections.LargeCenterRight,
            HallwaySections.SmallCenter,
            HallwaySections.SmallCenter
        };
        // 2 - 3
        private HallwaySections[] hallway2 =
        {
            HallwaySections.SmallLeft,
            HallwaySections.SmallCenter,
            HallwaySections.LargeCenterRight,
            HallwaySections.SmallLeft,
            HallwaySections.SmallLeft,
            HallwaySections.SmallRight,
            HallwaySections.LargeLeftCenter,
            HallwaySections.SmallRight,
            HallwaySections.SmallLeft,
            HallwaySections.LargeCenterRight, // transition to 3
            HallwaySections.SmallRight,
            HallwaySections.SmallLeft,
            HallwaySections.LargeLeftRight,
            HallwaySections.SmallCenter,
            HallwaySections.SmallLeft,
            HallwaySections.SmallCenter,
            HallwaySections.LargeCenterRight,
            HallwaySections.SmallCenter,
            HallwaySections.SmallLeft
        };
        // 6 - 7
        private HallwaySections[] hallway3 =
        {
            HallwaySections.SmallLeft,
            HallwaySections.SmallCenter,
            HallwaySections.LargeCenterRight,
            HallwaySections.SmallRight,
            HallwaySections.SmallLeft,
            HallwaySections.SmallCenter,
            HallwaySections.LargeLeftRight,
            HallwaySections.SmallLeft,
            HallwaySections.SmallCenter,
            HallwaySections.LargeLeftCenter, // transition to 7
            HallwaySections.SmallRight,
            HallwaySections.SmallRight,
            HallwaySections.LargeLeftCenter,
            HallwaySections.SmallCenter,
            HallwaySections.SmallRight,
            HallwaySections.SmallLeft,
            HallwaySections.LargeCenterRight,
            HallwaySections.SmallCenter,
            HallwaySections.SmallLeft
        };

        private HallwaySections[] playground =
        {
            HallwaySections.SmallCenter,
            HallwaySections.LargeLeftRight
        };
        #endregion

        private void Start()
        {
            CreateHallway(playground, -sectionSize*2, Hallways.Playground);
            CreateHallway(hallway1, 0, Hallways.Hallway1);
            CreateHallway(hallway2, sectionSize*2, Hallways.Hallway2);
            CreateHallway(hallway3, sectionSize*4, Hallways.Hallway3);
        }

        private void CreateHallway(IEnumerable<HallwaySections> layout, float startX, Hallways which)
        {
            var parent = new GameObject(Enum.GetName(typeof(Hallways), which));
            var collect = new Hallway
            {
                Name = parent.name,
                StartX = startX
            };
            var inst = GetInstantiationShorthand(startX, parent);
        
            // wall at the start
            var wall = Instantiate(
                HW_Wall,
                new Vector3(startX, wallHeight / 2f, -1.5f*sectionSize),
                Quaternion.Euler(0, 90, 0),
                parent.transform
            );
            var scale = wall.transform.localScale;
            scale = new Vector3(scale.x, scale.y, 3);
            wall.transform.localScale = scale;
            collect.WallStart = wall;
            // empty room behind start
            var room1 = inst(HW_Empty, -sectionSize);
            wall = room1.transform.Find("Wall_L").gameObject;
            collect.WallLeft = wall;
            wall = room1.transform.Find("Wall_R").gameObject;
            collect.WallRight = wall;
            // empty starting room
            inst(HW_Empty, 0);
            // generate hallway
            float currZ = sectionSize;
            foreach (var section in layout)
            {
                var prefab = section2Gameobject[section];
                inst(prefab, currZ);
                currZ += sectionSize;
            }
            // empty last room
            inst(HW_Empty, currZ);
            // wall at the end
            wall = Instantiate(
                HW_Wall,
                new Vector3(startX, wallHeight / 2f, currZ + sectionSize / 2f),
                Quaternion.Euler(0, 90, 0),
                parent.transform
            );
            scale = wall.transform.localScale;
            scale = new Vector3(scale.x, scale.y, 3);
            wall.transform.localScale = scale;
            collect.WallEnd = wall;

            HallwayObjects[which] = collect;
        }

        private Func<GameObject, float, GameObject> GetInstantiationShorthand(float xpos, GameObject parent)
        {
            return (prefab, zPos) => Instantiate(prefab, new Vector3(xpos, 0, zPos), Quaternion.identity, parent.transform);
        }
    }
}
