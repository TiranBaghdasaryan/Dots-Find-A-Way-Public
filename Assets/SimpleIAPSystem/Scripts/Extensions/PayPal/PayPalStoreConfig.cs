/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using System;

namespace SIS
{
    [Serializable]
    public class PayPalStoreConfig
    {
        public bool enabled = false;

        public string currencyCode = "USD";

        public Credentials sandbox = new Credentials();

        public Credentials live = new Credentials();

        public string returnUrl;


        [Serializable]
        public class Credentials
        {
            public string clientID;

            public string secretKey;
        }
    }
}
