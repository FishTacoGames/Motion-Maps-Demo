using UnityEngine;
namespace Grid_Parameters
{
  [System.Serializable]
  public struct MotionValues
  {   
    public Collider collider;
    public readonly bool UseRb => collider != null ? collider.attachedRigidbody : null != null;
    [Range(0.07f, 25f)] public float width;
    [Range(0.07f, 25f)] public float length;
    [Range(0.1f, 10f)] public float height;
    [Range(-5f, 5f)] public float offset;
    [Range(0.1f, 1f)] public float redStrength;

    public readonly Vector4 Vector { get { return collider ? new (redStrength, collider.bounds.size.y + offset, length, width) : new Vector4(redStrength, height, length, width); } }
  }
  [System.Serializable]
  public struct MotionValues2
  {
    public bool IgnoreLinkedRigidbody;
    public readonly bool UseRb => linkedCollider != null && linkedCollider.attachedRigidbody != null && !IgnoreLinkedRigidbody;
    public readonly bool UseCC => linkedCollider != null && linkedCollider.GetType() == typeof(CharacterController);
    [SerializeField] private ColliderAxis axis;
    [SerializeField] private Collider linkedCollider;
    [SerializeField] private Collider VelocitySource;
    [Range(0.07f, 25f), SerializeField]
    private float _x;
    [Range(0.07f, 25f), SerializeField]
    private float _z;
    [Range(0.1f, 10f), SerializeField]
    private float _y;
    [Range(0.1f, 1f), SerializeField]
    private float _strength;

    private Vector4 cachedVector;
    public readonly Vector4 Vector => cachedVector;
    public readonly Collider LinkedCollider => linkedCollider;
    public readonly ColliderAxis ColliderAxis => UseCC ? ColliderAxis.Y : linkedCollider is CapsuleCollider cap ? (ColliderAxis)cap.direction : axis;
    public readonly Vector3 Velocity => UseRb ? linkedCollider.attachedRigidbody.linearVelocity : linkedCollider is CharacterController cc ? cc.velocity : VelocitySource && VelocitySource.attachedRigidbody ? VelocitySource.attachedRigidbody.linearVelocity : VelocitySource is CharacterController mainCC ? mainCC.velocity : Vector3.zero; // if we get to 0 the user hasnt set any fields, we should notify them before this.

    /// <summary>
    /// Call to update sizes, lerping is allowed
    /// Does not automatically update at runtime, only at start
    /// </summary>
    public void UpdateVector()
    {

      if (linkedCollider.GetType() == typeof(MeshCollider))
      {
        Debug.LogWarning("Mesh Colliders are not supported, will use inspector values");
      };
      CapsuleCollider capsule = linkedCollider as CapsuleCollider;
      CharacterController cc = linkedCollider as CharacterController;
      BoxCollider box = linkedCollider as BoxCollider;
      float radius = capsule != null ? capsule.radius * 2 : _x;
      bool isCC = linkedCollider != null && cc != null;
      bool isCapsule = linkedCollider != null && capsule != null;
      bool isBox = linkedCollider != null && box != null;
      if (isCC) radius = cc.radius * 2;
      Vector4 axisVector = new(isBox ? box.size.y : isCapsule ? capsule.height : _y, isBox ? box.size.z : isCapsule ? radius : _z, isBox ? box.size.x : isCapsule ? radius : _x, _strength);
      switch (ColliderAxis)
      {
        case ColliderAxis.X:
          break;
        case ColliderAxis.Y://cc is always y
          axisVector = new Vector4(isBox ? box.size.x : isCapsule ? radius : isCC ? radius : _x, isBox ? box.size.y : isCapsule ? capsule.height : isCC ? cc.height : _y, isBox ? box.size.z : isCapsule ? radius : isCC ? radius: _z, _strength);
          break;
        case ColliderAxis.Z:
          axisVector = new Vector4(isBox ? box.size.z : isCapsule ? radius : _z, isBox ? box.size.x : isCapsule ? radius : _x, isBox ? box.size.y : isCapsule ? capsule.height : _y, _strength);
          break;
      }
      cachedVector = axisVector;
    }
  }
}