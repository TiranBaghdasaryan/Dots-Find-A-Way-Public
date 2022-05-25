/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using System;
using System.Collections;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Networking;
using SIS.SimpleJSON;
using System.Text.RegularExpressions;

#if SIS_IAP
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
#endif

namespace SIS
{
    #if !SIS_IAP
    public class PayPalStore
    { 
        public static PayPalStore instance { get; private set; } 
        public void ConfirmPurchase() { }
    }
    #endif

    #if SIS_IAP
    class PayPalStore : IStore
    {
        /// <summary>
        /// Reference to this store class, since the user needs to confirm the purchase
        /// transaction manually in-game, thus calling the confirm method of this script.
        /// </summary>
        public static PayPalStore instance { get; private set; }

        /// <summary>
        /// Callback for hooking into the custom Unity IAP logic.
        /// This is basically a stripped down version of the IStoreCallback.
        /// </summary>
        public IAPManager callback;

        //
        private PayPalStoreConfig config;

        //
        private AccessToken accessToken;

        //keeping track of the order that is currently being processed, so we can confirm and finish it later on.
        private string orderId;

        //keeping track of the product that is currently being processed
        private string currentProduct;


        public PayPalStore(IAPManager callback)
        {
            instance = this;
            this.callback = callback;

            config = callback.asset.customStoreConfig.PayPal;
        }


        public void Initialize(IStoreCallback callback)
        {
            //nothing to do here since the billing system has its own callback
        }


        public void RetrieveProducts(ReadOnlyCollection<ProductDefinition> products)
        {
            //nothing to do here since the billing system has its own callback
        }


        public void Purchase(ProductDefinition product, string developerPayload)
        {
            callback.StartCoroutine(Purchase(product.id));
        }


        IEnumerator Purchase(string productID)
        {
            if (accessToken == null || !accessToken.IsValid())
                yield return callback.StartCoroutine(GetAccessToken());

            if (accessToken == null || !accessToken.IsValid())
            {
                callback.OnPurchaseFailed(null, PurchaseFailureReason.SignatureInvalid);
                yield break;
            }

            IAPProduct product = IAPManager.GetIAPProduct(productID);
            if (product == null)
            {
                callback.OnPurchaseFailed(null, PurchaseFailureReason.ProductUnavailable);
                yield break;
            }

            string postData = GetPostData(product);
            using (UnityWebRequest www = UnityWebRequest.Post(GetUrl("order"), string.Empty))
            {
                UploadHandlerRaw uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(postData));
                uploadHandler.contentType = "application/json";
                www.uploadHandler = uploadHandler;

                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("Authorization", "Bearer " + accessToken.token);

                yield return www.SendWebRequest();
                
                #if UNITY_2020_1_OR_NEWER
                if (www.result != UnityWebRequest.Result.Success)
                #else
                if (www.isNetworkError || www.isHttpError)
                #endif
                {
                    if (IAPManager.isDebug)
                    {
                        Debug.Log("PayPalStore purchase error: " + www.error);
                        Debug.Log(www.downloadHandler.text);
                    }

                    callback.OnPurchaseFailed(null, PurchaseFailureReason.PurchasingUnavailable);
                }
                else
                {
                    JSONNode response = JSON.Parse(www.downloadHandler.text);
                    orderId = response["id"];
                    currentProduct = productID;

                    UIShopFeedback.ShowConfirmation();
                    Application.OpenURL(GetUrl("checkout") + orderId);
                }
            }
        }


        /// <summary>
        /// Manually triggering purchase confirmation after a PayPal payment has been made.
        /// This is so that the transaction gets finished and PayPal actually substracts funds.
        /// </summary>
        public void ConfirmPurchase()
        {
            if (string.IsNullOrEmpty(orderId))
            {
                callback.OnPurchaseFailed(null, PurchaseFailureReason.DuplicateTransaction);
                return;
            }

            callback.StartCoroutine(FinishTransaction());
        }


        public void FinishTransaction(ProductDefinition product, string transactionId)
        {
            //nothing to do here since the billing system has its own callback
        }


        IEnumerator FinishTransaction()
        {
            using (UnityWebRequest www = UnityWebRequest.Post(GetUrl("order") + "/" + orderId + "/capture", string.Empty))
            {
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("Authorization", "Bearer " + accessToken.token);

                yield return www.SendWebRequest();
                
                #if UNITY_2020_1_OR_NEWER
                if (www.result != UnityWebRequest.Result.Success)
                #else
                if (www.isNetworkError || www.isHttpError)
                #endif
                {
                    if (IAPManager.isDebug)
                    {
                        Debug.Log("PayPalStore finish error: " + www.error);
                        Debug.Log(www.downloadHandler.text);
                    }

                    if (www.downloadHandler.text.Contains("ORDER_NOT_APPROVED"))
                    {
                        if (UIShopFeedback.GetInstance() != null)
                            UIShopFeedback.ShowMessage("Order is not approved yet. Please confirm the transaction in your browser.");
                    }
                    else
                        callback.OnPurchaseFailed(null, PurchaseFailureReason.PaymentDeclined);
                }
                else
                {
                    JSONNode response = JSON.Parse(www.downloadHandler.text);
                    if (response["status"].Value == "COMPLETED")
                    {
                        callback.CompletePurchase(currentProduct);
                        orderId = currentProduct = string.Empty;

                        if (UIShopFeedback.GetInstance() != null && UIShopFeedback.GetInstance().confirmWindow != null)
                            UIShopFeedback.GetInstance().confirmWindow.SetActive(false);
                    }
                }
            }
        }


        string GetPostData(IAPProduct product)
        {
            JSONNode data = new JSONClass();
            data["intent"] = "CAPTURE";

            string price = product.priceList.Find(x => x.type == IAPExchangeObject.ExchangeType.RealMoney).realPrice;
            price = Regex.Match(price, @"[0-9]+\.?[0-9,]*").Value;

            JSONNode unit = new JSONClass();
            JSONNode amount = new JSONClass();
            amount["currency_code"] = config.currencyCode;
            amount["value"] = price;

            JSONNode total = new JSONClass();
            total["currency_code"] = amount["currency_code"].Value;
            total["value"] = amount["value"].Value;

            JSONNode breakdown = new JSONClass();
            breakdown["item_total"] = total;
            amount["breakdown"] = breakdown;

            unit["amount"] = amount;
            unit["description"] = "Goods for " + Application.productName;

            JSONNode item = new JSONClass();
            item["name"] = string.IsNullOrEmpty(product.title) ? product.ID : product.title;
            if (!string.IsNullOrEmpty(product.description)) item["description"] = product.description;
            item["unit_amount"] = amount;
            item["quantity"] = "1";
            item["sku"] = product.ID;

            unit["items"] = new JSONArray();
            unit["items"].Add(item);

            data["purchase_units"] = new JSONArray();
            data["purchase_units"].Add(unit);

            JSONNode context = new JSONClass();
            context["return_url"] = config.returnUrl;
            data["application_context"] = context;

            return data.ToString();
        }


        string GetUrl(string api)
        {
            switch(api)
            {
                case "token":
                    if (IAPManager.isDebug) return "https://api-m.sandbox.paypal.com/v1/oauth2/token";
                    else return "https://api-m.paypal.com/v1/oauth2/token";
                case "order":
                    if (IAPManager.isDebug) return "https://api-m.sandbox.paypal.com/v2/checkout/orders";
                    else return "https://api-m.paypal.com/v2/checkout/orders";
                case "checkout":
                    if (IAPManager.isDebug) return "https://www.sandbox.paypal.com/checkoutnow?token=";
                    else return "https://www.paypal.com/checkoutnow?token=";
            }

            return string.Empty;
        }


        IEnumerator GetAccessToken()
        {
            WWWForm form = new WWWForm();
            form.AddField("grant_type", "client_credentials");

            using (UnityWebRequest www = UnityWebRequest.Post(GetUrl("token"), form))
            {
                string auth = IAPManager.isDebug ? (config.sandbox.clientID + ":" + config.sandbox.secretKey) : (config.live.clientID + ":" + config.live.secretKey);
                auth = Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(auth));
                auth = "Basic " + auth;
                www.SetRequestHeader("Authorization", auth);

                yield return www.SendWebRequest();

                #if UNITY_2020_1_OR_NEWER
                if (IAPManager.isDebug && www.result != UnityWebRequest.Result.Success)
                #else
                if (IAPManager.isDebug && (www.isNetworkError || www.isHttpError))
                #endif
                {
                    Debug.Log("PayPalStore token error: " + www.error);
                    Debug.Log(www.downloadHandler.text);
                }
                else
                {
                    JSONNode response = JSON.Parse(www.downloadHandler.text);
                    accessToken = new AccessToken(response["access_token"], response["expires_in"].AsInt);
                }
            }
        }


        [Serializable]
        public class AccessToken
        {
            public string token;
            public long expirationTime;

            public AccessToken(string token, long time)
            {
                this.token = token;
                expirationTime = new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds() + time;
            }

            public bool IsValid()
            {
                return new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds() < expirationTime;
            }
        }
    }
    #endif
}
