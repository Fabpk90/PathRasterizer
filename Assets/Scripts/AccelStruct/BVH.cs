﻿
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BVHNode
{
    public Bounds boundingBox; //this is the union of more aabb
    public int meshIndexStart;
    public int meshCount;
    public BVHNode[] children;
    public int splitAxis; //use this to accelerate the traversing

    public BVHNode(int meshIndexStart, int meshCount, Bounds bounds)
    {
        this.meshIndexStart = meshIndexStart;
        this.meshCount = meshCount;
        boundingBox = bounds;
        children = null;
    }

    public void InitChildren(BVHNode child0, BVHNode child1, int axis)
    {
        children = new BVHNode[2];
        children[0] = child0;
        children[1] = child1;
        
        boundingBox.Encapsulate(child0.boundingBox);
        boundingBox.Encapsulate(child1.boundingBox);
        meshIndexStart = -1;
    }
}

public struct LBVH
{
    public Bounds bounds;
    public int offset; //could be child offset or primitive index
}

public struct PrimitiveInfo
{
    public Bounds bounds;
    public int primitiveIndex;
}

public struct MeshInfo
{
    //TODO: add material properties
    public Matrix4x4 localToWorld;
}

//used to project the centroids of the bounds
//to estimate the cost of the subdivision
public class BucketInfo
{
    public Bounds bounds;
    public int count;
}

public class BVH
{
    public BVHNode root;
    public List<PrimitiveInfo> primitivesInfo;

    private static int nBuckets = 12;
    public BucketInfo[] buckets = new BucketInfo[nBuckets];

    public uint nodeCreated; //used to allocate the memory of the linear tree

    private static int maxPrimsInNode = 1;
    
    public BVH(Bounds[] bounds, Matrix4x4[] transformPositions)
    {
        /*
         * TODO: 1) Create the primInfo for each mesh 2) create the Tree with pointer and SAH 3) Linearize
         */
        
        primitivesInfo = new List<PrimitiveInfo>(transformPositions.Length);
        
        for (int i = 0; i < transformPositions.Length; i++)
        {
            PrimitiveInfo info = new PrimitiveInfo()
            {
                bounds = bounds[i],
                primitiveIndex = i
            };
            
            primitivesInfo.Add(info);
        }

        for (int i = 0; i < nBuckets; i++)
        {
            buckets[i] = new BucketInfo();
        }
        
        List<PrimitiveInfo> orderedInfos = new List<PrimitiveInfo>(primitivesInfo.Count);
        
        root = RecursiveBuild(0, primitivesInfo.Count, ref orderedInfos);

        primitivesInfo = orderedInfos;
    }
    
    public BVHNode RecursiveBuild(int start, int end, ref List<PrimitiveInfo> orderedInfos)
    {
        nodeCreated++;
        //taking the first as start because a new bounds starts at (0, 0, 0) and is considered a box to encapsulate
        Bounds bounds = primitivesInfo[start].bounds;
        for (int i = start + 1; i < end; i++)
        {
            bounds.Encapsulate(primitivesInfo[i].bounds);
        }

        int primitives = end - start;
        int mid = (end + start) / 2;
        int axis = -1;

        //we create a node, we can't divide anymore
        if (primitives == 1)
        {
            Debug.Log("Creating a node cause 1 primitive remains");
            int primitivesOffset = orderedInfos.Count;
            
            int primitiveNumber = primitivesInfo[start].primitiveIndex;
            orderedInfos.Add(primitivesInfo[primitiveNumber]);
            
            return new BVHNode(primitivesOffset, 1, bounds);
        }
        else //subdivide
        {
            //we select the best axis to split
            Bounds centroidBounds = new Bounds();
            centroidBounds.center = primitivesInfo[start].bounds.center;

            for (int i = start + 1; i < end; i++)
            {
                centroidBounds.Encapsulate(primitivesInfo[i].bounds.center);
            }

            axis = GetMaximumExtentAxis(centroidBounds);

            //if the boxes are aligned
            //super strange case which should not occur
            if (Math.Abs(centroidBounds.max[axis] - centroidBounds.min[axis]) < 0.0001f)
            {
                int primitivesOffset = orderedInfos.Count;
                for (int i = start; i < end; i++)
                {
                    int primitiveNumber = primitivesInfo[i].primitiveIndex;
                    orderedInfos.Add(primitivesInfo[primitiveNumber]);
                }

                return new BVHNode(primitivesOffset, primitives, bounds);
            }
            else
            {
                if (primitives == 2)
                {
                    //using equally sized subsets as it's cheaper
                    //just swap the 2 primitives
                    // if a.centroid[dim] < b.centroid[dim]
                    Debug.Log(start);
                    Debug.Log(end);
                    Debug.Log(mid);
                    var prim0 = primitivesInfo[start];
                    var prim1 = primitivesInfo[start + 1];

                    if (prim0.bounds.center[axis] > prim1.bounds.center[axis])
                    {
                        primitivesInfo[start] = prim1;
                        primitivesInfo[start + 1] = prim0;
                    }
                }
                else
                {
                    //We compute the buckets count and bounds

                    for (int i = start; i < end; i++)
                    {
                        int bucket = (int) (nBuckets * GetRelativePosition(centroidBounds, primitivesInfo[i].bounds.center)[axis]);

                        //the last bucket is useless for subdividing
                        if (bucket == nBuckets) bucket = nBuckets - 1;

                        buckets[bucket].count++;
                        buckets[bucket].bounds.Encapsulate(primitivesInfo[i].bounds);
                    }
                    
                    //computing the cost for splitting each bucket
                    
                    float[] costs = new float[nBuckets];

                    //formula of the cost = Ttrav + pA * sum(Tisect(a[i])) + pB * sum(Tisect(b[i]))
                    
                    //following a bit the pbr book for the values
                    //Ttrav will be 0.5f relative to the cost of dividing

                    //computing the cost of splitting
                    for (int i = 0; i < nBuckets - 1; i++)
                    {
                        Bounds b0 = buckets[0].bounds;
                        int count0 = buckets[0].count;
                        
                        Bounds b1 = buckets[i+1].bounds;
                        int count1 = buckets[i+1].count;
                        
                        //left side of the division
                        for (int j = 1; j <= i; j++)
                        {
                            b0.Encapsulate(buckets[j].bounds);
                            count0 += buckets[j].count;
                        }

                        for (int j = i+2; j < nBuckets; j++)
                        {
                            b1.Encapsulate(buckets[j].bounds);
                            count1 += buckets[j].count;
                        }

                        costs[i] = 0.5f 
                                   + (count0 * GetSurfaceArea(b0) + count1 * GetSurfaceArea(b1)) 
                                   / GetSurfaceArea(bounds);
                    }
                    
                    //find the min bucket, to minimize the cost
                    float minCost = costs[0];
                    int minCostSplitBucket = 0;

                    for (int i = 1; i < nBuckets - 1; i++)
                    {
                        if (minCost > costs[i])
                        {
                            minCost = costs[i];
                            minCostSplitBucket = i;
                        }
                    }

                    float leafCost = primitives;

                    //the cost of creating a leaf is greater than the bucket chosen
                    if (primitives > maxPrimsInNode || minCost < leafCost)
                    {
                        //reorders the array of primsInfo 
                        //resets the mid
                        List<PrimitiveInfo> trueList = new List<PrimitiveInfo>(end - start);
                        List<PrimitiveInfo> falseList = new List<PrimitiveInfo>(end - start);

                        for (int i = start; i < end; i++)
                        {
                            int bucket = (int) (nBuckets * GetRelativePosition(centroidBounds, primitivesInfo[i].bounds.center)[axis]);

                            //the last bucket is useless for subdividing
                            if (bucket == nBuckets) bucket = nBuckets - 1;
                            
                            if(bucket <= minCostSplitBucket)
                                trueList.Add(primitivesInfo[i]);
                            else
                                falseList.Add(primitivesInfo[i]);
                        }

                        mid = start + trueList.Count;

                        //actually splitting
                        for (int i = 0; i < trueList.Count; i++)
                        {
                            primitivesInfo[i + start] = trueList[i];
                        }

                        for (int i = 0; i < falseList.Count; i++)
                        {
                            primitivesInfo[i + mid] = falseList[i];
                        }
                    }
                    else //we create a leaf
                    {
                        int primitivesOffset = orderedInfos.Count;
                        for (int i = start; i < end; i++)
                        {
                            int primitiveNumber = primitivesInfo[i].primitiveIndex;
                            orderedInfos.Add(primitivesInfo[primitiveNumber]);
                        }

                        return new BVHNode(primitivesOffset, primitives, bounds);
                    }
                }
            }
        }
        
        BVHNode node = new BVHNode(0, 0, new Bounds());
        node.InitChildren(RecursiveBuild(start, mid, ref orderedInfos), 
            RecursiveBuild(mid, end, ref orderedInfos), axis);

        return node;
    }

    private LBVH FlattenTree(BVHNode root)
    {
        return new LBVH();
    }

    private Vector3 GetRelativePosition(Bounds b, Vector3 point)
    {
        Vector3 o = point - b.min;
        if ( b.max.x > b.min.x) o.x /= b.max.x - b.min.x;
        if (b.max.y > b.min.y) o.y /= b.max.y - b.min.y;
        if (b.max.z > b.min.z) o.z /= b.max.z - b.min.z;
        return o;
    }

    public float GetSurfaceArea(Bounds bounds)
    {
        Vector3 diagonal = bounds.max - bounds.min;

        return 2.0f * (diagonal.x * diagonal.y + diagonal.x * diagonal.z + diagonal.y + diagonal.z);
    }
    /// <summary>
    /// 
    /// </summary>
    /// <returns>0 -> X 1 -> Y 2 -> Z</returns>
    public int GetMaximumExtentAxis(Bounds bounds)
    {
        Vector3 max = bounds.max;

        if (max.x > max.y && max.x > max.z)
            return 0;
        if (max.y > max.z)
            return 1;

        return 2;
    }
}