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
        children = new int[8];
    }

    public bool IsLeaf()
    {
        return isLeaf;
    }
    
    public void Add(ref List<LinearNode> nodes, ref LinearNodeData[] data, int nodeIndex)
    {
        Vector3 middle = (data[nodeIndex].vertexA + data[nodeIndex].vertexB + data[nodeIndex].vertexC) / 3; 
        if (!bounds.Contains(middle)) return;

        var bitFlag = middle.x > bounds.center.x ? 4 : 0;
        bitFlag |= middle.y > bounds.center.y ? 2 : 0;
        bitFlag |= middle.z > bounds.center.z ? 1 : 0;

        if (IsLeaf())
        {
            if (!isEmpty) // we need to separate
            {
                for (int i = 0; i < 8; i++)
                {
                    float size = (bounds.extents.x * 2);
                    var node = new LinearNode(bounds.center + size * 0.25f * offsets[i]
                        , Vector3.one * size * 0.5f, nodes.Count);
                    nodes.Add(node);
                    children[i] = nodes.Count - 1;
                    nodes[children[i]].Add(ref nodes, ref data, nodeIndex);
                }

                nodes[children[bitFlag]].Add(ref nodes, ref data, dataIndex); 
                
                isLeaf = false;
            }
            else
            {
                dataIndex = nodeIndex;
                isEmpty = false;
            }
        }
        else
        {
            nodes[children[bitFlag]].Add(ref nodes, ref data, nodeIndex);
        }
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
        
        for (int i = 0; i < data.Length; i++)
        {
            nodes[0].Add(ref nodes, ref data, i);
        }
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
            for (int i = 0; i < 8; i++)
            {
                if (nodes[n.children[i]].bounds.IntersectRay(r))
                {
                    if (nodes[n.children[i]].IsLeaf())
                    {
                        if(!nodes[n.children[i]].isEmpty)
                            return nodes[n.children[i]];
                    }
                    else
                    {
                        n = nodes[n.children[i]];
                    }
                }
            }
        }

        Debug.Log("found nothing");
        return null;
    }
}