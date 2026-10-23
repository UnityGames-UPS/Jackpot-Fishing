using System.Collections.Generic;
using UnityEngine;

// Single-flag mute model. Both focus sources (JS OnFocusChanged and Unity's OnApplicationFocus)
// call SetMuteAll; the sound button calls SetUserMute. Mute via the .mute flag only, never Pause().
internal class AudioController : MonoBehaviour
{
  [SerializeField] private List<AudioSource> sources = new();

  private bool userMuted;
  private bool isForceMuted;
  private bool preFocusUserMuted;

  // Focus-driven. Guarded so a duplicate call from the other focus source can't clobber the restore state.
  internal void SetMuteAll(bool forceMute)
  {
    if (forceMute == isForceMuted) return;
    isForceMuted = forceMute;

    if (forceMute)
    {
      preFocusUserMuted = userMuted;
      ApplyMute(true);
    }
    else
    {
      ApplyMute(preFocusUserMuted);
    }
  }

  // User-toggle-driven. An explicit tap proves the game has focus, so it clears any stale
  // forced mute and always wins immediately.
  internal void SetUserMute(bool mute)
  {
    userMuted = mute;
    isForceMuted = false;
    ApplyMute(mute);
  }

  private void ApplyMute(bool mute)
  {
    foreach (var source in sources)
    {
      if (source != null)
        source.mute = mute;
    }
  }

  private void OnApplicationFocus(bool focus)
  {
    SetMuteAll(!focus);
  }
}
