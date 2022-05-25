/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using UnityEngine;
using UnityEngine.SceneManagement;

namespace SIS
{
    /// <summary>
    /// Simple script that contains methods for testing purposes.
    /// You shouldn't implement this script in production versions.
    /// <summary>
    public class DebugCalls : MonoBehaviour
    {
        void Start()
        {
            //switch over to the virtual reality supported scene if VR mode is enabled
            if (SceneManager.GetActiveScene().name == "SceneSelection" && UnityEngine.XR.XRSettings.enabled)
                SceneManager.LoadScene("VerticalVR");
        }


        /// <summary>
        /// Allows initializing the IAPManager at some point later manually.
        /// </summary>
        public void Initialize()
        {
            if (IAPManager.GetInstance())
            {
                IAPManager.GetInstance().Initialize();
            }
        }


        /// <summary>
        /// Deletes all data saved in prefs, for ensuring a clean test state.
        /// <summary>
        public void Reset()
        {
            if (DBManager.GetInstance())
            {
                #if SIS_IAP
                    UnityEngine.Purchasing.UnityPurchasing.ClearTransactionLog();
                #endif

                DBManager.ClearAll();
                DBManager.GetInstance().Init();
            }
        }


        /// <summary>
        /// Increases player level by 1 which unlocks new shop items.
        /// <summary>
        public void LevelUp()
        {
            if (DBManager.GetInstance())
            {
                int level = DBManager.AddPlayerData("level", 1);

                IAPManager.GetInstance().RefreshShopItemAll();

                if (UIShopFeedback.GetInstance())
                {
                    UIShopFeedback.ShowMessage("Leveled up to level: " + level + "! Tried to unlock new items.");
                }
            }
        }


        /// <summary>
        /// Consumes product purchase by 1.
        /// </summary>
        public void ConsumeItem(string productID)
        {
            IAPManager.Consume(productID);
        }
    }
}
