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

    private int size;

    private void Start()
    {
        r = Camera.main.ScreenPointToRay(Input.mousePosition);
        size = 1000;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            r = Camera.main.ScreenPointToRay(Input.mousePosition);
            Stopwatch s = Stopwatch.StartNew();
            linearN = octree.GetLinearNode(r);
            s.Stop();

            if (linearN != null)
            {
                print("yes");
            }
        }
        Debug.DrawLine(r.origin, r.origin + r.direction * 50.0f, Color.red);

    }

    private void OnDrawGizmos()
    {
        if (linearN != null)
        {
            Gizmos.DrawWireSphere(linearN.bounds.center, 1f);
        }
    }
}
