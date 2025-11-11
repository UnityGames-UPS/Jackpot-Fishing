using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

public class Fish_view : MonoBehaviour
{
    [SerializeField] private double maxHealth = 50;
    [SerializeField] internal double currentHealth;
    [SerializeField] private Image health;
    [SerializeField] private double coin;
    [SerializeField] private Collider2D collder2d;
    [SerializeField] internal Transform selfTransform;
    [SerializeField] private Image self;
    [SerializeField] int counter = 0;
    [SerializeField] private TMP_Text coin_text;
    bool colliderOn;
    internal FishSchool fishSchool;
    internal Fish_controller fish_Controller;
    internal int screenIndex;
    internal Coin_controller coinController;
    internal bool isAutoAimTarget=false;
    internal bool isInfoOn=false;
    Tweener tween1;
    
    void Start()
    {


        selfTransform = transform;
        coin = Random.Range(1, 10);
        coin_text.text = coin.ToString("0.00");
        currentHealth = maxHealth;
        tween1=transform.DOScale(new Vector3(1.1f, 1.1f, 1.1f), 0.5f).SetLoops(-1, LoopType.Yoyo);
    }

    private void Update()
    {


    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("bullet"))
        {
            Bullet_Controller bullet = collision.GetComponent<Bullet_Controller>();
            if (bullet.autoaim)
            {
                if (isAutoAimTarget)
                {
                    OnHit(collision,bullet.damage);

                }


            }else{

                OnHit(collision, bullet.damage);
            }

        }

        if (collision.CompareTag("wall"))
        {

            counter++;
            if (counter % 2 == 0)
            {
                self.color = Color.white;
                if (fish_Controller.GetAutoAim() == selfTransform)
                {


                    fish_Controller.ClearAutoAim();
                }
            }
        }


    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("wall"))
        {

            isAutoAimTarget = false;
        }

    }

    private void OnDestroy()
    {
        coinController.SetTotalCoin(coin);
        fishSchool.fishList.Remove(this);
        tween1.Kill();
    }

    internal void ToggleCollider()
    {




    }

    private void OnMouseDown()
    {
        if (!fish_Controller.CheckAutoAimOn())
            return;

        isAutoAimTarget = !isAutoAimTarget;

        if (isAutoAimTarget)
        {
            self.color = Color.red;
            fish_Controller.AddToAutoAim(this);
        }
        else
        {

            self.color = Color.white;
            fish_Controller.ClearAutoAim();
        }

    }

    private void OnMouseEnter()
    {
        ShowInfoOn();
    }

    private void OnMouseExit()
    {

        ShowInfoOff();

    }

    internal void ShowInfoOn() {
        if (isInfoOn)
            return;
        coin_text.gameObject.SetActive(true);
        health.transform.parent.gameObject.SetActive(true);

    }

    internal void ShowInfoOff() {
        if (isInfoOn)
            return;
        coin_text.gameObject.SetActive(false);
        health.transform.parent.gameObject.SetActive(false);

    }

    void OnHit(Collider2D collision, double damage) {

        if (damage <= 0)
        {

            return;
        }
        ShowInfoOn();
        currentHealth = currentHealth - damage;
        tween1.Pause();
        transform.DOScale(new Vector3(0.9f, 0.9f, 0.9f), 0.15f);
        tween1.Play();
        if (currentHealth <= 0)
        {

            Destroy(gameObject);
        }
        else
            health.fillAmount = (float)(currentHealth / maxHealth);

        Invoke("ShowInfoOff",0.15f);

        Destroy(collision.gameObject);

    }


}

