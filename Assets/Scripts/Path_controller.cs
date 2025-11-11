using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;



public class Path_controller : MonoBehaviour
{


    Tween pathTween;
    internal float speed = 150f;
    internal Vector3 target;
    void Start()
    {

        StartCoroutine(MoveRoutineV());
    }

    // Update is called once per frame
    void Update()
    {

    }

    IEnumerator MoveRoutineV()
    {
        yield return new WaitForSeconds(1);

        pathTween=transform.DOLocalMove(target, speed).SetSpeedBased(true);

        yield return new WaitForSeconds(Random.Range(2f,3f));
        pathTween.timeScale = Random.Range(1, 3);


    }



}
