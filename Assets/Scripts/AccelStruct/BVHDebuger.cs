﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Compression;
using UnityEngine;

public class BVHDebuger : MonoBehaviour
{
    //public MeshRenderer[] renderers;
    public BVH bvh; 
    // Start is called before the first frame update

    private void OnDrawGizmos()
    {
        if (bvh != null)
        {
           Draw(bvh.flatTree);
        }
    }

    private void Draw(LBVH[] bvhFlatTree)
    {
        for (int i = 0; i < bvhFlatTree.Length; i++)
        {
            //if((bvhFlatTree[i].primAndAxis & 4294901760) > 0)
                //Gizmos.DrawWireCube(bvhFlatTree[i].bounds.center, bvhFlatTree[i].bounds.size);
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
