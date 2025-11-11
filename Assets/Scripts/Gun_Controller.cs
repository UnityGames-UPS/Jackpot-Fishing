using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class Gun_Controller : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private GameObject bullet;
    [SerializeField] private GameObject gun;
    [SerializeField] private GameObject bullet_container;
    [SerializeField] private GameObject direction;
    [SerializeField] private BoxCollider2D[] bounds;
    [SerializeField] private Coin_controller coinController;
    [SerializeField] private double curentBulletDamage = 1;
    [SerializeField] private double currentBulletCoin =1;
    [SerializeField] private List<Bullet_Controller> allbullets = new List<Bullet_Controller>();
    [SerializeField] private Fish_controller fishController;
    [SerializeField] private Transform muzzel;
    [SerializeField] private Button[] gunList;
    [SerializeField] private Button auto_aim_button;

    [SerializeField] internal Transform autoAimQueue = null;
    [SerializeField] public Bullet_Controller bulletAutoAim =null;
    internal  bool auto_aim;
    private bool isfiring = false;
    private Coroutine autoShoot;
    private Coroutine shoot;


    void Start()
    {

        if(auto_aim_button) auto_aim_button.onClick.AddListener(ToggleAutoAim);
        if(gunList[0]) gunList[0].onClick.AddListener(delegate { ChangeGun(1, 1, 0); });
        if(gunList[1]) gunList[1].onClick.AddListener(delegate { ChangeGun(2, 2, 1); });
        if(gunList[2]) gunList[2].onClick.AddListener(delegate { ChangeGun(3, 3, 2); });
        if(gunList[3]) gunList[3].onClick.AddListener(delegate { ChangeGun(4, 4, 3); });
        SetBounds();
    }

    // Update is called once per frame
    void Update()
    {
       
        changedirection();

    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isfiring && !auto_aim && coinController.GetTotalCoin()> currentBulletCoin)
        {
            Shoot();
        }
    }

    void ToggleAutoAim() {
        auto_aim = !auto_aim;

        if (auto_aim) auto_aim_button.image.color = Color.gray;
        if (!auto_aim) auto_aim_button.image.color = Color.white;
    }

    void SetBounds()
    {
        bounds[0].offset = new Vector2(Screen.width / 2, 0);
        bounds[0].size = new Vector2(1, Screen.height);

        bounds[1].offset = new Vector2(-Screen.width / 2, 0);
        bounds[1].size = new Vector2(1, Screen.height);

        bounds[2].offset = new Vector2(0, Screen.height / 2);
        bounds[2].size = new Vector2(Screen.width, 1);

        bounds[3].offset = new Vector2(0, -Screen.height / 2);
        bounds[3].size = new Vector2(Screen.width, 1);

    }

    float changedirection()
    {

        Vector3 mouseScreenPosition = Input.mousePosition;
        mouseScreenPosition.z = gun.transform.position.z;
        Vector3 mouseWorldSpace = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
        Vector3 direction = mouseWorldSpace - gun.transform.position;
        if (autoAimQueue != null && auto_aim)
        {
            direction = autoAimQueue.position - gun.transform.position;

        }
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90;
        if (angle > 180) angle -= 360;
        if (angle <= -180) angle += 360;
        float modifiedAngle = Mathf.Clamp(angle, -90, 90);
        gun.transform.eulerAngles = new Vector3(0, 0, modifiedAngle);
        return modifiedAngle;

    }

    void ChangeGun(double coin, double damage, int index)
    {


        for (int i = 0; i < gunList.Length; i++)
        {
            if (i == index)
            {
                gunList[i].interactable = false;
                continue;
            }
            gunList[i].interactable = true;

        }
        curentBulletDamage = damage;
        currentBulletCoin = coin;
        gun.GetComponent<Image>().color = gunList[index].image.color;
        gun.transform.localEulerAngles = Vector3.zero;

    }


    void Shoot(bool autoaim = false)
    {
        if (!auto_aim && autoShoot != null) StopCoroutine(autoShoot);
        if(!isfiring)
        shoot=StartCoroutine(Shootroutine(autoaim));
    }

    IEnumerator Shootroutine(bool autoaim=false) {

        isfiring = true;

        GameObject bulletIns = Instantiate(bullet, muzzel) ;
 
        bulletIns.transform.localPosition = Vector2.zero;
        coinController.SetTotalCoin(-currentBulletCoin);
        coinController.SetTotalBet(currentBulletCoin);

        Bullet_Controller temp = bulletIns.GetComponent<Bullet_Controller>();
        if (autoaim)
            bulletAutoAim = temp;

        allbullets.Add(temp);
        temp.gunController = this;
        temp.damage = curentBulletDamage;
        temp.coin = currentBulletCoin;
        temp.coinController = coinController;
        Vector2 dir = direction.transform.position - gun.transform.position;
        bulletIns.transform.localRotation = gun.transform.rotation;
        bulletIns.transform.SetParent(bullet_container.transform);
        if (autoAimQueue != null && auto_aim)
        {
            temp.target = autoAimQueue;
            temp.autoaim = true;
            temp.maketrigger(true);

        }
        else
            bulletIns.GetComponent<Rigidbody2D>().AddForce(dir.normalized*1.8f, ForceMode2D.Impulse);
        yield return new WaitForSeconds(0.1f);
        isfiring = false;


    }

    internal void Autoshoot(double health)
    {
        if (autoShoot != null) StopCoroutine(autoShoot);
        //if (shoot!=null) StopCoroutine(shoot);
        //auto_aim = true;
        if(coinController.GetTotalCoin() > currentBulletCoin)
        autoShoot = StartCoroutine(Autofire(health));
    }

    IEnumerator Autofire(double health)
    {

       while (autoAimQueue != null && auto_aim )
        {
            if (!bulletAutoAim)
            {
                Shoot(auto_aim);
                yield return new WaitForSeconds(0.4f); ;
            }
            else {
                yield return null;
            }
            //int bulletNos = (int)(health / curentBulletDamage);
            //for (int i = 0; i < bulletNos; i++)
            //{

            //}

        }

    }

    internal void ClearABullet(Bullet_Controller bullet) {

        allbullets.Remove(bullet);
    }


    internal void DestroyAllBullet()
    {

        allbullets.Clear();

    }



}
