using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class Screen_controller : MonoBehaviour
{
    [SerializeField] private Camera[] camlist;
    [SerializeField] private RenderTexture[] texturelist;
    [SerializeField] private Button screen1_button;
    [SerializeField] private Button screen2_button;
    [SerializeField] private Button screen3_button;
    [SerializeField] private Button screen4_button;
    [SerializeField] private Canvas cnv;
    [SerializeField] private Gun_Controller gunController;
    //[SerializeField] private RawImage img;
    internal static int currentIndex=0;


    void Start()
    {

        screen1_button.onClick.AddListener(delegate { Toggle(0); });
        screen2_button.onClick.AddListener(delegate { Toggle(1); });
        screen3_button.onClick.AddListener(delegate { Toggle(2); });
        screen4_button.onClick.AddListener(delegate { Toggle(3); });

    }

    // Update is called once per frame
    void Update()
    {
        
    }



    void Toggle(int index) {

        print(index);

       
        for (int i = 0; i < camlist.Length; i++)
        {
            camlist[i].targetTexture = texturelist[i];
        }

        camlist[index].targetTexture = null;
        cnv.worldCamera = camlist[index];
        gunController.DestroyAllBullet();
    }
}
