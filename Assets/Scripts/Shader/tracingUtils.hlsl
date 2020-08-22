struct Sphere
{
    float3 position;
    float radius;
    float3 color;
    float3 specular;
    float3 emission;
    float smoothness;
};

struct Ray
{
    float3 a;
    float3 direction;
    float3 invDir;
    float3 energy; //to be multiplied by the color touched
};

struct RayHit
{
    float3 color;
    float3 specular;
    float3 normal;
    float3 emission;
    float smoothness;
    
    float alpha;
    float3 position;
};

struct Mesh 
{
    float4x4 localToWorld;
    int eboOffset;
    int eboCount;
    float4 color;
};

struct MeshBoundingBox
{
    int indexMesh;
    float3 max;
    float3 min;
};

struct LBVH
{
    float3 minBox;
    float3 maxBox;
    int offset; //could be second child offset or primitive index // 96
    int primAndAxis; // LMB -> node  RMB -> axis
};

float seed;

static const float EPSILON = 1e-8;

// https://stackoverflow.com/questions/12964279/whats-the-origin-of-this-glsl-rand-one-liner
float rand(float2 _pixel)
{
    float result = frac(sin(seed / 100.0f * dot(_pixel, float2(12.9898f, 78.233f))) * 43758.5453f);
    seed += 1.0f;
    return result;
}

//used to convert [0, 1] value to a correct range for phong rendering
//to be used as it is in the standard shader
float SmoothnessToPhongAlpha(float s)
{
    return pow(1000.0f, s * s);
}

float linearizeDepth(float depth, float2 cameraPlanes)
{
    float nearPlane = cameraPlanes.x;
    float farPlane = cameraPlanes.y;
    return (2 * nearPlane) / (farPlane + nearPlane - depth * (farPlane - nearPlane));
}

//https://tavianator.com/fast-branchless-raybounding-box-intersections-part-2-nans/
bool rayBoxIntersection(float3 minBox, float3 maxBox, Ray r)
{
    float t1 = (minBox[0] - r.a[0]) * r.invDir[0];
    float t2 = (maxBox[0] - r.a[0]) * r.invDir[0];

    float tmin = min(t1, t2);
    float tmax = max(t1, t2);
 
    [unroll]
    for (int i = 1; i < 3; ++i) 
    {
        t1 = (minBox[i] - r.a[i]) * r.invDir[i];
        t2 = (maxBox[i] - r.a[i]) * r.invDir[i];
 
        tmin = max(tmin, min(t1, t2));
        tmax = min(tmax, max(t1, t2));
        
        //tmin = max(tmin, min(min(t1, t2), tmax));
        //tmax = min(tmax, max(max(t1, t2), tmin));
     
    }

    return tmax >= max(tmin, 0.0);// || tmax < 0.0;
}

float3 getPointAt(Ray r, float alpha)
{
    return r.a + (r.direction * alpha);
}

// http://fileadmin.cs.lth.se/cs/Personal/Tomas_Akenine-Moller/pubs/raytri_tam.pdf
//returns true if hits a triangle, filling t with the distance and u and v with barycentric [0] [1]  [2] = 1 - u - v
bool TriangleIntersection(Ray r, inout RayHit hit, float3 a, float3 b, float3 c)
{
    float t, u, v;
    float3 edge1 = b - a;
    float3 edge2 = c - a;
    
    float3 pVec = cross(r.direction, edge2);
    float det = dot(pVec, edge1);
    
    
    //back-face culling
    if(det < EPSILON)
        return false;
        
    float invDet = 1.0f / det;
    
    float3 tVec = r.a - a;
    u = dot(tVec, pVec) * invDet;
    
    if(u < 0 || u > 1.0f)
        return false;
    
    float3 qVec = cross(tVec, edge1);
    v = dot(r.direction, qVec) * invDet;
    
    if(v < 0 || v + u > 1.0f)
        return false;
        
    t = dot(edge2, qVec) * invDet;
    
    if(t < 0)
        return false;
    
    hit.position = getPointAt(r, t);
    hit.alpha = t;
    hit.normal = normalize(cross(edge1, edge2));
    hit.specular = 0.5f;
    hit.smoothness = 1.0f;
    hit.emission = 0.0f;
    hit.color = 1.0f;
    return true;
}

bool TriangleIntersectionHit(Ray r, float3 a, float3 b, float3 c)
{
    float t, u, v;
    float3 edge1 = b - a;
    float3 edge2 = c - a;
    
    float3 pVec = cross(r.direction, edge2);
    float det = dot(pVec, edge1);
    
    //back-face culling
    if(det < EPSILON)
        return false;
        
    float invDet = 1.0f / det;
    
    float3 tVec = r.a - a;
    u = dot(tVec, pVec) * invDet;
    
    if(u < 0 || u > 1.0f)
        return false;
    
    float3 qVec = cross(tVec, edge1);
    v = dot(r.direction, qVec) * invDet;
    
    if(v < 0 || v + u > 1.0f)
        return false;
        
    t = dot(edge2, qVec) * invDet;
    
    return t > 0;
}

bool sphereIntersection(Ray r, inout RayHit hit, Sphere sphere, float tMin, float tMax)
{
    float3 AC = r.a - sphere.position;
    float c = dot(AC, AC) - sphere.radius * sphere.radius;
    float a = dot(r.direction, r.direction);
    float b = dot(r.direction, AC);

    float discriminant = b*b - a * c; // aka delta

    float tempAlpha;

    if(discriminant > 0)
    {
        float root = sqrt(discriminant);
        tempAlpha = (-b - root) / a;

        if(tMin < tempAlpha && tMax > tempAlpha)
        {
            float3 surfaceHit = getPointAt(r, tempAlpha);
            float3 normal = (surfaceHit - sphere.position) / sphere.radius;
            
            hit.position = surfaceHit;
            hit.normal =  normal;
            
            hit.color = sphere.color;
            hit.alpha = tempAlpha;
            hit.specular = sphere.specular;
            hit.smoothness = sphere.smoothness;
            hit.emission = sphere.emission;
            return true;
        }

        tempAlpha = (-b + root) / a;

        if(tMin < tempAlpha && tMax > tempAlpha)
        {
            float3 surfaceHit = getPointAt(r, tempAlpha);
            float3 normal = (surfaceHit - sphere.position) / sphere.radius;
            
            hit.position = surfaceHit;
            hit.normal =  normal;
            
            hit.color = sphere.color;
            hit.alpha = tempAlpha;
            hit.specular = sphere.specular;
            hit.smoothness = sphere.smoothness;
            hit.emission = sphere.emission;
            return true;
        }
    }
    
    return false;
}