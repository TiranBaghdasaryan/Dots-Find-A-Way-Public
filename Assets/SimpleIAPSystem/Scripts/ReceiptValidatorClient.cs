/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

#if UNITY_ANDROID || UNITY_IPHONE || UNITY_STANDALONE_OSX || UNITY_TVOS
//Create your obfuscator secrets using Window > Unity IAP > Receipt Validation Obfuscator before enabling local receipt validation in the next line,
//or add it to your Project Settings > Player > Other Settings under Scripting Define Symbols to keep this setting even when upgrading and overwriting this file 
//#define VALIDATION_CLIENT
#endif

#if SIS_IAP
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;


namespace SIS
{
    /// <summary>
    /// IAP receipt verification on the client (local, on the device) using Unity IAPs validator class.
    /// Only supported on purchase.
    /// </summary>
	public class ReceiptValidatorClient : ReceiptValidator
    {
        #if VALIDATION_CLIENT
        [Header("Apple App Store")]
        /// <summary>
        /// Whether StoreKit should use a test configuration instead of an Apple Root certificate.
        /// </summary>
        public bool useStoreKitTest = false;

        Dictionary<string, string> introductory_info_dict;

        //subscribe to IAPManager events
        void Start()
        {
            if (!CanValidate() || !IAPManager.GetInstance())
                return;

            IAPManager.receiptValidationInitializeEvent += Validate;
            IAPManager.receiptValidationPurchaseEvent += Validate;

            //IAPManager was already initialized before subscribing, validate manually
            //this could happen when run in the Unity editor without real-world delay
            if (IAPManager.controller != null) Validate();
        }


        private void Validate()
        {
            IAppleExtensions m_AppleExtensions = IAPManager.extensions.GetExtension<IAppleExtensions>();
            introductory_info_dict = m_AppleExtensions.GetIntroductoryPriceDictionary();

            Validate(null as Product);
        }


        /// <summary>
        /// Overriding the base method to only trigger on Unity IAP supported platforms.
        /// </summary>
        public override bool CanValidate()
        {
            //when running on Android, validation is only supported on Google Play
            if (Application.platform == RuntimePlatform.Android && StandardPurchasingModule.Instance().appStore != AppStore.GooglePlay)
                return false;

            return true;
        }


        /// <summary>
        /// Overriding the base method for constructing Unity IAP's CrossPlatformValidator and passing in purchase receipts.
        /// The validation result will either grant the item (success) or remove it from the inventory if granted already (failed).
        /// </summary>
        public override void Validate(Product p = null)
        {
            Product[] products = new Product[]{ p };
            bool withEvent = p != null;

            if (p == null)
                products = IAPManager.controller.products.all;

            CrossPlatformValidator validator = null;
            try
            {
                if (useStoreKitTest) validator = new CrossPlatformValidator(GooglePlayTangle.Data(), AppleStoreKitTestTangle.Data(), Application.identifier);
                else validator = new CrossPlatformValidator(GooglePlayTangle.Data(), AppleTangle.Data(), Application.identifier);
            }
            catch (NotImplementedException) { }

            for (int i = 0; i < products.Length; i++)
            {
                //products should contain a receipt on a real device
                if (!products[i].hasReceipt)
                {
                    #if !UNITY_EDITOR
                    SetUnpurchased(products[i].definition.id);
                    #endif

                    continue;
                }

                //we found a receipt for this product on the device, initiate client receipt verification.
                //if we haven't found a receipt for this item, yet it is set to purchased. This can't be,
                //maybe the database contains fake data. Only pass the id to verification so it will fail
                try
                {
                    // On Google Play, result will have a single product Id.
                    // On Apple stores receipts contain multiple products.
                    validator.Validate(products[i].receipt);
                    UpdateCustomData(null, products[i]);
                    IAPManager.GetInstance().CompletePurchase(products[i].definition.id, withEvent);

                    if (IAPManager.isDebug) Debug.Log("Local Receipt Validation passed for: " + products[i].definition.id);
                }
                catch (Exception ex)
                {                   
                    #if UNITY_EDITOR
                    //complete fake store = test mode purchases successfully anyway, but only in the editor
                    if(ex is NullReferenceException || ex.Message.Contains("fake"))
                    {
                        IAPManager.GetInstance().CompletePurchase(products[i].definition.id, withEvent);
                        if (IAPManager.isDebug) Debug.Log("Test Mode. Local Receipt Validation passed for: " + products[i].definition.id);
                        continue;
                    }
                    #endif

                    if (IAPManager.isDebug) Debug.Log("Local Receipt Validation failed for: " + products[i].definition.id + ". Exception: " + ex + ", " + ex.Message);

                    if ((ex is NullReferenceException || ex is IAPSecurityException))
                    {
                        SetUnpurchased(products[i].definition.id);
                    }
                };
            }

            void SetUnpurchased(string productID)
            {
                if (!DBManager.IsPurchased(productID))
                    return;

                ShopItem2D item = null;
                if (IAPManager.GetInstance())
                    item = IAPManager.GetShopItem(productID);
                if (item) item.Purchased(false);
                DBManager.ConsumePurchase(productID);
            }
        }

        
        private void UpdateCustomData(IAPProduct product, Product p)
        {
            if (product == null) product = IAPManager.GetIAPProduct(p.definition.id);
            if (product == null) return;

            if (product.type != ProductType.Subscription || !IsAvailableForSubscriptionManager(p.receipt))
                return;
                  
            string intro_json = (introductory_info_dict == null || !introductory_info_dict.ContainsKey(p.definition.storeSpecificId)) ? null : introductory_info_dict[p.definition.storeSpecificId];
            SubscriptionManager sub = new SubscriptionManager(p, intro_json);
            SubscriptionInfo info = sub.getSubscriptionInfo();

            DateTime exDate = info.getExpireDate().ToLocalTime();
            if ((exDate - DateTime.Now).TotalDays > 0)
            {
                product.customData.Remove("expiration");
                product.customData.Add("expiration", exDate.ToUniversalTime().ToString("u"));
            }
        }
        #endif


        //modified from the Unity IAP sample
        //developerPayload is not supported anymore
        private bool IsAvailableForSubscriptionManager(string receipt)
        {
            Hashtable hash = SIS.MiniJson.JsonDecode(receipt) as Hashtable;

            if (!hash.ContainsKey("Store") || !hash.ContainsKey("Payload"))
            {
                if (IAPManager.isDebug) Debug.Log("The product receipt does not contain enough information");
                return false;
            }

            string store = hash["Store"] as string;
            Hashtable payload = SIS.MiniJson.JsonDecode(hash["Payload"] as string) as Hashtable;

            switch (store)
            {
                case GooglePlay.Name:
                {
                    if (payload == null)
                    {
                        if (IAPManager.isDebug) Debug.Log("The product receipt does not contain enough information, payload is empty.");
                        return false;
                    }

                    if (!payload.ContainsKey("json"))
                    {
                        if (IAPManager.isDebug) Debug.Log("The product receipt does not contain enough information, the 'json' field is missing");
                        return false;
                    }

                    Hashtable json = SIS.MiniJson.JsonDecode(payload["json"] as string) as Hashtable;

                    if (json == null)
                    {
                        if (IAPManager.isDebug) Debug.Log("The product receipt does not contain enough information, json is empty.");
                        return false;
                    }

                    return true;
                }

                case AppleAppStore.Name:
                case AmazonApps.Name:
                case MacAppStore.Name:
                {
                    return true;
                }

                default:
                {
                    return false;
                }
            }
        }
    }
}
#endif