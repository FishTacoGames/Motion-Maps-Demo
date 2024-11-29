namespace Grid_Parameters
{
  public enum ExpansionMode
  {
    Center,
    PositiveXZCorner,
    NegativeXZCorner,
    PositiveZXCorner,
    NegativeZXCorner
  }
  public enum ExpansionLevel
  {
    Top,
    Center,
    Bottom
  }
  public enum CellSize
  {
    _128 = 128,
    _256 = 256,
    _512 = 512,
    _1024 = 1024,
    _2048 = 2048,
  }
  public enum ColliderAxis
  {
    X,
    Y,
    Z
  }
  public enum LinkShape
  {
    Box,
    Cylinder,
    Ellipsoid,
    Capsule
  }
}
