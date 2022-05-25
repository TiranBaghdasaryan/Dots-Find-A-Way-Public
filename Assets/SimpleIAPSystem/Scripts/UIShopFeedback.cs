/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SIS
{
    /// <summary>
    /// Presents UI feedback to the user for various purchase states, e.g. failed or successful purchases.
    /// Also handles showing a transaction confirmation popup where necessary, e.g. for PayPal transactions.
    /// </summary>
    public class UIShopFeedback : MonoBehaviour
    {
        //static reference to this script.
        private static UIShopFeedback instance;

        /// <summary>
        /// Main window for showing feedback on purchase events to the user.
        /// </summary>
        public GameObject errorWindow;

        /// <summary>
        /// Confirmation window for refreshing transactions. Only required when using third party services, e.g. PayPal and Xsolla.
        /// </summary>
        public GameObject confirmWindow;

        /// <summary>
        /// 
        /// </summary>
        public UIWindowShowReward previewWindow;

        /// <summary>
        /// Text component of the errorWindow gameobject.
        /// </summary>
        public Text messageLabel; 


        /// <summary>
        /// Returns a static reference to this script.
        /// </summary>
        public static UIShopFeedback GetInstance()
        {
            return instance;
        }


        void Awake()
        {
            instance = this;

            //check for IAPManager existence before other containers initialize
            if (!IAPManager.GetInstance())
            {
                //double check if there is an IAPManager in the scene that just isn't ready yet
                IAPManager manager = (IAPManager)FindObjectOfType(typeof(IAPManager));
                if (manager != null) return;

                //there really is no IAPManager in the scene, this should be avoided
                Debug.LogWarning("UIShopFeedback: Could not find IAPManager prefab. Have you placed it in the first scene of your app and started from there? Instantiating copy...");
                GameObject obj = Instantiate(Resources.Load("IAPManager", typeof(GameObject))) as GameObject;
                //remove clone tag from its name. Not necessary, but nice to have
                obj.name = obj.name.Replace("(Clone)", "");
            }
        }


        /// <summary>
        /// Show feedback/error window with text received.
        /// This gets called in IAPListener's HandleSuccessfulPurchase method with some custom text,
        /// or from the IAPManager with the error message when a purchase failed at billing.
        /// <summary>
        public static void ShowMessage(string text)
        {
            if (!instance.errorWindow) return;

            if (instance.messageLabel) instance.messageLabel.text = text;
            instance.errorWindow.SetActive(true);
        }


        /// <summary>
        /// Shows window waiting for transaction confirmation. This gets called by the store
        /// when waiting for the user to confirm his purchase payment with the third party service.
        /// </summary>
        public static void ShowConfirmation()
        {
            if (!instance.confirmWindow) return;

            instance.confirmWindow.SetActive(true);
        }


        /// <summary>
        /// 
        /// </summary>
        public static void ShowRewardPreview(List<KeyValuePairStringInt> items)
        {
            if (!instance.previewWindow) return;

            instance.previewWindow.SetItems(items);
            instance.previewWindow.gameObject.SetActive(true);
        }
    }
}
