/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SIS
{
    [Serializable]
    [ExcludeFromPreset]
    public class IAPScriptableObject : ScriptableObject
    {
        /// <summary>
        /// In app products, set in the IAP Settings editor
        /// </summary>
        public List<IAPCategory> categoryList = new List<IAPCategory>();

        /// <summary>
        /// List of virtual currency, set in the IAP Settings editor
        /// </summary>
        public List<IAPCurrency> currencyList = new List<IAPCurrency>();

        /// <summary>
        /// 
        /// </summary>
        public List<IAPProduct> productList = new List<IAPProduct>();

        /// <summary>
        /// 
        /// </summary>
        public CustomStoreConfig customStoreConfig = new CustomStoreConfig();


        #if UNITY_EDITOR
        public static IAPScriptableObject GetOrCreateSettings()
        {
            string path = AssetDatabase.GetAssetPath(Resources.Load("IAPManager"));
            path = path.Replace("IAPManager.prefab", "IAPSettings.asset");

            var settings = AssetDatabase.LoadAssetAtPath<IAPScriptableObject>(path);
            if (settings == null)
            {
                settings = CreateInstance<IAPScriptableObject>();
                AssetDatabase.CreateAsset(settings, path);
                AssetDatabase.SaveAssets();
            }

            return settings;
        }


        public static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }
        #endif


        [Serializable]
        public class CustomStoreConfig
        {
            public PayPalStoreConfig PayPal = new PayPalStoreConfig();
        }
    }
}
