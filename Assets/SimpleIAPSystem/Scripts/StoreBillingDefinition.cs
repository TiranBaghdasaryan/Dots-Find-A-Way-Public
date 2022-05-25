using System;

namespace SIS
{
    [Serializable]
    public class StoreBillingDefinition
    {
        public bool active = false;

        public BillingProvider provider;
    }
}
