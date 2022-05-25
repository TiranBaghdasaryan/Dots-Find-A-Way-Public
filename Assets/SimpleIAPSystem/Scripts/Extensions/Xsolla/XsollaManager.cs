/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using SIS.SimpleJSON;

#if SIS_IAP
using UnityEngine.Purchasing;
#endif

#if XSOLLA
using Xsolla.Core;
using Xsolla.Login;
using Xsolla.Demo;
#endif

#pragma warning disable 0162, 0414
namespace SIS
{
    /// <summary>
    /// Manager integrating Xsolla's Store SDK for handling all related web requests, such as
    /// logging in and syncing Simple IAP System storage (purchases, player data, currency) with Xsolla.
    /// </summary>
    public class XsollaManager : MonoBehaviour
    {
        #if XSOLLA
        /// <summary>
        /// Static reference to this script.
        /// </summary>
        private static XsollaManager instance;

        /// <summary>
        /// 
        /// </summary>
        public string serverUrl;

        /// <summary>
        /// Xsolla account id of the user logged in.
        /// </summary>
        public static string userId;

        /// <summary>
        /// Fired when the user successfully logged in to a Xsolla account.
        /// </summary>
        public static event Action<UserInfo> loginSucceededEvent;

        /// <summary>
        /// Fired when logging in fails due to authentication or other issues.
        /// </summary>
        public static event Action<string> loginFailedEvent;

        private UserInfo loginResult;
        private List<UserAttribute> userData;
        private JSONNode serverData;


        /// <summary>
        /// Returns a static reference to this script.
        /// </summary>
        public static XsollaManager GetInstance()
        {
            return instance;
        }


        //setting up parameters and callbacks
        void Awake()
        {
            //make sure we keep one instance of this script in the game
            if (instance)
            {
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(this);

            //set static reference
            instance = this;
            gameObject.AddComponent<Xsolla.Login.XsollaLogin>();
            gameObject.AddComponent<Xsolla.Store.XsollaStore>();
            Xsolla.Core.WebRequestHelper webHelper = gameObject.AddComponent<Xsolla.Core.WebRequestHelper>();
            webHelper.SetReferralAnalytics("fb-simpleiap", "5.0.0");

            IAPManager.remoteInitializeReadyEvent += ReadyToInitialize;
            IAPManager.remoteShouldAddProductFunc += ShouldAddProductFromRemote;
            IAPManager.remoteFetchProductsEvent += FetchProductsFromRemote;
            IAPManager.remotePurchaseVirtualEvent += PurchaseVirtualFromRemote;
            IAPManager.remoteConsumePurchaseEvent += ConsumePurchaseFromRemote;

            //subscribe to selected events for handling them automatically
            DBManager.itemSelectedEvent += x => SetSelected();
            DBManager.itemDeselectedEvent += x => SetSelected();
        }


        void ReadyToInitialize()
        {
            IAPManager.GetInstance().GetComponent<DBManager>().memoryOnly = true;
            if (IAPManager.isDebug)
                UnityEngine.Debug.Log("Xsolla (online mode) is enabled: IAP data will not be saved on devices.");
        }


        bool ShouldAddProductFromRemote(IAPProduct product)
        {
            //if it's an IAP for real money, add it to the id list
            //we also add virtual products for cloud save
            if (!product.IsVirtual()) return true;

            //removing free virtual products because PlayFab does not keep them in their store
            if (!product.priceList.Exists(x => x.amount > 0))
                return false;

            return true;
        }


        void FetchProductsFromRemote()
        {
            //manually retrieve catalog items for remote overwrites, when using Xsolla on a platform
            //that does not utilize the XsollaStore class (e.g. XsollaLogin but without XsollaStore)
            #if !XSOLLA_IAP
            new XsollaStore().RetrieveProducts(null);
            #endif
        }


        void PurchaseVirtualFromRemote(IAPProduct product)
        {
            #if SIS_IAP && XSOLLA_IAP
            XsollaStore.instance.Purchase(product);
            #endif
        }


        void ConsumePurchaseFromRemote(IAPProduct product, int amount)
        {
            #if SIS_IAP && XSOLLA_IAP
            XsollaStore.instance.Consume(product, amount);
            #endif
        }


        public static void RedeemCoupon(string code)
        {
            #if SIS_IAP && XSOLLA_IAP
            XsollaStore.instance.RedeemCoupon(code);
            #endif
        }


        /// <summary>
        /// Return raw LoginResult data from Xsolla.
        /// Can be null if not logged in yet.
        /// </summary>
        /// <returns></returns>
        public UserInfo GetLoginResult()
        {
            return this.loginResult;
        }


        /// <summary>
        /// Grant products to the user's inventory on Xsolla, for free. Requires server.
        /// As this is a security risk you should have some server validations in place.
        /// This method is additive, calling it multiple times will result in multiple granted items.
        /// </summary>
        public static void SetPurchase(List<KeyValuePair<string, int>> products)
        {
            if (products.Count == 0)
                return;

            JSONNode body = new JSONClass();
            body.Add("method", "grantItems");
            body.Add("user", userId);

            body["items"] = new JSONArray();
            for (int i = 0; i < products.Count; i++)
            {
                JSONNode node = new JSONClass();
                node.Add("sku", products[i].Key);
                node.Add("quantity", new JSONData(products[i].Value));
                body["items"].Add(node);
            }

            GetInstance().StartCoroutine(instance.SendWebRequest(body.ToString()));
        }


        /// <summary>
        /// Uploads local player data to Xsolla.
        /// Call this after manipulating player data manually, e.g. via DBManager.AddPlayerData.
        /// </summary>
        public static void SetPlayerData()
        {
            string json = DBManager.GetJSON(DBManager.playerKey);
            JSONNode node = JSON.Parse(json);

            List<UserAttribute> list = new List<UserAttribute>();
            list.Add(new UserAttribute() { key = DBManager.playerKey, permission = "private", value = node.ToString() });

            XsollaLogin.Instance.UpdateUserAttributes(XsollaLogin.Instance.Token, XsollaSettings.StoreProjectId, list, null, null);
        }


        /// <summary>
        /// Uploads local currency balance to Xsolla. Requires server. Call this after setting currency manually,
        /// e.g. via DBManager.AddCurrency (with amount to add) or SetCurrency (no arguments).
        /// As this is a security risk you should have some server validations in place.
        /// </summary>
        public static void SetCurrency(Dictionary<string, int> toAdd = null)
        {
            Dictionary<string, int> dic = null;
            JSONNode body = new JSONClass();

            if (toAdd != null)
            {
                body.Add("method", "addCurrency");
                dic = toAdd;
            }
            else
            {
                body.Add("method", "setCurrency");
                dic = DBManager.GetCurrencies();
            }
            
            body.Add("user", userId);
            body["items"] = new JSONArray();
            foreach(KeyValuePair<string, int> pair in dic)
            {
                JSONNode node = new JSONClass();
                node.Add("sku", pair.Key);
                node.Add("quantity", new JSONData(pair.Value));
                body["items"].Add(node);
            }

            GetInstance().StartCoroutine(instance.SendWebRequest(body.ToString()));
        }


        /// <summary>
        /// Uploads local item selection states to Xsolla. Call this after manual selections, e.g. via DBManager.SetSelected.
        /// Note that selection states are synced automatically when selecting or deseleting items in the shop.
        /// </summary>
        public static void SetSelected()
        {
            List<UserAttribute> list = new List<UserAttribute>();
            list.Add(new UserAttribute() { key = DBManager.selectedKey, permission = "private", value = DBManager.GetJSON(DBManager.selectedKey) });

            XsollaLogin.Instance.UpdateUserAttributes(XsollaLogin.Instance.Token, XsollaSettings.StoreProjectId, list, null, null);
        }


        //
        IEnumerator SendWebRequest(string data)
        {
            if(string.IsNullOrEmpty(instance.serverUrl))
            {
                if (IAPManager.isDebug)
                    UnityEngine.Debug.LogError("Sending WebRequest for updating player data on Xsolla but Server URL is empty.");

                yield break;
            }

            using (UnityWebRequest www = UnityWebRequest.Put(instance.serverUrl, data.ToString()))
            {
                yield return www.SendWebRequest();

                if (!string.IsNullOrEmpty(www.error))
                {
                    OnXsollaError(new Error { errorCode = www.responseCode.ToString(), errorMessage = www.error });
                }
                else
                {
                    if(www.downloadHandler.text != "OK")
                    {
                        OnXsollaError(new Error { errorCode = www.responseCode.ToString(), errorMessage = www.downloadHandler.text });
                        yield break;
                    }

                    //only process client data if the cloud request was successful
                    JSONNode json = JSON.Parse(data);
                    
                    switch (json["method"])
                    {
                        case "grantItems":
                            JSONArray products = json["items"].AsArray;
                            for (int i = 0; i < products.Count; i++)
                            {
                                if (DBManager.GetPurchase(products[i]["sku"]) == 0)
                                    IAPManager.GetInstance().CompletePurchase(products[i]["sku"]);
                            }
                            break;

                        case "setCurrency":
                            JSONArray currencies = json["items"].AsArray;
                            for (int i = 0; i < currencies.Count; i++)
                            {
                                if (DBManager.GetCurrency(currencies[i]["sku"]) != currencies[i]["quantity"].AsInt)
                                    DBManager.SetCurrency(currencies[i]["sku"], currencies[i]["quantity"].AsInt);
                            }
                            break;
                    }
                }
            }
        }


        /// <summary>
        /// Register a new account by using the email address and password provided.
        /// </summary>
        public static void RegisterAccount(string emailAddress, string password)
        {
            XsollaLogin.Instance.Registration(emailAddress, password, emailAddress, onSuccess: OnRegisteredResult, onError: OnLoginError);
        }


        /// <summary>
        /// Login via email by using the email address and password provided.
        /// </summary>
        public static void LoginWithEmail(string emailAddress, string password)
        {
            XsollaLogin.Instance.SignIn(emailAddress, password, false, string.Empty, OnLoggedIn, OnLoginError);
        }


        /// <summary>
        /// Login via various social providers usually by entering email address or username and password.
        /// </summary>
        public static void LoginWithSocial(string providerName)
        {

            SocialProvider provider = (SocialProvider)Enum.Parse(typeof(SocialProvider), providerName);
            object[] arguments = { provider };

            #if UNITY_EDITOR || UNITY_STANDALONE
            TryAuthBy<SocialAuth>(arguments, ProcessToken, OnLoginError);
            #elif UNITY_ANDROID
			TryAuthBy<AndroidSocialAuth>(arguments, ProcessToken, OnLoginError);
            #endif

            void TryAuthBy<T>(object[] args, Action<string> onSuccess = null, Action<Error> onFailed = null) where T : MonoBehaviour, ILoginAuthorization
            {
                T auth = GetInstance().gameObject.AddComponent<T>();
                UnityEngine.Debug.Log($"Trying {auth.GetType().Name}");
                auth.OnSuccess = token => { Destroy(auth); onSuccess?.Invoke(token); };
                auth.OnError = error => { Destroy(auth); onFailed?.Invoke(error); };
                auth.TryAuth(args);
            }

            void ProcessToken(string token)
            {
                XsollaLogin.Instance.SaveToken(Constants.LAST_SUCCESS_AUTH_TOKEN, token);
                token = token.Split('&').First();
                var jwtToken = new Token(token);
                XsollaLogin.Instance.Token = jwtToken;

                OnLoggedIn(token);
            }
        }


        /// <summary>
        /// Request an account recovery email (new password) to the email passed in.
        /// </summary>
        public static void ForgotPassword(string emailAddress)
        {
            XsollaLogin.Instance.ResetPassword(emailAddress, OnEmailRecovery, OnLoginError);
        }


        private static void OnEmailRecovery()
        {
            if (loginFailedEvent != null)
                loginFailedEvent("Recovery Email sent. Please check your inbox.");
        }


        //process the login result and all user data retrieved from Xsolla, such as
        //user inventory with purchased products, virtual currency balances and title data
        private static void OnLoggedIn(string token)
        {
            XsollaLogin.Instance.GetUserInfo(XsollaLogin.Instance.Token, userResult => 
            {
                instance.loginResult = userResult;
                userId = userResult.id;

                if (IAPManager.isDebug)
                    UnityEngine.Debug.Log("Got XsollaID: " + userId + ((DateTime.Now - DateTime.Parse(userResult.registered)).TotalSeconds < 20 ? " (new account)" : " (existing account)"));

                XsollaLogin.Instance.GetUserAttributes(XsollaLogin.Instance.Token, XsollaSettings.StoreProjectId, UserAttributeType.CUSTOM, null, null, attributeResult =>
                {
                    instance.userData = attributeResult;

                    Xsolla.Store.XsollaStore.Instance.Token = XsollaLogin.Instance.Token;
                    Xsolla.Store.XsollaStore.Instance.GetInventoryItems(XsollaSettings.StoreProjectId, inventory =>
                    {
                        ConvertInventory(inventory);
                        IAPManager.GetInstance().Initialize();

                        if (loginSucceededEvent != null)
                            loginSucceededEvent(instance.loginResult);
                    }, OnLoginError);

                    //also get subscriptions for expiration times, but we do not have to wait for this request to finish
                    Xsolla.Store.XsollaStore.Instance.GetSubscriptions(XsollaSettings.StoreProjectId, ConvertSubscriptions, null);

                }, OnLoginError);
            }, OnLoginError);
        }


        private static void ConvertInventory(Xsolla.Store.InventoryItems result)
        {
            instance.serverData = new JSONClass();

            List<Xsolla.Store.InventoryItem> inventory = result.items.Where(x => x.type == "virtual_good").ToList();
            if (inventory != null && inventory.Count > 0)
            {
                string itemId = null;
                for (int i = 0; i < inventory.Count; i++)
                {
                    itemId = inventory[i].sku;
                    IAPProduct product = IAPManager.GetIAPProduct(itemId);

                    if (product != null)
                       instance.serverData[DBManager.contentKey][product.ID] = new JSONData((int)inventory[i].quantity);
                }
            }

            List<Xsolla.Store.InventoryItem> virtualCurrency = result.items.Where(x => x.type == "virtual_currency").ToList();
            if (virtualCurrency != null && virtualCurrency.Count > 0)
            {
                Dictionary<string, int> currency = DBManager.GetCurrencies();
                for(int i = 0; i < virtualCurrency.Count; i++)
                {
                    if (!currency.ContainsKey(virtualCurrency[i].sku))
                        continue;

                    instance.serverData[DBManager.currencyKey][virtualCurrency[i].sku].AsInt = (int)virtualCurrency[i].quantity;
                }
            }

            //getting custom user data from UserAttributes
            if (instance.userData != null && instance.userData.Count != 0)
            {
                foreach(UserAttribute att in instance.userData)
                {
                    JSONNode node = JSON.Parse(att.value);

                    foreach (string groupKey in node.AsObject.Keys)
                    {
                        JSONArray array = node[groupKey].AsArray;

                        //it's a simple value, not an array (usually Player data)
                        if (array == null)
                        {
                            instance.serverData[att.key][groupKey] = node[groupKey].Value;
                            continue;
                        }

                        //it's an array of values (usually Selected group data)
                        for (int j = 0; j < array.Count; j++)
                        {
                            instance.serverData[att.key][groupKey][j] = array[j].Value;
                        }
                    }
                }
            }

            DBManager.Overwrite(instance.serverData.ToString());
        }


        private static void ConvertSubscriptions(Xsolla.Store.SubscriptionItems result)
        {
            if (result == null || result.items == null || result.items.Length == 0)
                return;

            for (int i = 0; i < result.items.Length; i++)
            {
                if (result.items[i].Status != Xsolla.Store.SubscriptionStatusType.Active || result.items[i].expired_at == null)
                    continue;

                IAPProduct product = IAPManager.GetIAPProduct(result.items[i].sku);

                if (product != null)
                {
                    DateTime date = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds((double)result.items[i].expired_at).ToLocalTime();
                    product.customData.Remove("expiration");
                    product.customData.Add("expiration", date.ToString("u"));
                }
            }
        }


        //called on successful registration of a new account, advise user to check inbox
        private static void OnRegisteredResult()
        {
            if (loginFailedEvent != null)
                loginFailedEvent("Account Activation Email sent. Please check your inbox.");
        }


        //called after receiving an error when trying to log in
        private static void OnLoginError(Xsolla.Core.Error error)
        {
            string errorText = string.Empty;
            if (error != null)
            {
                errorText = error.errorMessage;
                UnityEngine.Debug.LogError(errorText);
            }

            if (loginFailedEvent != null)
                loginFailedEvent(errorText);
        }

        //called after receiving an error for any request
        //(except login methods since they have their own error callback)
        private static void OnXsollaError(Xsolla.Core.Error error)
        {
            if (!IAPManager.isDebug) return;

            UnityEngine.Debug.Log("Error: " + error.errorCode + ", " + error.statusCode + ", " + error.errorMessage);
        }


        public static void Logout()
        {
            userId = string.Empty;
            XsollaLogin.Instance.Token = new Xsolla.Core.Token();
        }
        #endif
    }
}