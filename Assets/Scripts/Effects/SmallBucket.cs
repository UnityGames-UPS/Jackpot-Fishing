  using UnityEngine;
  using UnityEngine.UI;
  
  [RequireComponent(typeof(ImageAnimation))]
  [RequireComponent(typeof(RectTransform))]
  internal class SmallBucket : MonoBehaviour
  {
    [SerializeField] internal RectTransform rect;
    [SerializeField] internal ImageAnimation anim;
    internal Vector3 baseScale;
    private void OnValidate() {
      rect = this.transform as RectTransform;
      anim = GetComponent<ImageAnimation>();
    }
  }
