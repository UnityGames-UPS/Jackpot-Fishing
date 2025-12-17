using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HitMarkerPool : GenericObjectPool<HitMarker>
{
  public static HitMarkerPool Instance;
  internal override void Awake()
  {
    base.Awake();
    Instance = this;
  }
}
