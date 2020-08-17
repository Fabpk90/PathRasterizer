using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class CameraRayController : MonoBehaviour
{
    public Octree octree;
    public Ray r;

    private OctreeNode n;
    private LinearNode linearN;

    public BVHDebuger bvh;

    private int size;
    private float distance;

    private void Start()
    {
        r = Camera.main.ScreenPointToRay(Input.mousePosition);
        size = 1000;

        distance = -1;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            r = Camera.main.ScreenPointToRay(Input.mousePosition);
            Stopwatch s = Stopwatch.StartNew();
           /* var res = bvh.bvh.RayIntersection(r);
            s.Stop();

            if (res.Item1)
            {
                print("yes");
                distance = res.Item2;
            }*/
        }
        Debug.DrawLine(r.origin, r.origin + r.direction * 50.0f, Color.red);

    }

    private void OnDrawGizmos()
    {
        if (distance > 0)
        {
            Gizmos.DrawWireSphere(r.GetPoint(distance), 1f);
        }
    }
}
