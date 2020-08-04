using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BVHDebuger : MonoBehaviour
{

    public BVH bvh; 
    // Start is called before the first frame update
    void Start()
    {
        Mesh m = GetComponent<MeshFilter>().mesh;
        bvh = new BVH(m.vertices, m.triangles, transform.localToWorldMatrix);
    }

    private void OnDrawGizmos()
    {
        if (bvh != null)
        {
            foreach (PrimitiveInfo info in bvh.primitivesInfo)
            {
                Gizmos.DrawWireCube(info.bounds.center, info.bounds.size);   
            }
            
        }
    }
}
