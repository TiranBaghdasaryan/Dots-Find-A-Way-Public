/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

#if SIS_IAP
using UnityEngine.Purchasing;
#endif

using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using SIS.SimpleJSON;

namespace SIS
{
    /// <summary>
    /// Stores IAP related data such as all purchases, selected items and ingame currency.
    /// Makes use of the JSON format and simple encryption. You should only modify below
    /// values once (if necessary at all), thus they aren't public.
    /// </summary>
    public class DBManager : MonoBehaviour
    {
        /// <summary>
        /// The name of the playerpref key on the device.
        /// <summary>
        public const string prefsKey = "data";

        /// <summary>
        /// The prefix for storing receipt keys on the device.
        /// <summary>
        public const string idPrefixKey = "SIS_";

        /// <summary>
        /// Should purchase data only be saved in device memory rather than on disk? Warning:
        /// if you do not use some cloud save provider and login system (like PlayFab), your
        /// user's purchases will only exist throughout the current game session.
        /// </summary>
        public bool memoryOnly = false;

        /// <summary>
        /// Whether the data saved on the device should be encrypted.
        /// </summary>
        public bool encrypt = false;

        /// <summary>
        /// 56+8 bit key for encrypting the JSON string: 8 characters, do not use
        /// code characters (=.,? etc) and play-test that your key actually works!
        /// on Windows Phone this key must be exactly 16 characters (128 bit) long.
        /// SAVE THIS KEY SOMEWHERE ON YOUR END, SO IT DOES NOT GET LOST ON UPDATES
        /// </summary>
        public string obfuscKey;

        /// <summary>
        /// Fired when a data save/update on the device happens, delivering the updated key, when available.
        /// </summary>
        public static event Action<string> dataUpdateEvent;

        /// <summary>
        /// Fired when selecting a shop item, delivering its product ID.
        /// </summary>
        public static event Action<string> itemSelectedEvent;

        /// <summary>
        /// Fired when deselecting a shop item, delivering its product ID.
        /// </summary>
        public static event Action<string> itemDeselectedEvent;

        //array names for storing specific parts in the JSON string
        public const string currencyKey = "Currency";
        public const string contentKey = "Content";
        public const string selectedKey = "Selected";
        public const string playerKey = "Player";

        //static reference of this script
        private static DBManager instance;

        //whether or not old IAP entries on the user's device should be
        //removed if they don't exist in the IAP Settings editor anymore
        //true: keeps them, false: removes obsolete purchases. Your users
        //won't be too happy about an obsolete purchase though, change with caution
        private bool keepLegacy = true;

        //representation of device's data in memory during the game
        private JSONNode gameData;



        /// <summary>
        /// Initialization called by IAPManager in Awake().
        /// </summary>
        public void Init()
        {
            instance = this;
            InitDB();
        }


        //reads the saved data from the device and initializes it into memory
        void InitDB()
        {
            //create new JSON data
            gameData = new JSONClass();

            //look up existing playerpref
            if (!memoryOnly && PlayerPrefs.HasKey(prefsKey))
            {
                //read existing data string into memory
                string data = Read();
                if (!string.IsNullOrEmpty(data))
                    gameData = JSON.Parse(data);
            }

            //get all currencies defined in the IAP Editor
            List<IAPCurrency> currencies = IAPManager.GetInstance().asset.currencyList;

            //delete legacy entries which were
            //removed in the IAP Settings editor
            if (!keepLegacy)
            {
                //create new string array for all existing entries
                //on the device and copy paste them to this array
                string[] entries = new string[gameData[contentKey].Count];
                gameData[contentKey].AsObject.Keys.CopyTo(entries, 0);
                //loop over entries
                for (int i = 0; i < entries.Length; i++)
                {
                    //cache entry and find corresponding
                    //IAPObject of the IAP Settings editor
                    string id = entries[i];
                    IAPProduct obj = IAPManager.GetIAPProduct(id);

                    //if the IAP does not exist in the game anymore,
                    //or a non consumable has been switched to a consumable
                    //(consumable items don't go in the database)
                    if (obj == null || obj.type == ProductType.Consumable)
                    {
                        //remove this item id from contents
                        gameData[contentKey].Remove(id);
                        //in case it was a selected one,
                        //loop over selected groups and delete that one too
                        for (int j = 0; j < gameData[selectedKey].Count; j++)
                        {
                            if (gameData[selectedKey][j].ToString().Contains(id))
                                gameData[selectedKey][j].Remove(id);
                        }
                    }
                }

                //do the same with currency entries
                entries = new string[gameData[currencyKey].Count];
                gameData[currencyKey].AsObject.Keys.CopyTo(entries, 0);

                List<string> curNames = new List<string>();
                for (int i = 0; i < currencies.Count; i++)
                    curNames.Add(currencies[i].ID);

                //loop over entries
                for (int i = 0; i < entries.Length; i++)
                {
                    //cache currency name
                    string id = entries[i];
                    //if it does not exist in the game anymore,
                    //remove this currency from the device
                    if (!curNames.Contains(id))
                        gameData[currencyKey].Remove(id);
                }
            }

            //loop over currencies
            for (int i = 0; i < currencies.Count; i++)
            {
                //cache currency name
                string cur = currencies[i].ID;
                //don't create an empty currency name
                if (string.IsNullOrEmpty(cur))
                {
                    Debug.LogError("Found Currency in IAP Settings without a name. "
                                    + "The database will not know how to save it. Cancelling.");
                    return;
                }

                //check if the currency doesn't exist already within currencies,
                //then set the initial amount to the value entered in IAP Settings editor
                if (string.IsNullOrEmpty(gameData[currencyKey][cur]))
                    gameData[currencyKey][cur].AsInt = currencies[i].baseAmount;
            }

            //save modified data on the device
            Save();
        }


        /// <summary>
        /// Returns a static reference to this script.
        /// </summary>
        public static DBManager GetInstance()
        {
            return instance;
        }


        /// <summary>
        /// Returns the purchase amount of a product.
        /// </summary>
        public static int GetPurchase(string productID)
        {
            //if the product exists, return purchase amount
            if (instance.gameData[contentKey][productID] != null)
                return instance.gameData[contentKey][productID].AsInt;
            else
            {
                //check if the product is available for free
                IAPProduct obj = IAPManager.GetIAPProduct(productID);
                if (obj.IsVirtual() && obj.priceList.Count == 0)
                    return 1;
            }

            //otherwise return zero as default
            return 0;
        }


        /// <summary>
        /// Convenience method for checking whether a product is purchased or not.
        /// </summary>
        public static bool IsPurchased(string productID)
        {
            return GetPurchase(productID) > 0;
        }


        /// <summary>
        /// Sets a product id to purchased state. By default, the purchase amount is 1.
        /// Usually you would not want to call this directly except when granting products for free.
        /// </summary>
        public static void SetPurchase(string productID, int amount = 1)
        {
            instance.gameData[contentKey][productID].AsInt = amount;
            Save(productID);
        }


        //this is only being used for non-consumable products, now as usage on consumable products is
        //postponed until PlayFab properly supports inventory management including aggregated calls to
        //ConsumeItem, ModifyItemUses and GrantItemToUsers without making one API call per item / action
        /// <summary>
        /// This will increase the purchase amount by product id and return the new value.
        /// Negative values are clamped to 1.
        /// </summary>
        public static int AddPurchase(string productID, int amount)
        {
            IAPProduct product = IAPManager.GetIAPProduct(productID);
            if (product == null) return 0;

            //if the product is purchased already, just fire the dataUpdate event
            //this is so that even though technically nothing changed in the database,
            //we could still have extended a subscription and need to refresh the ShopItem
            if (product.type != ProductType.Consumable && IsPurchased(productID))
            {
                dataUpdateEvent?.Invoke(productID);
                return 1;
            }

            int newValue = instance.gameData[contentKey][productID].AsInt + Mathf.Clamp(amount, 1, int.MaxValue);
            instance.gameData[contentKey][productID].AsInt = newValue;
            Save(productID);
            return newValue;
        }


        /// <summary>
        /// Consumes a product. If amount is not passed in, it is removed from purchased state completely.
        /// This should be used with caution. Also used for virtual product costs, expired subscriptions or fake purchases.
        /// </summary>
        public static void ConsumePurchase(string productID, int amount = 0)
        {
            int newValue = instance.gameData[contentKey][productID].AsInt - Mathf.Clamp(amount, 0, int.MaxValue);

            if (amount == 0 || newValue <= 0)
                instance.gameData[contentKey].Remove(productID);
            else
                instance.gameData[contentKey][productID].AsInt = newValue;

            Save(productID);
        }


        /// <summary>
        /// Returns a player data node for a specific id.
        /// </summary>
        public static JSONNode GetPlayerData(string keyID)
        {
            //return node result
            return instance.gameData[playerKey][keyID];
        }


        /// <summary>
        /// Convenience method for checking whether player data exists or not.
        /// </summary>
        public static bool IsPlayerData(string keyID)
        {
            return !string.IsNullOrEmpty(GetPlayerData(keyID).Value);
        }


        /// <summary>
        /// Used for storing your own player-related data on the device.
        /// JSONData supports all primitive data types.
        /// </summary>
        public static void SetPlayerData(string keyID, JSONData data)
        {
            //pass result to node
            instance.gameData[playerKey][keyID] = data;
            Save(keyID);
        }


        /// <summary>
        /// This will increment the player-related data value defined by id and return the new value.
        /// Can only work for integer compatible data. Negative values are clamped to 1.
        /// </summary>
        public static int AddPlayerData(string keyID, int amount)
        {
            int newValue = instance.gameData[playerKey][keyID].AsInt + Mathf.Clamp(amount, 1, int.MaxValue);
            instance.gameData[playerKey][keyID].AsInt = newValue;
            Save(keyID);
            return newValue;
        }


        /// <summary>
        /// Consumes or removes a player data node for a specific id and saves the modified data on the device.
        /// Can only work for integer compatible data. If amount is not passed in, it is removed from player data completely.
        /// </summary>
        public static void ConsumePlayerData(string keyID, int amount = 0)
        {
            int newValue = instance.gameData[playerKey][keyID].AsInt - Mathf.Clamp(amount, 0, int.MaxValue);

            if (amount == 0 || newValue <= 0)
                instance.gameData[playerKey].Remove(keyID);
            else
                instance.gameData[playerKey][keyID].AsInt = newValue;

            Save(keyID);
        }


        /// <summary>
        /// Returns the amount of funds for a specific currency.
        /// </summary>
        public static int GetCurrency(string currencyID)
        {
            int value = 0;

            //check whether currency actually exists
            if (instance.gameData[currencyKey].Count == 0)
                Debug.LogWarning("Couldn't get funds, no currency specified or empty inventory.");
            else if (string.IsNullOrEmpty(instance.gameData[currencyKey][currencyID]))
                Debug.LogWarning("Couldn't get funds, currency: '" + currencyID + "' not found or set.");
            else
                value = instance.gameData[currencyKey][currencyID].AsInt;

            //return currency value
            return value;
        }


        /// <summary>
        /// Overwrites and/or sets the total amount of funds for a specific currency.
        /// </summary>
        public static void SetCurrency(string currencyID, int amount)
        {
            IAPCurrency currency = IAPManager.GetInstance().asset.currencyList.Find(x => x.ID == currencyID);
            if (currency == null) return;

            //don't allow currency outside bounds
            amount = Mathf.Clamp(amount, 0, Mathf.Abs(amount));
            if (currency.maxAmount > 0)
                amount = Mathf.Clamp(amount, 0, currency.maxAmount);

            //prior checks passed, set currency value
            instance.gameData[currencyKey][currencyID].AsInt = amount;
            //save modified data on the device
            Save();
        }


        /// <summary>
        /// Increases the amount of funds for a specific currency and return the new value.
        /// </summary>
        public static int AddCurrency(string currencyID, int amount)
        {
            IAPCurrency currency = IAPManager.GetInstance().asset.currencyList.Find(x => x.ID == currencyID);
            if (currency == null) return 0;

            //increase currency value
            JSONNode node = instance.gameData[currencyKey][currencyID];
            int newValue = node.AsInt + amount;

            //don't allow currency outside bounds
            newValue = Mathf.Clamp(newValue, 0, int.MaxValue);
            if (currency.maxAmount > 0)
                newValue = Mathf.Clamp(newValue, 0, currency.maxAmount);

            //save modified data on the device
            node.AsInt = newValue;
            Save();
            return newValue;
        }


        /// <summary>
        /// Convenience method for decreasing amount of funds for a specific currency and return the new value.
        /// </summary>
        public static int ConsumeCurrency(string currencyID, int amount)
        {
            return AddCurrency(currencyID, -amount);
        }


        /// <summary>
        /// Returns whether a requirement has been met.
        /// </summary>
        public static bool IsRequirementMet(IAPRequirement req)
        {
            foreach(KeyValuePairStringInt pair in req.pairs)
            {
                if (instance.gameData[playerKey][pair.Key] == null || instance.gameData[playerKey][pair.Key].AsInt < pair.Value)
                    return false;
            }

            return true;
        }


        /// <summary>
        /// Reads receipt data for a specific product id. Optionally supports decryption.
        /// </summary>
        public static string GetReceipt(string productID)
        {
            string key = idPrefixKey + productID;
            if (instance.encrypt)
                key = instance.Encrypt(key);
            //read existing data string
            string data = PlayerPrefs.GetString(key, "");
            //read data into memory
            //if encryption is enabled, decrypt before reading
            if (!string.IsNullOrEmpty(data) && instance.encrypt)
                data = instance.Decrypt(data);
            return data;
        }


        /// <summary>
        /// Saves receipt data along with the product id on the device. Optionally supports encryption.
        /// </summary>
        public static void SaveReceipt(string productID, string data)
        {
            string key = idPrefixKey + productID;
            if (instance.encrypt)
            {
                key = instance.Encrypt(key);
                data = instance.Encrypt(data);
            }
            PlayerPrefs.SetString(key, data);
            PlayerPrefs.Save();
        }


        /// <summary>
        /// This method checks user's currency and product inventory for a virtual purchase.
        /// Returns true if the user owns all resources necessary to afford the new product.
        /// </summary>
        public static bool CanPurchaseVirtual(IAPProduct product)
        {
            foreach (IAPExchangeObject exchange in product.priceList)
            {
                switch (exchange.type)
                {
                    case IAPExchangeObject.ExchangeType.VirtualCurrency:
                        if (GetCurrency(exchange.currency.ID) < exchange.amount)
                            return false;

                        break;

                    case IAPExchangeObject.ExchangeType.VirtualProduct:

                        if (GetPurchase(exchange.product.ID) < exchange.amount)
                            return false;

                        break;
                }
            }

            //validation succeeded
            return true;
        }


        /// <summary>
        /// 
        /// </summary>
        public static void PurchaseVirtual(IAPProduct product)
        {
            //the player has enough funds, loop over each exchange and substract costs
            foreach (IAPExchangeObject exchange in product.priceList)
            {
                switch (exchange.type)
                {
                    case IAPExchangeObject.ExchangeType.VirtualCurrency:
                        ConsumeCurrency(exchange.currency.ID, exchange.amount);
                        break;

                    case IAPExchangeObject.ExchangeType.VirtualProduct:

                        switch (exchange.product.type)
                        {
                            case ProductType.Consumable:
                                ConsumePurchase(exchange.product.ID, exchange.amount);
                                break;

                            case ProductType.NonConsumable:
                            case ProductType.Subscription:
                                ConsumePurchase(exchange.product.ID);
                                break;
                        }
                        break;
                }
            }

            Save();
        }


        /// <summary>
        /// Returns list that holds all purchased product ids. By default,
		/// for upgradeable products this only returns the current active one.
        /// </summary>
        public static List<string> GetAllPurchased(bool withUpgrades = false)
        {
            //create temporary string list
            List<string> temp = new List<string>();
            //find the correct content JSON node
            JSONNode node = instance.gameData[contentKey];
            //merge paid and free products (which are not saved on disk)
            List<string> mergedIDs = new List<string>(node.AsObject.Keys);
            mergedIDs = mergedIDs.Union(IAPManager.GetAllIDs()).ToList();

            //loop through keys and add product ids
            for (int i = 0; i < mergedIDs.Count; i++)
            {
                //check for purchase
                if (GetPurchase(mergedIDs[i]) == 0)
                    continue;

                //checking base product or upgrade but it is not the current one
                if (!withUpgrades && mergedIDs[i] != IAPManager.GetCurrentUpgrade(mergedIDs[i]))
                    continue;

                temp.Add(mergedIDs[i]);
            }

            //convert and return array
            return temp;
        }


        /// <summary>
        /// Returns a dictionary of all currencies (name and currently owned amount).
        /// </summary>
        public static Dictionary<string, int> GetCurrencies()
        {
            //create temporary currency list
            Dictionary<string, int> curs = new Dictionary<string, int>();
            List<IAPCurrency> currencies = IAPManager.GetInstance().asset.currencyList;

            for (int i = 0; i < currencies.Count; i++)
            {
                //find the correct currency JSON node
                JSONNode node = instance.gameData[currencyKey];
                if (node != null) node = node[currencies[i].ID];

                //add existing currencies with their values to the dictionary
                curs.Add(currencies[i].ID, node == null ? 0 : node.AsInt);
            }

            return curs;
        }


        /// <summary>
        /// Returns a dictionary that holds all group names with selected product ids.
        /// </summary>
        public static Dictionary<string, List<string>> GetAllSelected()
        {
            //create temporary string list
            Dictionary<string, List<string>> temp = new Dictionary<string, List<string>>();
            //find the correct selected JSON node
            JSONNode node = instance.gameData[selectedKey];
            //loop over groups and add all ids
            //iterate over product ids
            foreach (string key in node.AsObject.Keys)
            {
                string groupName = key;
                if (!temp.ContainsKey(groupName))
                    temp.Add(groupName, new List<string>());
                for (int j = 0; j < node[key].Count; j++)
                    temp[groupName].Add(node[key][j].Value);
            }
            //convert and return array
            return temp;
        }


        /// <summary>
        /// Returns all selected products within a specific group.
        /// </summary>
        public static List<string> GetSelectedGroup(string groupName)
        {
            List<string> list = new List<string>();

            if (instance.gameData[selectedKey][groupName] != null)
            {
                JSONNode node = instance.gameData[selectedKey][groupName];
                for (int i = 0; i < node.Count; i++)
                    list.Add(node[i].Value);
            }

            return list;
        }


        /// <summary>
        /// Sets a product id to selected state. If single is true, other ids in the
        /// same group get deselected. single = false allows for multi selection.
        /// Returns a boolean that indicates whether it was a new selection.
        /// </summary>
        public static bool SetSelected(string productID, bool single)
        {
            //get the group name the product was placed in
            string groupName = IAPManager.GetProductCategoryName(productID);
            //find the correct selected JSON node with that group name
            JSONNode node = instance.gameData[selectedKey][groupName];
            //if single select has been chosen and the product is already selected,
            //or in case multi selection is allowed and it is one of the selected ones,
            //do nothing
            if (node.ToString().Contains(productID))
                return false;
            //cache count of selected items
            int arrayCount = node.Count;
            //if single select is true, we loop through all selected ids
            //and remove them from this group, then just add this id
            if (single)
            {
                for (int i = 0; i < arrayCount; i++)
                    instance.gameData[selectedKey][groupName].Remove(i);
                instance.gameData[selectedKey][groupName][0] = productID;
            }
            //if multi select is possible,
            //we just add this id to the selected group
            else
                instance.gameData[selectedKey][groupName][arrayCount] = productID;

            //save modified data
            Save();
            itemSelectedEvent?.Invoke(productID);
            return true;
        }


        /// <summary>
        /// Returns whether a product has been selected.
        /// </summary>
        public static bool IsSelected(string id)
        {
            //if the product is included, return true
            //otherwise return false as default
            if (instance.gameData[selectedKey].ToString().Contains(id))
                return true;
            else
                return false;
        }


        /// <summary>
        /// Sets a product id to deselected state.
        /// </summary>
        public static void SetDeselected(string productID)
        {
            //get the group name the product was placed in
            string groupName = IAPManager.GetProductCategoryName(productID);
            //sanity check
            if (!instance.gameData[selectedKey].ToString().Contains(productID))
                return;
            //remove this id from the group of selected items
            instance.gameData[selectedKey][groupName].Remove(productID);
            //if this group now does not contain any ids,
            //remove it too
            if (instance.gameData[selectedKey][groupName].Count == 0)
                instance.gameData[selectedKey].Remove(groupName);

            //save modified data
            Save();
            itemDeselectedEvent?.Invoke(productID);
        }


        /// <summary>
        /// Returns the local data in string format.
        /// </summary>
        public static string Read()
        {
            string str = string.Empty;

            if (!instance.memoryOnly)
                str = PlayerPrefs.GetString(prefsKey);
            else
                str = instance.gameData.ToString();

            if (instance.encrypt) str = instance.Decrypt(str);
            return str;
        }


        /// <summary>
        /// Save modified data to the device.
        /// Optionally supports encryption.
        /// </summary>
        public static void Save(string key = "")
        {
            if (!instance.memoryOnly)
            {
                //read data from memory and cache as string
                string data = instance.gameData.ToString();
                //encrypt string, if enabled
                if (instance.encrypt)
                    data = instance.Encrypt(data);
                //store data into playerprefs
                PlayerPrefs.SetString(prefsKey, data);
                //save data on device
                PlayerPrefs.Save();
            }

            //notify subscribed scripts of data updates
            dataUpdateEvent?.Invoke(key);
        }


        /// <summary>
        /// Overwrite the current storage with another JSON representation.
        /// E.g. after downloading data from a remote server.
        /// </summary>
        public static void Overwrite(string otherData)
        {
            instance.gameData = JSON.Parse(otherData);
            Save();
        }


        /// <summary>
        /// Returns the desired JSON data node as a string.
        /// In case of content, free products are excluded.
        /// </summary>
        public static string GetJSON(string key)
        {
            return instance.gameData[key].ToString();
        }


        /// <summary>
        /// Remove data defined by section key. E.g. content, selected or currency.
        /// Should be used for testing purposes only.
        /// </summary>
        public static void Clear(string data)
        {
            //don't continue if no data was initialized
            if (instance.gameData == null) return;
            //remove full string part from data
            //and save result on the device
            instance.gameData.Remove(data);
            Save();
        }


        /// <summary>
        /// Removes all PlayerPref data set in this project.
        /// Should be used for testing purposes only.
        /// </summary>
        public static void ClearAll()
        {
            PlayerPrefs.DeleteAll();

            if (instance != null)
                instance.gameData = null;
        }


        //encrypt string passed in
        //based on obfuscation key
        private string Encrypt(string toEncrypt)
        {
            //convert obfuscation key and input string to byte array
            byte[] keyArray = UTF8Encoding.UTF8.GetBytes(obfuscKey);
            byte[] toEncryptArray = UTF8Encoding.UTF8.GetBytes(toEncrypt);
            byte[] resultArray = null;

            #if UNITY_ANDROID || UNITY_IOS
            //create new DES service and set all necessary properties
            DESCryptoServiceProvider des = new DESCryptoServiceProvider();
            des.Key = keyArray;
            des.Mode = CipherMode.ECB;
            des.Padding = PaddingMode.PKCS7;
            //create DES encryptor
            ICryptoTransform cTransform = des.CreateEncryptor();
            //encrypt input array, then convert back to string
            //and return final encrypted (unreadable) string
            resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            #else
            keyArray = null;
            resultArray = toEncryptArray;
            #endif

            return Convert.ToBase64String(resultArray, 0, resultArray.Length);
        }


        //decrypt string passed in
        //based on obfuscation key
        private string Decrypt(string toDecrypt)
        {
            //convert obfuscation key and input string to byte array
            byte[] keyArray = UTF8Encoding.UTF8.GetBytes(obfuscKey);
            byte[] toDecryptArray = Convert.FromBase64String(toDecrypt);
            byte[] resultArray = null;

            #if UNITY_ANDROID || UNITY_IOS
            //create new DES service and set all necessary properties
            DESCryptoServiceProvider des = new DESCryptoServiceProvider();
            des.Key = keyArray;
            des.Mode = CipherMode.ECB;
            des.Padding = PaddingMode.PKCS7;
            //create DES decryptor
            ICryptoTransform cTransform = des.CreateDecryptor();
            //decrypt input array, then convert back to string
            //and return final decrypted (raw) string
            resultArray = cTransform.TransformFinalBlock(toDecryptArray, 0, toDecryptArray.Length);
            #else
            keyArray = null;
            resultArray = toDecryptArray;
            #endif

            return UTF8Encoding.UTF8.GetString(resultArray, 0, resultArray.Length);
        }


        /// <summary>
        /// 
        /// </summary>
        public static string GetDeviceId()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            AndroidJavaClass up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = up.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject contentResolver = currentActivity.Call<AndroidJavaObject>("getContentResolver");
            AndroidJavaClass secure = new AndroidJavaClass("android.provider.Settings$Secure");
            return secure.CallStatic<string>("getString", contentResolver, "android_id");
            #else
            return SystemInfo.deviceUniqueIdentifier;
            #endif
        }
    }
}