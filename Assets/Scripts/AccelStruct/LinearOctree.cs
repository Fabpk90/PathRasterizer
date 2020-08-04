using System;
using System.Collections.Generic;
using UnityEngine;

public class LinearNode
{
    public Bounds bounds;

    public bool isLeaf;
    public bool isEmpty;
    public int dataIndex;
    public int[] children;
    
    private static Vector3[] offsets =
    {
        new Vector3(-1, -1, -1),
        new Vector3(-1, -1, 1),
        new Vector3(-1, 1, -1),
        new Vector3(-1, 1, 1),
        new Vector3(1, -1, -1),
        new Vector3(1, -1, 1),
        new Vector3(1, 1, -1),
        new Vector3(1, 1, 1)
    };

    public LinearNode(Vector3 position, Vector3 size, int dataIndex)
    {
        bounds = new Bounds(position, size);
        
        isLeaf = true;
        isEmpty = true;
        
        this.dataIndex = dataIndex;
        children = new int[8] {-1, -1, -1, -1, -1, -1, -1, -1};
    }

    public bool IsLeaf()
    {
        return isLeaf;
    }
    
    public bool Add(ref List<LinearNode> nodes, ref LinearNodeData[] data, int nodeIndex)
    {
        Vector3 middle = (data[nodeIndex].vertexA + data[nodeIndex].vertexB + data[nodeIndex].vertexC) / 3; 
        if (!bounds.Contains(middle)) return false;

        var bitFlagMiddle = middle.x > bounds.center.x ? 4 : 0;
        bitFlagMiddle |= middle.y > bounds.center.y ? 2 : 0;
        bitFlagMiddle |= middle.z > bounds.center.z ? 1 : 0;
        
        float boundsSize = bounds.size.x;

        if (IsLeaf())
        {
            if (!isEmpty) // we need to separate
            {
                if (bounds.size.x < 0.001f) return false;

                
                //replace the initial node
                Vector3 middleDataIndex =
                        (data[dataIndex].vertexA + data[dataIndex].vertexB + data[dataIndex].vertexC) / 3;
                
                var bitFlagDataIndex = middleDataIndex.x > bounds.center.x ? 4 : 0;
                bitFlagDataIndex |= middleDataIndex.y > bounds.center.y ? 2 : 0;
                bitFlagDataIndex |= middleDataIndex.z > bounds.center.z ? 1 : 0;
            
                var n = new LinearNode(bounds.center + boundsSize * 0.25f * offsets[bitFlagDataIndex]
                    , Vector3.one * boundsSize * 0.5f, nodes.Count);
                
                children[bitFlagMiddle] = nodes.Count;
                nodes.Add(n);

                n.Add(ref nodes, ref data, dataIndex);
                    
                //place the new node
                var node = new LinearNode(bounds.center + boundsSize * 0.25f * offsets[bitFlagMiddle]
                    , Vector3.one * boundsSize * 0.5f, nodes.Count);
                children[bitFlagMiddle] = nodes.Count;
                nodes.Add(node);

                if (!node.Add(ref nodes, ref data, nodeIndex))
                    Debug.Log("Fuck not inserted");

                isLeaf = false;
            }

            dataIndex = nodeIndex;
            isEmpty = false;
            return true;
        }

        //if the child doesn't exist we create it
        if (children[bitFlagMiddle] == -1)
        {
            //place the new node
            var node = new LinearNode(bounds.center + boundsSize * 0.25f * offsets[bitFlagMiddle]
                , Vector3.one * boundsSize * 0.5f, nodes.Count);
            children[bitFlagMiddle] = nodes.Count;
            nodes.Add(node);

            return node.Add(ref nodes, ref data, nodeIndex);
        }

        return nodes[children[bitFlagMiddle]].Add(ref nodes, ref data, nodeIndex);
    }
}
//stores triangles
public struct LinearNodeData
{
    public Vector3 vertexA;
    public Vector3 vertexB;
    public Vector3 vertexC;

    public LinearNodeData(Vector3 a, Vector3 b, Vector3 c)
    {
        vertexA = a;
        vertexB = b;
        vertexC = c;
    }
}

public class LinearOctree
{
    public List<LinearNode> nodes;
    public LinearNodeData[] data;

    /// <summary>
    /// Assumes that triangles are feed, triples
    /// </summary>
    public LinearOctree(Vector3 position, Vector3 size, Vector3[] points)
    {
        nodes = new List<LinearNode>(points.Length)
        {
            new LinearNode(position, size, 0)
        };
        
        data = new LinearNodeData[points.Length / 3];

        for (int i = 0; i < data.Length; i++)
        {
            data[i] = new LinearNodeData(points[(i * 3)], points[(i * 3) + 1], points[(i * 3) + 2]);
        }

        uint t = 0;
        for (int i = 0; i < data.Length; i++)
        {
            if (!nodes[0].Add(ref nodes, ref data, i))
                t++;
        }

        Debug.Log(t);
    }

    public LinearNode GetNodeFromRay(Ray r)
    {
        if (!nodes[0].bounds.IntersectRay(r))
            return null;

        return GetNode(nodes[0], r);
    }

    private LinearNode GetNode(LinearNode n, Ray r)
    {
        for (int j = 0; j < 1000; j++)
        {
            float minDistance = Single.MaxValue;
            int index = -1;
            for (int i = 0; i < 8; i++)
            {
                if (n.children[i] != -1 
                    && nodes[n.children[i]].bounds.IntersectRay(r, out var distance))
                {
                    if (distance < minDistance)
                    {
                        index = n.children[i];
                        minDistance = distance;
                    }
                }
            }

            if (index == -1)
            {
                Debug.Log("fuck nothing");
                return null;
            }
            
            Debug.Log(index);
            Debug.Log(nodes[index].bounds);
            
            if (nodes[index].IsLeaf())
            {
                Debug.Log("It's a leaf");
                
                if(!nodes[index].isEmpty)
                    return nodes[index];
                Debug.Log("found zero things");
                return null;
            }
 
            n = nodes[index];
            
        }

        Debug.Log("found nothing");
        return null;
    }
}