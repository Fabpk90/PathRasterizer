using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Compression;
using UnityEngine;

public class BVHDebuger : MonoBehaviour
{

    public MeshRenderer[] renderers;
    public BVH bvh; 
    // Start is called before the first frame update

    private void Start()
    {
        BuildBVH(renderers);
    }

    public void BuildBVH(MeshRenderer[] renderers)
    {
        Bounds[] bounds = new Bounds[renderers.Length];
        Matrix4x4[] localToWorlds = new Matrix4x4[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            bounds[i] = renderers[i].bounds;
            localToWorlds[i] = renderers[i].transform.localToWorldMatrix;
        }

        bvh = new BVH(bounds, localToWorlds);
        print(bvh.nodeCreated);
    }

    private void OnDrawGizmos()
    {
        if (bvh != null)
        {
           Draw(bvh.root);
        }
    }

    private void Draw(BVHNode node)
    {
        if (node.children == null)
        {
            Gizmos.DrawWireCube(node.boundingBox.center, node.boundingBox.size);
        }
        else
        {
            Draw(node.children[0]);
            Draw(node.children[1]);
        }
    }
}
