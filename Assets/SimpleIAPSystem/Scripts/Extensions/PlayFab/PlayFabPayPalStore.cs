/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

#if SIS_IAP
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
#endif

#if PLAYFAB_PAYPAL
using PlayFab;
using PlayFab.ClientModels;
#endif

namespace SIS
{
    #if SIS_IAP && !PLAYFAB_PAYPAL
    public class PlayFabPayPalStore : PlayFabStore
    { 
        public void ConfirmPurchase() { }
    }
    #endif

    #if SIS_IAP && PLAYFAB_PAYPAL
    /// <summary>
    /// Store implementation for PayPal, based on the PlayFabStore class.
    /// </summary>
    public class PlayFabPayPalStore : PlayFabStore
    {       
        /// <summary>
        /// Setting this store reference on initialization.
        /// </summary>
        public PlayFabPayPalStore()
        {
            instance = this;
        }


        /// <summary>
        /// Overriding the initialization with setting the correct store.
        /// </summary>
        public override void Initialize(IStoreCallback callback)
        {
            storeId = "PayPal";
            this.callback = callback;
        }


        /// <summary>
        /// Overriding the product retrieval process to allow for validation-only behavior.
        /// Even though validation is first happening on purchase, we still need to this as
        /// otherwise Unity IAP would not initialize correctly without any products.
        /// </summary>
        public override void RetrieveProducts(ReadOnlyCollection<ProductDefinition> products)
        {
            if (PlayFabManager.IsValidationOnly())
            {
                this.products = new Dictionary<string, ProductDescription>();

                foreach (IAPProduct product in IAPManager.GetInstance().asset.productList)
                {
                    this.products.Add(product.ID, new ProductDescription(product.ID, new ProductMetadata("", product.title, product.description, "USD", 0)));
                }

                //skip all catalog calls to PlayFab
                if (callback != null)
                    callback.OnProductsRetrieved(this.products.Values.ToList());
            }
            else
            {
                //do the regular calls for getting the catalog etc. too
                base.RetrieveProducts(products);
            }
        }


        /// <summary>
        /// Overriding the purchase behavior to allow for validation-only behavior (products outside the app).
        /// </summary>
        public override void Purchase(ProductDefinition product, string developerPayload)
        {
            //make sure to do the login on the purchase, at the latest
            if(PlayFabManager.IsValidationOnly())
            {
                if (string.IsNullOrEmpty(PlayFabManager.userId))
                {
                    PlayFabManager.GetInstance().LoginWithDevice((result) =>
                    { 
                        //login failed, raise error
                        if (result == false)
                        {
                            OnPurchaseFailed(null);
                            return;
                        }

                        //we're logged in with PlayFab now
                        base.Purchase(product, developerPayload);
                    });

                    return;
                }
            }

            //logged in already
            base.Purchase(product, developerPayload);
        }


        /// <summary>
        /// Overriding the payment request for opening the PayPal website in the browser.
        /// </summary>
        public override void OnPurchaseResult(PayForPurchaseResult result)
        {
            if (UIShopFeedback.GetInstance() != null)
                UIShopFeedback.ShowConfirmation();

            Application.OpenURL(result.PurchaseConfirmationPageURL);
        }


        /// <summary>
        /// Manually triggering purchase confirmation after a PayPal payment has been made.
        /// This is so that the transaction gets finished and PayPal actually substracts funds.
        /// </summary>
        public void ConfirmPurchase()
        {
            if (string.IsNullOrEmpty(orderId))
                return;

            ConfirmPurchaseRequest request = new ConfirmPurchaseRequest()
            {
                OrderId = orderId
            };

            PlayFabClientAPI.ConfirmPurchase(request, (result) => 
            {
                if (UIShopFeedback.GetInstance() != null && UIShopFeedback.GetInstance().confirmWindow != null)
                    UIShopFeedback.GetInstance().confirmWindow.SetActive(false);

                OnPurchaseSucceeded(result);
            }, OnPurchaseFailed);
        }
    }
    #endif
}