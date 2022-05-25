/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using UnityEngine;
using UnityEngine.UI;

namespace SIS
{
    /// <summary>
    /// Simple script to handle coupon redemption on platforms supporting it.
    /// </summary>
    public class UIButtonCoupon : MonoBehaviour
    {
        //only show the button on platforms supporting it
        void Start()
        {
            bool supportedPlatform = false;

            #if XSOLLA_IAP || PLAYFAB
	        supportedPlatform = true;
            #endif

            gameObject.SetActive(supportedPlatform);
        }


        /// <summary>
        /// Calls the RedeemCoupon method on a corresponding service.
        /// It makes sense to add this to an UI button event.
        /// </summary>
        public void Redeem(InputField inputField)
        {
            #if XSOLLA_IAP
            XsollaManager.RedeemCoupon(inputField.text);
            #endif

            #if PLAYFAB
            PlayFabManager.RedeemCoupon(inputField.text);
            #endif
        }
    }
}
