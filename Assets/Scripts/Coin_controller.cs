using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Coin_controller : MonoBehaviour
{
    [SerializeField] private double total_amount;
    [SerializeField] private TMP_Text total_coin;
    [SerializeField] private double total_bet = 0;
    [SerializeField] private TMP_Text total_bet_text;
    void Start()
    {
        SetTotalCoin(100);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    internal void SetTotalCoin(double amount) {

        total_amount += amount;
        total_coin.text = total_amount.ToString("0.00");

    }

    internal void SetTotalBet(double amount) {
        total_bet += amount;
        total_bet_text.text = total_bet.ToString("0.00");

    }

    internal double GetTotalCoin() {


        return total_amount;
    }
}
