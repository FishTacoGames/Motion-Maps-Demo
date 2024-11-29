using Grid_Parameters;
using UnityEditor;
using UnityEngine;

public class MotionLink : MonoBehaviour
{
  [Header("You can define the shape as a box,cylinder,ellipsoid or capsule regardless of collider type.\nIf a collider is assigned the OOBB sizes will be automatically calculated. \nEach link will look for an attached rigidbody to use for velocity, \nif you have a setup where the collider does not have a rigidbody\n" +
    "or you simply dont need one, \nyou can specify where it will read velocity from.\n" +
    "You can assign a character controller directly to the linked collider field\nand it will be used for velocity.\n In most cases the Velocity source can be left empty" +
    "\nIf you wish to ignore velocity from the linked collider,\nyou will need to set the Velocity source for force effects.")]
  public LinkShape shape;
  public MotionValues2 motionValues;

#if UNITY_EDITOR
  public bool runtimeDebugNot_On_Build = true;
  [Range(9, 60)] public int CircleQualityEditor_Draw_Only = 20;
  private void OnDrawGizmos()
  {
    if (Application.isPlaying && !runtimeDebugNot_On_Build || EditorApplication.isCompiling) return;
    motionValues.UpdateVector();
    if (motionValues.UseRb)
    {
      Vector3 projectedVelocity = new(motionValues.Velocity.x, 0, motionValues.Velocity.z);
      Gizmos.DrawLine(transform.position, transform.position + projectedVelocity.normalized * 3f);
      Handles.Label(transform.position + projectedVelocity.normalized * 3f, $"Speed: {projectedVelocity.magnitude:F2}");
    }
    else
    {
      Vector3 projectedforward = new(transform.forward.x, 0, transform.forward.z);
      Gizmos.DrawLine(transform.position, transform.position + projectedforward * 3f);
    }
    Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(transform.eulerAngles), Vector3.one);
    Gizmos.matrix = rotationMatrix;
    Gizmos.color = new Color(0.0f, 0.0f, 0.5f, 0.5f);
    Vector3 axisScaleVector = new(motionValues.Vector.x, motionValues.Vector.y, motionValues.Vector.z);
    switch (shape)
    {
      case LinkShape.Box:
        Gizmos.DrawCube(Vector3.zero, axisScaleVector);
        break;
      case LinkShape.Cylinder:
        DrawCylinder(axisScaleVector.x * 0.5f, axisScaleVector.y);
        break;
      case LinkShape.Ellipsoid:
        DrawEllipsoid(axisScaleVector);
        break;
      case LinkShape.Capsule:
        DrawCapsule(axisScaleVector);
        break;
    }
  }
  private void DrawCapsule(Vector3 dimensions)
  {
    float radius = dimensions.x * 0.5f;
    float cylinderHeight = dimensions.y - 2 * radius;
    if (cylinderHeight < 0) cylinderHeight = 0;
    DrawCylinder(radius, cylinderHeight);
    Vector3 topCenter = Vector3.zero + new Vector3(0,cylinderHeight * 0.5f,0);
    DrawHemisphere(topCenter, radius, true);
    Vector3 bottomCenter = Vector3.zero - new Vector3(0, cylinderHeight * 0.5f, 0);
    DrawHemisphere(bottomCenter, radius, false); 
  }

  private void DrawCylinder(float radius, float height)
  {
    Vector3 topCenter = 0.5f * height * Vector3.up;
    Vector3 bottomCenter = 0.5f * height * Vector3.down;
    DrawCircle(topCenter, radius);
    DrawCircle(bottomCenter, radius);

    // Draw connecting lines between the top and bottom circles
    for (int i = 0; i < CircleQualityEditor_Draw_Only; i++)
    {
      float angle1 = (i * Mathf.PI * 2) / CircleQualityEditor_Draw_Only;
      float angle2 = ((i + 1) * Mathf.PI * 2) / CircleQualityEditor_Draw_Only;

      Vector3 point1Top = topCenter + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
      Vector3 point2Top = topCenter + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);

      Vector3 point1Bottom = bottomCenter + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
      Vector3 point2Bottom = bottomCenter + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);

      Gizmos.DrawLine(point1Top, point1Bottom);
      Gizmos.DrawLine(point1Top, point2Top);
      Gizmos.DrawLine(point1Bottom, point2Bottom);
    }
  }
  private void DrawEllipsoid(Vector3 scale)
  {
    Matrix4x4 originalMatrix = Gizmos.matrix;
    Gizmos.matrix *= Matrix4x4.Scale(scale);
    Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
    // Restore the original Gizmos matrix
    Gizmos.matrix = originalMatrix;
  }
  private void DrawHemisphere(Vector3 center, float radius, bool isTop)
  {
    // Vertical angle range for the hemispheres
    float verticalAngleStart = isTop ? 0 : Mathf.PI / 2;  // Top hemisphere starts at 0, bottom starts at PI/2
    float verticalAngleEnd = isTop ? Mathf.PI / 2 : Mathf.PI;  // Top hemisphere ends at PI/2, bottom at PI

    float angleStep = (verticalAngleEnd - verticalAngleStart) / CircleQualityEditor_Draw_Only;  // Calculate the angle step based on segments

    // Loop over the vertical slices
    for (int i = 0; i < CircleQualityEditor_Draw_Only; i++)
    {
      float angle1 = verticalAngleStart + i * angleStep;
      float angle2 = verticalAngleStart + (i + 1) * angleStep;

      // Loop over the horizontal slices (circular slices)
      for (int j = 0; j < CircleQualityEditor_Draw_Only; j++)
      {
        float horizontalAngle1 = (j * Mathf.PI * 2) / CircleQualityEditor_Draw_Only;
        float horizontalAngle2 = ((j + 1) * Mathf.PI * 2) / CircleQualityEditor_Draw_Only;

        // Calculate the points for the hemisphere
        Vector3 point1 = center + new Vector3(
            Mathf.Sin(angle1) * Mathf.Cos(horizontalAngle1) * radius,
            Mathf.Cos(angle1) * radius,  // This is the correct Y for both top and bottom hemispheres
            Mathf.Sin(angle1) * Mathf.Sin(horizontalAngle1) * radius);

        Vector3 point2 = center + new Vector3(
            Mathf.Sin(angle1) * Mathf.Cos(horizontalAngle2) * radius,
            Mathf.Cos(angle1) * radius,
            Mathf.Sin(angle1) * Mathf.Sin(horizontalAngle2) * radius);

        Vector3 point3 = center + new Vector3(
            Mathf.Sin(angle2) * Mathf.Cos(horizontalAngle1) * radius,
            Mathf.Cos(angle2) * radius,
            Mathf.Sin(angle2) * Mathf.Sin(horizontalAngle1) * radius);

        // Draw the lines for the hemisphere
        Gizmos.DrawLine(point1, point2);
        Gizmos.DrawLine(point1, point3);
      }
    }
  }



  private void DrawCircle(Vector3 position, float radius)
  {
    Vector3 previousPoint = position + new Vector3(radius, 0, 0);
    for (int i = 1; i <= CircleQualityEditor_Draw_Only; i++)
    {
      float angle = (i * Mathf.PI * 2) / CircleQualityEditor_Draw_Only;
      Vector3 newPoint = position + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
      Gizmos.DrawLine(previousPoint, newPoint);
      previousPoint = newPoint;
    }
  }
#endif
}
