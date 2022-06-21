using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class HallwayCreator : MonoBehaviour
{
    private float sectionSize = 2f;
    private float wallHeight = 2.7f;
    public GameObject HW_Empty, HW_smL, HW_smC, HW_smR, HW_lgLC, HW_lgCR, HW_lgLR, HW_Wall;
    

    private enum HallwaySections
    {
        Empty, 
        SmallLeft, SmallRight, SmallCenter,
        LargeLeftCenter, LargeCenterRight, LargeLeftRight
    }

    private Dictionary<HallwaySections, GameObject> section2Gameobject;

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

    private void Start()
    {
        CreateHallway(hallway1, 0);
        CreateHallway(hallway2, sectionSize*2);
        CreateHallway(hallway3, sectionSize*4);
    }

    private void CreateHallway(HallwaySections[] layout, float startX)
    {
        var parent = new GameObject("Hallway Parent");
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
        // empty room behind start
        inst(HW_Empty, -sectionSize);
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
        // wall at the start
        wall = Instantiate(
            HW_Wall,
            new Vector3(startX, wallHeight / 2f, currZ + sectionSize / 2f),
            Quaternion.Euler(0, 90, 0),
            parent.transform
        );
        scale = wall.transform.localScale;
        scale = new Vector3(scale.x, scale.y, 3);
        wall.transform.localScale = scale;
    }

    private Action<GameObject, float> GetInstantiationShorthand(float xpos, GameObject parent)
    {
        return (prefab, zPos) => Instantiate(prefab, new Vector3(xpos, 0, zPos), Quaternion.identity, parent.transform);
    }
}
