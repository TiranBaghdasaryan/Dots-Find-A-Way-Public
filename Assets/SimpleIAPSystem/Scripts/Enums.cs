/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace SIS
{
    public class EnumHelper
    {
        /// <summary>
        /// Returns an array of string values for a given enum type.
        /// This will only work if you assign the Description attribute to the enum items.
        /// </summary>
        public static string[] GetEnumDescriptions(Enum e)
        {
            List<string> list = new List<string>();
            foreach (Enum en in Enum.GetValues(e.GetType()))
            { list.Add(GetEnumDescription(en)); }

            return list.ToArray();
        }

        /// <summary>
        /// Returns the string value for a given enum value.
        /// This will only work if you assign the Description attribute to the enum items.
        /// </summary>
        public static String GetEnumDescription(Enum e)
        {
            FieldInfo fieldInfo = e.GetType().GetField(e.ToString());

            DescriptionAttribute[] enumAttributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);

            if (enumAttributes.Length > 0)
            {
                return enumAttributes[0].Description;
            }
            return e.ToString();
        }
    }


    /// <summary>
    /// 
    /// </summary>
    public enum BuildTargetIAP
    {
        Standalone = 1,
        iOS = 2,
        Android = 4,
        WebGL = 8,
        WSA = 16,
        tvOS = 32
    }


    /// <summary>
    /// 
    /// </summary>
    public enum UIAssetPlugin
    {
        UnityUI,
        TextMeshPro
    }


    /// <summary>
    /// 
    /// </summary>
    public enum DesktopPlugin
    {
        [Description("")]
        UnityIAP = 0,

        [Description("PLAYFAB_PAYPAL")]
        PlayfabPaypal = 1,

        [Description("PLAYFAB_STEAM")]
        PlayfabSteam = 2,

        [Description("PLAYFAB_FACEBOOK")]
        PlayfabFacebook = 3,

        [Description("OCULUS_IAP")]
        OculusRift = 4,

        [Description("STEAM_IAP")]
        Steam = 5,

        [Description("XSOLLA_IAP")]
        Xsolla = 6
    }


    /// <summary>
    /// 
    /// </summary>
    public enum AndroidPlugin
    {
        [Description("")]
        UnityIAP = 0,

        [Description("XSOLLA_IAP")]
        Xsolla = 1
    }


    /// <summary>
    /// 
    /// </summary>
    public enum IOSPlugin
    {
        [Description("")]
        UnityIAP = 0,

        [Description("XSOLLA_IAP")]
        Xsolla = 1
    }


    /// <summary>
    /// 
    /// </summary>
    public enum WebPlugin
    {
        [Description("")]
        UnityIAP = 0,

        [Description("PLAYFAB_PAYPAL")]
        PlayfabPaypal = 1,

        [Description("PLAYFAB_FACEBOOK")]
        PlayfabFacebook = 2,

        [Description("XSOLLA_IAP")]
        Xsolla = 3
    }


    /// <summary>
    /// 
    /// </summary>
    public enum ThirdPartyPlugin
    {
        [Description("")]
        None = 0,

        [Description("PLAYFAB")]
        PlayFab = 1,

        [Description("XSOLLA")]
        Xsolla = 2
    }


    /// <summary>
    /// Supported billing stores.
    /// </summary>
    public enum IAPPlatform
    {
        NotSpecified = 0,
        GooglePlay = 1,
        AmazonAppStore = 2,
        //SamsungApps = 3, obsolete
        UDP = 4,
        MacAppStore = 5,
        AppleAppStore = 6,
        WinRT = 7,
        FacebookStore = 8,
        fake = 9,

        OculusStore = 20,
        SteamStore = 21,
        XsollaStore = 22,
        PayPal = 30
    }

    /// <summary>
    /// 
    /// </summary>
    public enum BillingProvider
    {
        PayPal = 0
    }


    #if !SIS_IAP
    public enum ProductType
    {
        Consumable = 0,
        NonConsumable = 1,
        Subscription = 2
    }


    public class StoreID
    {
        public string id;
        public string store;

        public StoreID(string id, string store) {}
    }


    public class Product {}
    #endif
}
