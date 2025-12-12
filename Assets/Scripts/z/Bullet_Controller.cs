using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;


public class Bullet_Controller : MonoBehaviour
{

    [SerializeField] internal Coin_controller coinController;
    [SerializeField] internal Transform target;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D col;
    [SerializeField] internal bool autoaim = false;
    internal Gun_Controller gunController;
    internal double damage;
    internal double coin;
    private void Start()
    {

    }

    private void OnEnable()
    {

    }

    private void Update()
    {
        if (target != null && autoaim)
        {
            Vector3 dir = (target.position - rb.transform.position).normalized;
        
            rb.velocity = dir * 25;
        }

        transform.up = -rb.velocity.normalized;
    }


    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (autoaim)
        {
            //Destroy(this.gameObject);
            //target = null;
            //autoaim = false;
            //Vector2 lastVelocity = rb.velocity;
            //Vector3 newDirection = Vector2.Reflect(lastVelocity, collision.contacts[0].normal);
            //print(newDirection);
            //rb.AddForce(newDirection.normalized);
        }
    }


    private void OnDestroy()
    {
        coinController.SetTotalBet(-coin);
        gunController.ClearABullet(this);
    }

    internal void maketrigger(bool toggle) {

        col.isTrigger = toggle;

    }

}
