using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

/*
 * unsigned char a; // because an unsigned char is 8 bits

    int first_node(double tx0, double ty0, double tz0, double txm, double tym, double tzm){
    unsigned char answer = 0;   // initialize to 00000000
    // select the entry plane and set bits
    if(tx0 > ty0){
        if(tx0 > tz0){ // PLANE YZ
            if(tym < tx0) answer|=2;    // set bit at position 1
            if(tzm < tx0) answer|=1;    // set bit at position 0
            return (int) answer;
        }
    }
    else {
        if(ty0 > tz0){ // PLANE XZ
            if(txm < ty0) answer|=4;    // set bit at position 2
            if(tzm < ty0) answer|=1;    // set bit at position 0
            return (int) answer;
        }
    }
    // PLANE XY
    if(txm < tz0) answer|=4;    // set bit at position 2
    if(tym < tz0) answer|=2;    // set bit at position 1
    return (int) answer;
    }

    int new_node(double txm, int x, double tym, int y, double tzm, int z){
    if(txm < tym){
        if(txm < tzm){return x;}  // YZ plane
    }
    else{
        if(tym < tzm){return y;} // XZ plane
    }
    return z; // XY plane;
    }

    void proc_subtree (double tx0, double ty0, double tz0, double tx1, double ty1, double tz1, Node* node){
    float txm, tym, tzm;
    int currNode;

    if(tx1 < 0 || ty1 < 0 || tz1 < 0) return;
    if(node->terminal){
        cout << "Reached leaf node " << node->debug_ID << endl;
        return;
    }
    else{ cout << "Reached node " << node->debug_ID << endl;}

    txm = 0.5*(tx0 + tx1);
    tym = 0.5*(ty0 + ty1);
    tzm = 0.5*(tz0 + tz1);

    currNode = first_node(tx0,ty0,tz0,txm,tym,tzm);
    do{
        switch (currNode)
        {
        case 0: { 
            proc_subtree(tx0,ty0,tz0,txm,tym,tzm,node->children[a]);
            currNode = new_node(txm,4,tym,2,tzm,1);
            break;}
        case 1: { 
            proc_subtree(tx0,ty0,tzm,txm,tym,tz1,node->children[1^a]);
            currNode = new_node(txm,5,tym,3,tz1,8);
            break;}
        case 2: { 
            proc_subtree(tx0,tym,tz0,txm,ty1,tzm,node->children[2^a]);
            currNode = new_node(txm,6,ty1,8,tzm,3);
            break;}
        case 3: { 
            proc_subtree(tx0,tym,tzm,txm,ty1,tz1,node->children[3^a]);
            currNode = new_node(txm,7,ty1,8,tz1,8);
            break;}
        case 4: { 
            proc_subtree(txm,ty0,tz0,tx1,tym,tzm,node->children[4^a]);
            currNode = new_node(tx1,8,tym,6,tzm,5);
            break;}
        case 5: { 
            proc_subtree(txm,ty0,tzm,tx1,tym,tz1,node->children[5^a]);
            currNode = new_node(tx1,8,tym,7,tz1,8);
            break;}
        case 6: { 
            proc_subtree(txm,tym,tz0,tx1,ty1,tzm,node->children[6^a]);
            currNode = new_node(tx1,8,ty1,8,tzm,7);
            break;}
        case 7: { 
            proc_subtree(txm,tym,tzm,tx1,ty1,tz1,node->children[7^a]);
            currNode = 8;
            break;}
        }
    } while (currNode<8);
    }

    void ray_octree_traversal(Octree* octree, Ray ray){
    a = 0;

    // fixes for rays with negative direction
    if(ray.direction[0] < 0){
        ray.origin[0] = octree->size[0] - ray.origin[0];
        ray.direction[0] = - ray.direction[0];
        a |= 4 ; //bitwise OR (latest bits are XYZ)
    }
    if(ray.direction[1] < 0){
        ray.origin[1] = octree->size[1] - ray.origin[1];
        ray.direction[1] = - ray.direction[1];
        a |= 2 ; 
    }
    if(ray.direction[2] < 0){
        ray.origin[2] = octree->size[2] - ray.origin[2];
        ray.direction[2] = - ray.direction[2];
        a |= 1 ; 
    }

    double divx = 1 / ray.direction[0]; // IEEE stability fix
    double divy = 1 / ray.direction[1];
    double divz = 1 / ray.direction[2];

    double tx0 = (octree->min[0] - ray.origin[0]) * divx;
    double tx1 = (octree->max[0] - ray.origin[0]) * divx;
    double ty0 = (octree->min[1] - ray.origin[1]) * divy;
double ty1 = (octree->max[1] - ray.origin[1]) * divy;
double tz0 = (octree->min[2] - ray.origin[2]) * divz;
double tz1 = (octree->max[2] - ray.origin[2]) * divz;

if( max(max(tx0,ty0),tz0) < min(min(tx1,ty1),tz1) ){
    proc_subtree(tx0,ty0,tz0,tx1,ty1,tz1,octree->root);
}
}
 */

public class OctreeNode
{
    private Vector3[] offsets =
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
    
    //triangle
    public Vector3 a;
    public Vector3 b;
    public Vector3 c;

    public bool isEmpty;

    public Vector3 position;
    public float size;
    public Bounds bounds;
    
    public OctreeNode[] children;

    public OctreeNode(Vector3 position, float size)
    {
        this.position = position;
        this.size = size;

        isEmpty = true;
        
        bounds = new Bounds(position, Vector3.one * size);
    }

    public bool IsLeaf()
    {
        return children == null;
    }

    public void Add(Vector3 vertexA, Vector3 vertexB, Vector3 vertexC)
    {
        Vector3 middle = (vertexA + vertexB + vertexC) / 3; 
        if (!bounds.Contains(middle)) return;

        var bitFlag = middle.x > position.x ? 4 : 0;
        bitFlag |= middle.y > position.y ? 2 : 0;
        bitFlag |= middle.z > position.z ? 1 : 0;

        if (IsLeaf())
        {
            if (!isEmpty) // we need to separate
            {
                children = new OctreeNode[8];
                for (int i = 0; i < 8; i++)
                {
                    children[i] = new OctreeNode(position + size * 0.25f * offsets[i], size * 0.5f);
                    children[i].Add(a, b, c);
                    children[i].Add(vertexA, vertexB, vertexC);
                }
            }
            else
            {
                a = vertexA;
                b = vertexB;
                c = vertexC;

                isEmpty = false;
            }
        }
        else
        {
            children[bitFlag].Add(vertexA, vertexB, vertexC);
        }
    }
}
    
public class Octree : MonoBehaviour
{
    public OctreeNode root;
    public LinearOctree linear;

    public GameObject[] meshes;

    private Ray ray;
    private float max;

    private void Awake()
    {
        List<CombineInstance> combineInstances = new List<CombineInstance>(meshes.Length + 1);
        foreach (GameObject mesh1 in meshes)
        {
            CombineInstance m = new CombineInstance();
            m.mesh = mesh1.GetComponent<MeshFilter>().sharedMesh;
            m.transform = mesh1.transform.localToWorldMatrix;

            combineInstances.Add(m);
           // mesh1.SetActive(false);
        }
        

        Mesh meshCombined = new Mesh();
        meshCombined.CombineMeshes(combineInstances.ToArray());
        
        meshCombined.RecalculateBounds();
        meshCombined.RecalculateNormals();
        meshCombined.RecalculateTangents();
        meshCombined.Optimize();

        //GetComponent<MeshFilter>().mesh = meshCombined;
            
        var b = meshCombined.bounds.max;
        // var scale = transform.localScale;
        max = Mathf.Max(Mathf.Max(b.x, b.y), b.z);

        transform.position = meshCombined.bounds.center;
        //root = new OctreeNode(transform.position, max * 2.0f);

        MakeTree(meshCombined);
    }

    public void MakeTree(Mesh m)
    {
        var vertices = m.vertices;
        var ebo = m.triangles;

        Vector3[] vert = new Vector3[ebo.Length];

        for (int i = 0; i < vert.Length; i++)
        {
            vert[i] = vertices[ebo[i]];
        }
        
        linear = new LinearOctree(transform.position, Vector3.one * (max * 2.0f), vert);

         /*for (int i = 0; i < ebo.Length; i += 3)
         {
             root.Add(vertices[ebo[i]], vertices[ebo[i + 1]], vertices[ebo[i + 2]]);
         }*/
    }

    public LinearNode GetLinearNode(Ray r)
    {
        return linear.GetNodeFromRay(r);
    }

    public OctreeNode GetNodeFromRay(Ray r)
    {
        if (!root.bounds.IntersectRay(r))
            return null;

        return GetNode(root, r);
    }

    private OctreeNode GetNode(OctreeNode n, Ray r)
    {
        for (int j = 0; j < 10000; j++)
        {
            for (int i = 0; i < 8; i++)
            {
                if (n.children[i].bounds.IntersectRay(r))
                {
                    if (n.children[i].IsLeaf())
                    {
                        if(!n.children[i].isEmpty)
                            return n;
                    }
                    else
                    {
                        n = n.children[i];
                        i = 0;
                    }
                }
            }
        }
       

        return null;
    }

    private void OnDrawGizmos()
    {
        if (linear != null)
        {
            Gizmos.color = Color.blue;
            DrawOctree(linear.nodes[0]);
        }
    }

    private void DrawOctree(LinearNode l)
    {
        if (l.IsLeaf())
        {
            Gizmos.DrawWireCube(l.bounds.center, l.bounds.size);
            
        }
        else
        {
            for (int i = 0; i < 8; i++)
            {
                DrawOctree(linear.nodes[l.children[i]]);
            }
        }
        
    }
    private void DrawOctree(OctreeNode n)
    {
        if (n.IsLeaf())
        {
            Gizmos.DrawWireCube(n.position, n.bounds.size);
            return;
        }
        for (int i = 0; i < 8; i++)
        {
            DrawOctree(n.children[i]);
        }
    }
}