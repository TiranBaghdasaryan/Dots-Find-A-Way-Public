using System;
using UnityEngine;

namespace SIS
{
    [Serializable]
    public class IAPExchangeObject
    {
        public ExchangeType type = ExchangeType.RealMoney;
        public enum ExchangeType
        {
            RealMoney,
            VirtualCurrency,
            VirtualProduct
        }

        [SerializeReference]
        public IAPCurrency currency;

        [SerializeReference]
        public IAPProduct product;

        public int amount;

        public string realPrice;
    }
}
