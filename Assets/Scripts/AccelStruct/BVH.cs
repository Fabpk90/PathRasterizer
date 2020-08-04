
using System;
using System.Collections.Generic;
using UnityEngine;

public class BVHNode
{
    public Bounds boundingBox; //this is the union of more aabb
    public int vertexStartOffset;
    public int vertexCount;
    public int meshIndex;
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

public class BVH
{
    public BVHNode root;
    public Vector3[] vertices;
    public List<PrimitiveInfo> primitivesInfo;

    public MeshInfo meshInfo;
    
    public BVH(Vector3[] vertices, int[] ebo, Matrix4x4 transformPosition)
    {
        this.vertices = new Vector3[ebo.Length];

        int index = 0;
        foreach (int i in ebo)
        {
            this.vertices[index++] = vertices[i];
        }
        
        meshInfo = new MeshInfo();
        meshInfo.localToWorld = transformPosition;
        primitivesInfo = new List<PrimitiveInfo>(this.vertices.Length / 3);
        
        CreatePrimitveInfoFrom(ref this.vertices);
    }

    private void CreatePrimitveInfoFrom(ref Vector3[] blockOfVertex)
    {
        Vector3 min = Vector3.zero, max = Vector3.zero;
        Bounds b = new Bounds();

        for (int i = 0 ; i < blockOfVertex.Length; i += 3)
        {
            min.x = Mathf.Min(blockOfVertex[i].x, min.x);
            min.y = Mathf.Min(blockOfVertex[i].y, min.y);
            min.z = Mathf.Min(blockOfVertex[i].z, min.z);
            
            max.x = Mathf.Max(blockOfVertex[i].x, max.x);
            max.y = Mathf.Max(blockOfVertex[i].y, max.y);
            max.z = Mathf.Max(blockOfVertex[i].z, max.z);
        }
        

        b.SetMinMax(min, max); // using property resets the center, thanks decompiler :)
        b.center = meshInfo.localToWorld.GetColumn(3);
        b.center += new Vector3(0, max.y / 2); //centering the box because the pivot is on the feet on the mesh
      
      PrimitiveInfo info = new PrimitiveInfo()
      {
          bounds = b,
          primitiveIndex = 0
      };
            
      primitivesInfo.Add(info);
    }
}