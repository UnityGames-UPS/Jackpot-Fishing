using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class BlastAnimationPool : GenericObjectPool<ImageAnimation>
{
  internal static BlastAnimationPool Instance;

  internal override void Start()
  {
    base.Start();
    Instance = this; 
  }
}
