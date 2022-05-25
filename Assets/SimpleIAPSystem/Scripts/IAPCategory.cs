using System;
using System.Collections.Generic;
using UnityEngine;

namespace SIS
{
    /// <summary>
    /// IAP Settings editor group properties. Each group holds a list of IAPObject.
    /// </summary>
    [Serializable]
    public class IAPCategory
    {
        /// <summary>
        /// The unique group id for identifying mappings to an IAPContainer in the scene.
        /// </summary>
        [HideInInspector]
        public string referenceID;

        /// <summary>
        /// The unique name of the group.
        /// </summary>
        public string ID;

        /// <summary>
        /// 
        /// </summary>
        public List<StoreMetaDefinition> storeIDs = new List<StoreMetaDefinition>();

        /// <summary>
        /// 
        /// </summary>
        public StoreBillingDefinition customBilling = new StoreBillingDefinition();
    }
}
