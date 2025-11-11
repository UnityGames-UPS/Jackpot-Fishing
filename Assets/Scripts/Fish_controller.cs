using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class Fish_controller : MonoBehaviour
{
    [SerializeField]private RectTransform SpawnArea;
    [SerializeField] private List<Transform> spawnPointList = new List<Transform>();
    [SerializeField] private GameObject[] fish;
    [SerializeField] private int FishCount;
    [SerializeField] internal List<FishSchool> allfish = new List<FishSchool>();
    [SerializeField] private List<Vector3> allpos;
    [SerializeField] private Coin_controller coinController;
    [SerializeField] private GameObject batchPrefab;
    [SerializeField] private Gun_Controller gunController;
    [SerializeField] private GameObject[] BigfishPool;
    [SerializeField] private Button btn;
    [SerializeField] private Transform[] patternList;
    [SerializeField] private Button info_button;
    internal static bool start;
    private bool showinfo = false;

    void Start()
    {

        //Spawnfish(6);
        //Spawnfish(3);
        Spawnfish(1,fish[1],5);
        //Spawnfish(5);
        //Spawnfish(15);
        Spawnfish(9,fish[0],4);


        if (info_button) info_button.onClick.AddListener(ToggleFishInfo);
    }


    // Update is called once per frame
    void Update()
    {

    }



    void Spawnfish(int indexStrat,GameObject fish,int fishCount=1)
    {
        //int indexStrat = Random.Range(0, allpos.Count);
        
        List<int> availablePosIndex = GetAvailablePosIndex(indexStrat);

        print(availablePosIndex.Count);
        int indexEnd = availablePosIndex[Random.Range(0, availablePosIndex.Count)];

        Vector3 target = spawnPointList[indexEnd].transform.localPosition;

        List<Transform> tempList = new List<Transform>();
        //GameObject fishSchool = Instantiate(batchPrefab, spawnPointList[indexStrat].transform);
        //fishSchool.transform.localPosition = Vector3.zero;
        //fishSchool.transform.localScale = Vector3.one;
        //fishSchool.transform.localEulerAngles = Vector3.zero;

        //fishSchool.GetComponent<Path_controller>().target = target;
        //Vector3 dir = target - fishSchool.transform.localPosition;
        //fishSchool.transform.right = dir;
        FishSchool batch = new FishSchool();
        List<Vector3> fisPositions = pattern(4);


        //foreach (var item in fisPositions)
        //{
        //    GameObject fishtemp = Instantiate(fish, fishSchool.transform);
        //    fishtemp.transform.localPosition = item;
        //    fishtemp.transform.right = fishSchool.transform.right;

        //    Fish_view temp = fishtemp.GetComponent<Fish_view>();
        //    tempList.Add(fishtemp.transform);
        //    batch.fishList.Add(temp);
        //    temp.fishSchool = batch;
        //    temp.coinController = coinController;
        //    temp.fish_Controller = this;
        //}

        Transform patternChoice = patternList[Random.Range(0, patternList.Length)];

        foreach (Transform child in patternList[Random.Range(0, patternList.Length)])
        {
            if (fishCount < 0)
                break;

            GameObject fishtemp = Instantiate(fish, spawnPointList[indexStrat].transform);
            fishtemp.transform.localPosition = child.localPosition;
            Vector3 dir = target - fishtemp.transform.localPosition;
            fishtemp.transform.right = dir;

            Fish_view temp = fishtemp.GetComponent<Fish_view>();
            tempList.Add(fishtemp.transform);
            batch.fishList.Add(temp);
            temp.fishSchool = batch;
            temp.coinController = coinController;
            temp.fish_Controller = this;
            temp.GetComponent<Path_controller>().target = target;
            fishCount--;

        }



        //for (int i = 0; i < tempList.Count; i++)
        //{

        //    tempList[i].transform.SetParent(fishSchool.transform, true);
        //}

        for (int i = 0; i < tempList.Count; i++)
        {

            tempList[i].transform.SetParent(SpawnArea.transform, true);
        }


        //fishSchool.transform.right = dir.normalized;

        //fishSchool.transform.SetParent(SpawnArea,true);
        tempList.Clear();

        allfish.Add(batch);
    }

    void ToggleFishInfo() {

        showinfo = !showinfo;
        if (showinfo)
        {
            foreach (var item in allfish)
            {
                item.ShowAllFishInfo();
            }
        }
        else {

            foreach (var item in allfish)
            {
                item.HideAllFishInfo();
            }
        }



    }


    internal void AddToAutoAim(Fish_view fish) {
        ClearAutoAim();
        if (gunController.auto_aim && fish) {

                    
                gunController.autoAimQueue = fish.transform;
                gunController.Autoshoot(fish.currentHealth);
        }
    }

    internal Transform GetAutoAim() {

        return gunController.autoAimQueue;
    }

    internal bool CheckAutoAimOn() {

        return gunController.auto_aim;
    }

    internal void ClearAutoAim() {

        if (gunController.autoAimQueue != null)
        {

            gunController.autoAimQueue.GetComponent<Image>().color = Color.white;
            gunController.autoAimQueue = null;
        }


    }

    List<Vector3> pattern(int count)
    {
        Vector3 spawnPosition = Vector3.zero;

        List<Vector3> poslist = new List<Vector3>();

        for (int i = 0; i < count; i++)
        {
            //if (i % 2 == 0)
            //{
            //    spawnPosition.x = i * 0.5f * 100;
            //    spawnPosition.y = i * 0.5f * 100;
            //}
            //else
            //{
            //    spawnPosition.x = -(i + 1) * 0.5f * 100;
            //    spawnPosition.y = (i - 1) * 0.5f * 100;

            //}
            spawnPosition.x = i * 0.5f * 400;
            //spawnPosition.x = i * Random.Range(0,3)* 80;
            //spawnPosition.y = i * Random.Range(0, 3) * 80;

            poslist.Add(spawnPosition);
        }
        return poslist;
    }

    List<int> GetAvailablePosIndex(int start) {

        List<int> availablePosIndex = new List<int>();

        int inc = 0;
        int max = spawnPointList.Count;
        if (start >= 0 && start <= 3)
        {
            inc = 13-1;
            max = 15+1;
        }
        else if (start >= 3 && start <= 11) {
            inc = 16-1;
            max = 22+1;
        }
        else if (start >= 11 && start <= 16)
        {
            inc = 0;
            max = 3+1;

        }else if (start >= 16 && start <= 23)
        {
            inc = 4;
            max = 12;

        }

        availablePosIndex.Remove(start);
        for (int i = inc; i < max; i++)
        {
            //availablePosIndex.Remove(i);
            availablePosIndex.Add(i);
        }


        return availablePosIndex;
    }
}

[System.Serializable]
public class FishSchool
{

    internal List<Fish_view> fishList = new List<Fish_view>();
    internal void ShowAllFishInfo() {

        foreach (var item in fishList)
        {
            item.ShowInfoOn();
            item.isInfoOn = true;
        }

    }

    internal void HideAllFishInfo()
    {

        foreach (var item in fishList)
        {
            item.isInfoOn = false;
            item.ShowInfoOff();
        }

    }

}