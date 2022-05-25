/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using System;
using UnityEngine;

namespace SIS
{
    /// <summary>
    /// IAP currency, defined in the IAP Setting editor.
    /// </summary>
    [Serializable]
    public class IAPCurrency
    {
        [HideInInspector]
        public string referenceID;

        /// <summary>
        /// Unique currency name.
        /// </summary>
        public string ID;

        /// <summary>
        /// Default starting value for new players.
        /// </summary>
        public int baseAmount = 0;

        /// <summary>
        /// The maximum amount that can be earned. 0 for infinite.
        /// </summary>
        public int maxAmount = 0;

        /// <summary>
        /// Image to visualize currency in CurrencyContainer or Bundle. 
        /// </summary>
        public Sprite icon;
    }
}
