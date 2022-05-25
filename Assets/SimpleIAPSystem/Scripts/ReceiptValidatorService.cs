/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

#if SIS_IAP
using UnityEngine.Purchasing;
#endif

#if PLAYFAB || PLAYFAB_VALIDATION
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.SharedModels;
#endif

namespace SIS
{
    /// <summary>
    /// Remote IAP receipt verification on PlayFab's servers. Only supported on purchase.
    /// </summary>
	public class ReceiptValidatorService : ReceiptValidator
    {
        #if PLAYFAB
        //subscribe to IAPManager events
        void Start()
        {
            if (!CanValidate() || !IAPManager.GetInstance())
                return;

            IAPManager.receiptValidationPurchaseEvent += Validate;
        }


        /// <summary>
        /// Overriding the base method to only trigger on PlayFab supported platforms.
        /// </summary>
        public override bool CanValidate()
        {
            #if !UNITY_EDITOR && UNITY_ANDROID
            if (Application.platform == RuntimePlatform.Android && StandardPurchasingModule.Instance().appStore != AppStore.GooglePlay)
                return false;
            #endif

            #if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS || UNITY_TVOS)
                return true;
            #else
                return false;
            #endif
        }


        /// <summary>
        /// Overriding the base method for calling a web request and passing in the product purchased. Since PlayFab needs a user
        /// for validating receipts, it is also checked if the user is logged in already. If not, device login is called as well.
        /// </summary>
        public override void Validate(Product p = null)
        {
            if (p == null || !p.hasReceipt)
                return;
            
            if (string.IsNullOrEmpty(PlayFabManager.userId))
            {
                //log in before validating
                PlayFabManager.GetInstance().LoginWithDevice((result) =>
                {
                    //login failed, treat like without validation
                    if(result == false)
                    {
                        OnValidationResult(new PlayFabResultCommon() { CustomData = p.definition.storeSpecificId });
                        return;
                    }

                    //we're logged in with PlayFab now
                    WaitForRequest(p);
                });
            }
            else
            {
                //we've been logged in all the time
                WaitForRequest(p);
            }
        }


        //the actual method constructing the web request for validation
        private void WaitForRequest(Product p)
        {
            #pragma warning disable 0219
            Hashtable hash = SIS.MiniJson.JsonDecode(p.receipt) as Hashtable;
			string receipt = string.Empty;
            #pragma warning restore 0219

            #if UNITY_ANDROID
            string signature = string.Empty;
            switch(StandardPurchasingModule.Instance().appStore)
            {
                case AppStore.GooglePlay:
                    hash = SIS.MiniJson.JsonDecode(hash["Payload"] as string) as Hashtable;
                    receipt = hash["json"] as string;
                    signature = hash["signature"] as string;

                    ValidateGooglePlayPurchaseRequest gRequest = new ValidateGooglePlayPurchaseRequest()
                    {
                        ReceiptJson = receipt,
                        Signature = signature,
                        CurrencyCode = p.metadata.isoCurrencyCode,
                        PurchasePrice = (uint)(p.metadata.localizedPrice * 100)
                    };

                    PlayFabClientAPI.ValidateGooglePlayPurchase(gRequest, OnValidationResult, OnValidationError, p.definition.storeSpecificId);
                    break;

                case AppStore.AmazonAppStore:
                    ValidateAmazonReceiptRequest aRequest = new ValidateAmazonReceiptRequest()
                    {
                        ReceiptId = p.receipt,
                        UserId = IAPManager.extensions.GetExtension<IAmazonExtensions>().amazonUserId,
                        CurrencyCode = p.metadata.isoCurrencyCode,
                        PurchasePrice = (int)(p.metadata.localizedPrice * 100)
                    };
                    PlayFabClientAPI.ValidateAmazonIAPReceipt(aRequest, OnValidationResult, OnValidationError, p.definition.storeSpecificId);
                    break;
            }   
            #elif UNITY_IOS || UNITY_TVOS
			receipt = hash["Payload"] as string;

            ValidateIOSReceiptRequest request = new ValidateIOSReceiptRequest()
            {
                ReceiptData = receipt,
                CurrencyCode = p.metadata.isoCurrencyCode,
                PurchasePrice = (int)(p.metadata.localizedPrice * 100)
            };
            PlayFabClientAPI.ValidateIOSReceipt(request, OnValidationResult, OnValidationError, p.definition.storeSpecificId);
            #endif
        }


        /// <summary>
        /// Callback from PlayFab when the receipt validation has been successful.
        /// Products will be granted and transactions confirmed. Mirror PlayFabStore-OnPurchaseSucceeded
        /// </summary>
        public void OnValidationResult(PlayFabResultCommon result)
        {
            Product p = IAPManager.controller.products.WithStoreSpecificID(result.CustomData as string);
            if (p == null) return;
            IAPManager.controller.ConfirmPendingPurchase(p);

            IAPProduct product = IAPManager.GetIAPProduct(p.definition.id);
            if (product == null) return;

            //successful response, verified transaction
            if (IAPManager.isDebug) Debug.Log(p.definition.storeSpecificId + " verification success.");

            if (product.rewardList.Exists(x => x.type == IAPExchangeObject.ExchangeType.VirtualProduct && x.amount > 0))
            {
                //in case product was added to inventory and not consumed immediately,
                //get its server item instance IDs for tracking a possible consumption call later
                PlayFabClientAPI.GetUserInventory(new GetUserInventoryRequest(), userInv =>
                {
                    ItemInstance item = null;
                    for (int i = 0; i < userInv.Inventory.Count; i++)
                    {
                        if (userInv.Inventory[i].ItemId == p.definition.storeSpecificId)
                        {
                            item = userInv.Inventory[i];
                            break;
                        }
                    }

                    PlayFabStore.instance.OnPurchaseSucceeded(new ConfirmPurchaseResult()
                    {
                        Items = new List<ItemInstance>() { item }
                    });
                }, error =>
                {
                    //fallback just grant the item directly
                    IAPManager.GetInstance().CompletePurchase(p.definition.id);
                });
            }
            else
            {
                //fallback just grant the item directly
                IAPManager.GetInstance().CompletePurchase(p.definition.id);
            }
        }


        /// <summary>
        /// Callback from PlayFab when the receipt validation has failed.
        /// The transaction will be confirmed, altough no products will be granted.
        /// </summary>
        public void OnValidationError(PlayFabError error)
        {
            //wrong store identifiers, potentially by changing or deleting product identifiers while testing
            //handle as with success, since the receipt could actually be valid but contain outdated products too
            //OnValidationError: PlayFabError(There is no item in the catalog with an ID matching the product ID in the receipt)
            if (error.Error == PlayFabErrorCode.NoMatchingCatalogItemForReceipt)
            {
                OnValidationResult(new PlayFabResultCommon() { CustomData = error.CustomData });
                return;
            }

            Product p = IAPManager.controller.products.WithStoreSpecificID(error.CustomData as string);
            if (p == null) return;
            IAPManager.controller.ConfirmPendingPurchase(p);

            //timeout detection
            //OnValidationError: PlayFabError(ServiceUnavailable, Could not resolve host: f31a.playfabapi.com, 400 BadRequest)
            //OnValidationError: PlayFabError(ServiceUnavailable, Failed to connect to f31a.playfabapi.com port 443: Timed out, 400 BadRequest)

            //the receipt could be invalid, e.g. faked or sent multiple times
            if (IAPManager.isDebug) Debug.Log("The receipt for '" + p.definition.storeSpecificId + "' could not be verified: " + error.ErrorMessage);
            IAPManager.OnPurchaseFailed("Purchase invalid: " + error.ErrorMessage);
        }
        #endif
    }
}