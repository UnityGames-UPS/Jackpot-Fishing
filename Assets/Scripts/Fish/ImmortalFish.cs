using System.Collections;
using UnityEngine;

internal class ImmortalFish : BaseFish
{
  private Coroutine speedPulseRoutine;
  internal override void Initialize(FishData data)
  {
    base.Initialize(data);
    if (data.variant == "immo_squid_fish")
    {
      speedPulseRoutine = StartCoroutine(OctopusSpeedPulse());
      SetSpeedMultiplier(1.6f);
    }
  }

private IEnumerator OctopusSpeedPulse()
{
  while (true)
  {
    yield return new WaitForSecondsRealtime(0.8f);
    SetSpeedMultiplier(0.6f);   // glide

    yield return new WaitForSecondsRealtime(0.7f);
    SetSpeedMultiplier(1.8f);   // push
  }
}


  internal override void ResetFish()
  {
    base.ResetFish();
    if(speedPulseRoutine != null)
    {
      StopCoroutine(speedPulseRoutine);
    }
  }
}

