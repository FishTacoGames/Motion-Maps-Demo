#ifndef GLOBAL_ARRAY_INCLUDE
#define GLOBAL_ARRAY_INCLUDE
//6.4 KB of memory.
float4 _GlobalPositionsArray[100];
float4 _GlobalRotationsArray[100];
float4 _GlobalMotionValues[100];
float4 _GlobalExtraValues[100];
uint _StopAt;
void IsWithinShape_float(float3 worldPosition, out float inRange, out float strength, out float velocityMagnitude, out float3 directionToObject)
{
    inRange = false;
    for (uint i = 0; i < 100; i++)
    {
        if (i == _StopAt) break;
        float3 boxposition = _GlobalPositionsArray[i].xyz;
        float3 offset = worldPosition.xyz - boxposition;
        float distanceSquared = dot(offset, offset);
        strength = _GlobalMotionValues[i].w;
        directionToObject = normalize(offset);
        float radius = 100; 
        if (distanceSquared <= radius * radius)
        {   
            float xsize = _GlobalMotionValues[i].x;
            float ysize = _GlobalMotionValues[i].y;
            float zsize = _GlobalMotionValues[i].z;

            float4 bRot = normalize(_GlobalRotationsArray[i]);

            // Convert offset into a quaternion
            float4 oQ = float4(offset, 0.0);
            float4 cjgR= float4(-bRot.xyz, bRot.w);

            // First multiplication:
            float4 q1 = float4(
                cjgR.w * oQ.x + cjgR.x * oQ.w + cjgR.y * oQ.z - cjgR.z * oQ.y,
                cjgR.w * oQ.y - cjgR.x * oQ.z + cjgR.y * oQ.w + cjgR.z * oQ.x,
                cjgR.w * oQ.z + cjgR.x * oQ.y - cjgR.y * oQ.x + cjgR.z * oQ.w,
                cjgR.w * oQ.w - cjgR.x * oQ.x - cjgR.y * oQ.y - cjgR.z * oQ.z
            );

            // Now, multiply by the box's rotation quaternion to fully rotate into local space
            float4 rQ = float4(
                q1.w * bRot.x + q1.x * bRot.w + q1.y * bRot.z - q1.z * bRot.y,
                q1.w * bRot.y - q1.x * bRot.z + q1.y * bRot.w + q1.z * bRot.x,
                q1.w * bRot.z + q1.x * bRot.y - q1.y * bRot.x + q1.z * bRot.w,
                q1.w * bRot.w - q1.x * bRot.x - q1.y * bRot.y - q1.z * bRot.z
            );

            float3 localPoint = rQ.xyz;
            float a = xsize * 0.5;
            float b = ysize * 0.5; 
            float c = zsize * 0.5; 
            float radiusSquared = a * a;
            bool inshape = false;
            int linkshape = _GlobalExtraValues[i].x;
            if (linkshape == 0) { inshape = abs(localPoint.x) <= a && abs(localPoint.y) <= b && abs(localPoint.z) <= c; }
            else if (linkshape == 1) { inshape = (localPoint.x * localPoint.x + localPoint.z * localPoint.z <= radiusSquared) && (abs(localPoint.y) <= b);} // full cylinder
            else if (linkshape == 2) { inshape = (localPoint.x * localPoint.x) / (radiusSquared) + (localPoint.y * localPoint.y) / (b * b) + (localPoint.z * localPoint.z) / (c * c) <= 1.0; }
            else if (linkshape == 3) 
            {             
                bool inCylinder = (localPoint.x * localPoint.x + localPoint.z * localPoint.z <= radiusSquared) && (abs(localPoint.y) <= b - a); // shortened cylinder
                float3 topHemisphereCenter = float3(0, b - a, 0);
                float3 dth = localPoint - topHemisphereCenter;
                float sqrdth = dot(dth, dth);
                bool inTopHemisphere = sqrdth <= radiusSquared;
                float3 bottomHemisphereCenter = float3(0, -b + a, 0);
                float3 dbh = localPoint - bottomHemisphereCenter;
				float sqrdbh = dot(dbh, dbh);
                bool inBottomHemisphere = sqrdbh <= radiusSquared;
                inshape = inCylinder || inTopHemisphere || inBottomHemisphere;
            }

            if (inshape)
            {                    
                velocityMagnitude = _GlobalPositionsArray[i].w;
                inRange = true;
                return;
            }
        }
    }
}
#endif