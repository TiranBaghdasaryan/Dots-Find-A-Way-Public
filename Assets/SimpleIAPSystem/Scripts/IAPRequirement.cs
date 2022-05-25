using System;
using System.Collections.Generic;
using UnityEngine;

namespace SIS
{
    /// <summary>
    /// IAP unlock requirement, matched with the database.
    /// </summary>
    [Serializable]
    public class IAPRequirement
    {
        /// <summary>
        /// Database key name for the target value. Value to reach for unlocking this requirement.
        /// </summary>
        [SerializeField]
        public List<KeyValuePairStringInt> pairs = new List<KeyValuePairStringInt>();

        /// <summary>
        /// Optional label text that describes the requirement.
        /// </summary>
        public string label;


        /// <summary>
        /// 
        /// </summary>
        public bool Exists()
        {
            return pairs.Count > 0;
        }
    }
}
