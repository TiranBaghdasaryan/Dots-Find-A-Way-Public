/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SIS
{
    /// <summary>
    /// Displaying purchase confirmation for finishing transactions required on certain stores, 
    /// e.g. on Xsolla or when using PayPal store. Confirming payments is a manual action so this
    /// script should be somewhere in your shop UI. Otherwise user rewards could be lost.
    /// </summary>
    public class UIWindowConfirmOrder : MonoBehaviour
    {
        /// <summary>
        /// Button to trigger transaction confirmation on the store.
        /// </summary>
        public GameObject confirmButton;

        /// <summary>
        /// Button to close the window (eventually without confirming transactions).
        /// </summary>
        public GameObject closeButton;

        /// <summary>
        /// Loading indicator for the user to see that something is going on.
        /// </summary>
        public Image loadingImage;


        //rotate the loading indicator for visual representation
        void Update()
        {
            loadingImage.rectTransform.Rotate(-Vector3.forward * 100 * Time.deltaTime);
        }


        #if SIS_IAP
        //start displaying the UI buttons after some time
        void OnEnable()
        {
            StartCoroutine(UpdateStatus());
        }


        //hide UI buttons for some time to actually give the user the chance for payment
        private IEnumerator UpdateStatus()
        {
			if (PayPalStore.instance == null &&
                PlayFabPayPalStore.instance == null &&
                XsollaStore.instance == null)
            {
                closeButton.SetActive(true);
                yield break;
            }
            
            yield return new WaitForSeconds(10);
            closeButton.SetActive(true);

            //Xsolla has an auto-refresh mechanic, so we do not need a confirm button there
            //only enabled on platforms that require a manual confirmation of the order
            #if !XSOLLA_IAP
            confirmButton.SetActive(true);
            #endif
        }


        /// <summary>
        /// Triggers transaction confirmation on the store.
        /// Usually assigned to a UI button in-game.
        /// </summary>
        public void ConfirmPurchase()
        {
            if (PayPalStore.instance != null) PayPalStore.instance.ConfirmPurchase();
            if (PlayFabStore.instance != null && PlayFabStore.instance is PlayFabPayPalStore)
               (PlayFabStore.instance as PlayFabPayPalStore).ConfirmPurchase();
            if (XsollaStore.instance != null) XsollaStore.instance.ConfirmPurchase();

            StartCoroutine(DelayConfirmation());
        }
        #endif


        //delay further confirm request within the timeout frame
        private IEnumerator DelayConfirmation()
        {
            confirmButton.SetActive(false);
            yield return new WaitForSeconds(10);
            confirmButton.SetActive(true);
        }


        public void Close()
        {
            #if SIS_IAP
            //trigger a last manual confirmation approach when closing the window,
            //just in case the automatic processing missed fulfilling the order
            if (XsollaStore.instance != null)
                XsollaStore.instance.ConfirmPurchase();
            #endif

            gameObject.SetActive(false);
        }


        //reset UI buttons to the original state
        void OnDisable()
        {
            confirmButton.SetActive(false);
            closeButton.SetActive(false);
        }
    }
}
