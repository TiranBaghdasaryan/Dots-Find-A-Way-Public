/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SIS
{
    /// <summary>
    ///
    /// </summary>
    public class UIWindowShowReward : MonoBehaviour
    {
        /// <summary>
        /// 
        /// </summary>
        public Transform container;

        /// <summary>
        /// 
        /// </summary>
        public GameObject itemPrefab;

        /// <summary>
        /// 
        /// </summary>
        public bool includeChildRewards = false;


        public void SetItems(List<KeyValuePairStringInt> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                //instantiate item in the scene and attach it to the defined parent transform
                GameObject newItem = Instantiate(itemPrefab);
                newItem.transform.SetParent(container, false);
                newItem.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                //rename item to force ordering as set in the IAP Settings editor
                newItem.name = "BundleItem " + string.Format("{0:000}", (container.childCount - 1) + 1);

                IAPProduct product = IAPManager.GetIAPProduct(items[i].Key);
                IAPCurrency currency = IAPManager.GetInstance().asset.currencyList.Find(x => x.ID == items[i].Key);

                if (product != null && currency != null)
                {
                    //we are not sure if this is a product or a currency, do not handle it as a product
                    product = null;
                }

                Image image = newItem.GetComponentInChildren<Image>();
                if (image != null)
                {
                    if (product != null) image.sprite = product.icon;
                    else if (currency != null) image.sprite = currency.icon;
                    else image.enabled = false;
                }

                Text txt = newItem.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    if (product != null) txt.text = items[i].Value + " " + product.title + "\n" + product.description;
                    else txt.text = items[i].Value + " " + items[i].Key;
                }

                if(includeChildRewards && product != null)
                {
                    SetItems(IAPManager.GetProductRewards(product.ID));
                }
            }
        }


        //reset container to original state
        void OnDisable()
        {
            int count = container.childCount;
            for (int i = count - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
            }
        }
    }
}
