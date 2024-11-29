using Grid_Parameters;
using UnityEngine;
[ExecuteInEditMode]
public class Simplemapper : MonoBehaviour
{
  [SerializeField] private MotionLink[] links;
  private Vector4[] posArray;
  private Vector4[] rotArray;
  private Vector4[] valArray;
  private Vector4[] extraValArray;
  void Start()
  {
    CreateLinkArrays();
    UpdateAllLinkVectors();
    UpdateLinks();
    UpdateGlobalShader();
  }
  // Update is called once per frame
  void Update()
  {
    UpdateLinks();
    UpdateGlobalShader();
  }
  void CreateLinkArrays()
  {
    posArray = new Vector4[100];
    rotArray = new Vector4[100];
    valArray = new Vector4[100];
    extraValArray = new Vector4[100];
  }
  void UpdateGlobalShader()
  {
    Shader.SetGlobalVectorArray("_GlobalPositionsArray", posArray);
    Shader.SetGlobalVectorArray("_GlobalRotationsArray", rotArray);
    Shader.SetGlobalVectorArray("_GlobalMotionValues", valArray);
    Shader.SetGlobalVectorArray("_GlobalExtraValues", extraValArray);
    Shader.SetGlobalInt("_StopAt", finalindex);
  }
  int finalindex = -1;
  void UpdateLinks()
  {
    finalindex = -1;
    if (links == null || links.Length == 0)
    {
      finalindex = 0;
      return;
    }
    for (int i = 0; i < links.Length; i++)
    {
      if (i >= 100)
      {
        finalindex = i;
        break;
      }
      posArray[i] = links[i].transform.position;
      Quaternion quaternion = links[i].transform.rotation;
      rotArray[i] = new Vector4(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
      posArray[i].w = links[i].motionValues.Velocity.magnitude;
      switch (links[i].shape)
      {
        case LinkShape.Box:
          extraValArray[i].x = 0;
          break;
        case LinkShape.Cylinder:
          extraValArray[i].x = 1;
          break;
        case LinkShape.Ellipsoid:
          extraValArray[i].x = 2;
          break;
        case LinkShape.Capsule:
          extraValArray[i].x = 3;
          break;

      }
      if (links[i].motionValues.UseCC)
        extraValArray[i].y = MapDirectionToFloat(links[i].transform.position);
      else
        extraValArray[i].y = MapDirectionOfTravelToFloat(links[i].motionValues.Velocity, links[i].transform);
      valArray[i] = links[i].motionValues.Vector;
      finalindex = i + 1;
    }
  }
  void UpdateAllLinkVectors()
  {
    for (int i = 0; i < links.Length; i++)
    {
      links[i].motionValues.UpdateVector();
    }
  }
  public float MapDirectionOfTravelToFloat(Vector3 velocity, Transform rbTransform)
  {
    Vector3 projectedVelocity = new(velocity.x, 0, velocity.z);
    if (projectedVelocity.magnitude == 0) return 0;
    projectedVelocity.Normalize();
    Vector3 forward = new Vector3(rbTransform.forward.x, 0, rbTransform.forward.z).normalized;
    float angle = Mathf.Atan2(projectedVelocity.z, projectedVelocity.x) - Mathf.Atan2(forward.z, forward.x);
    if (angle < 0) angle += 2 * Mathf.PI;
    return angle; // Angle in radians
  }
  public float MapDirectionToFloat(Vector3 forward)
  {
    Vector3 projectedForward = new(forward.x, 0, forward.z);
    if (projectedForward.magnitude == 0) return 0;
    projectedForward.Normalize();
    float angle = Mathf.Atan2(projectedForward.z, projectedForward.x);
    if (angle < 0) angle += 2 * Mathf.PI;
    return angle;//angle in radians
  }
}
