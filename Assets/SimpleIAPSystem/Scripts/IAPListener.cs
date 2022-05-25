/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using UnityEngine;

namespace SIS
{
    /// <summary>
    /// Script that listens to purchases and other IAP events:
    /// here we tell our app what to do when these events happen.
    /// <summary>
    public class IAPListener : MonoBehaviour
    {
        //subscribe to the most important IAP events
        void Start()
        {
            IAPManager.purchaseSucceededEvent += HandleSuccessfulPurchase;
            IAPManager.purchaseFailedEvent += HandleFailedPurchase;
            IAPManager.consumeSucceededEvent += HandleSuccessfulConsume;
            IAPManager.consumeFailedEvent += HandleFailedConsume;
            DBManager.itemSelectedEvent += HandleSelectedItem;
            DBManager.itemDeselectedEvent += HandleDeselectedItem;
        }


        /// <summary>
        /// Handle the completion of purchases, be it for products or virtual currency.
        /// Most of the IAP logic is handled internally already, such as adding products or currency to the inventory.
        /// However, this is the spot for you to implement your custom game logic for instantiating in-game products etc.
        /// </summary>
        public void HandleSuccessfulPurchase(string productID)
        {
            if (IAPManager.isDebug) Debug.Log("IAPListener reports: HandleSuccessfulPurchase: " + productID);

            //differ between ids set in the IAP Settings editor
            switch (productID)
            {
                //section for in app purchases
                case "coins_small":
                    //the user bought the item "coins", show appropriate feedback
                    ShowMessage("1000 coins were added to your balance!");
                    break;
                case "coin_pack":
                    ShowMessage("2500 coins were added to your balance!");
                    break;
                case "big_coin_pack":
                    ShowMessage("6000 coins were added to your balance!");
                    break;
                case "huge_coin_pack":
                    ShowMessage("12000 coins were added to your balance!");
                    break;
                case "no_ads":
                    //no_ads purchased. You can now check DBManager.isPurchased("no_ads")
                    //before showing ads and block them
                    ShowMessage("Ads disabled!");
                    break;
                case "abo_monthly":
                    //same here - your code to unlock subscription content
                    ShowMessage("Added subscription!");
                    break;
                case "bundle":
                    ShowMessage("Thank you for buying the bundle!");
                    break;

                //section for in game content
                case "bullets":
                    //if you define a usage count in the Project Settings, then the amount
                    //has been added to your inventory already. No need to call something like
                    //DBManager.AddPlayerData("bullets", new SimpleJSON.JSONData(100)) manually
                    ShowMessage("Bullets were added to your inventory!");
                    break;
                case "health":
                    //this product does not have a usage count and therefore is consumed instantly
                    ShowMessage("Medikits were consumed instantly!");
                    break;
                case "energy":
                    /*
                    //for example, in case product was bought not in the shop,
                    //but during the game to receive a second chance
                    if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MyGameScene")
                    {
                        //try to consume product immediately
                        IAPManager.Consume("energy", 1);
                    }
                    else
                    */
                    ShowMessage("Energy was added to your inventory!");
                    break;
                case "speed":
                    ShowMessage("Speed boost unlocked!");
                    break;
                case "speed_1":
                case "speed_2":
                case "speed_3":
                    ShowMessage("Speed boost upgraded!");
                    break;
                case "bonus":
                    ShowMessage("Bonus level unlocked!");
                    break;

                case "uzi":
                    ShowMessage("Uzi unlocked!");
                    break;
                case "ak47":
                    ShowMessage("AK47 unlocked!");
                    break;
                case "m4":
                    ShowMessage("M4 unlocked!");
                    break;

                case "hat":
                    ShowMessage("Hat unlocked!");
                    break;
                case "backpack":
                    ShowMessage("Backpack unlocked!");
                    break;
                case "belt":
                    ShowMessage("Ammo belt unlocked!");
                    break;
                case "jetpack":
                    ShowMessage("Jetpack unlocked!");
                    break;
                case "booster":
                    ShowMessage("Double XP unlocked!");
                    break;

                default:
                    ShowMessage("Product (" + productID + ") bought!");
                    break;
            }
        }


        /// <summary>
        /// Handle the completion of consumes, be it for consumable or non-consumable products.
        /// This is the spot for you to implement your custom game logic for activating in-game products etc.
        /// </summary>
        public void HandleSuccessfulConsume(string productID)
        {
            IAPProduct product = IAPManager.GetIAPProduct(productID);
            if(product != null)
                ShowMessage(product.title + " consumed!");

            //section for in game content
            switch(productID)
            { 
                case "bullets":
                    break;
                case "health":
                    //for example, add health to your custom player class
                    //Player.GetInstance().AddHealth(75);
                    break;
                case "energy":
                    break;
            }
        }


        //just shows a message via our ShopManager component,
        //but checks for an instance of it first
        void ShowMessage(string text)
        {
            if (UIShopFeedback.GetInstance())
                UIShopFeedback.ShowMessage(text);
        }

        //called when an purchaseFailedEvent happens,
        //we do the same here
        void HandleFailedPurchase(string error)
        {
            if (UIShopFeedback.GetInstance())
                UIShopFeedback.ShowMessage(error);
        }


        //called when an consumeFailedEvent happens,
        //we do the same here
        void HandleFailedConsume(string error)
        {
            if (UIShopFeedback.GetInstance())
                UIShopFeedback.ShowMessage(error);
        }


        //called when a purchased shop item gets selected
        void HandleSelectedItem(string productID)
        {
            if (IAPManager.isDebug) Debug.Log("Selected: " + productID);
        }


        //called when a selected shop item gets deselected
        void HandleDeselectedItem(string productID)
        {
            if (IAPManager.isDebug) Debug.Log("Deselected: " + productID);
        }
    }
}