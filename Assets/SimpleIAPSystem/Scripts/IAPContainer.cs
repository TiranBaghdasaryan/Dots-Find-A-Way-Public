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
    /// Stretches a container rect transform to include all children.
    /// Also repositions children using the GridLayoutGroup, if found.
    /// </summary>
    public class IAPContainer : MonoBehaviour
    {
        /// <summary>
        /// 
        /// </summary>
        [HideInInspector, SerializeReference]
        public IAPCategory category;

        /// <summary>
        /// Prefab used for instantiating items from category.
        /// </summary>
        public GameObject itemPrefab;

        /// <summary>
        /// Maximum value for clamping the product vertically.
        /// </summary>
        public int maxCellSizeX;

        /// <summary>
        /// Maximum value for clamping the product horizontally.
        /// </summary>
        public int maxCellSizeY;


        void Start()
        {
            if (category == null || itemPrefab == null)
                return;

            CreateShopItems();
            Reposition();
        }


        public void CreateShopItems()
        {
            List<IAPProduct> products = IAPManager.GetInstance().asset.productList.FindAll(x => x.category.referenceID == this.category.referenceID);

            //skip group if prefab or parent wasn't set
            if (itemPrefab == null)
            {
                return;
            }

            //loop over items
            for (int i = 0; i < products.Count; i++)
            {
                //cache item
                IAPProduct product = products[i];

                //the item has already been placed in the scene manually
                //dont instantiate it in a container then
                if (IAPManager.GetShopItem(product.ID) != null)
                    continue;

                //check overrides
                IAPCategory category = IAPManager.GetInstance().asset.categoryList.Find(x => x.referenceID == product.category.referenceID);
                if (category.storeIDs.Find(x => x.store == IAPManager.appStore && !x.active) != null) continue;
                else if (product.storeIDs.Find(x => x.store == IAPManager.appStore && !x.active) != null) continue;

                //instantiate item in the scene and attach it to the defined parent transform
                GameObject newItem = Instantiate(itemPrefab);
                newItem.transform.SetParent(transform, false);
                newItem.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                //rename item to force ordering as set in the IAP Settings editor
                newItem.name = "IAPItem " + string.Format("{0:000}", (transform.childCount - 1) + 1);
                
                //get IAPItem component of the instantiated item
                ShopItem2D item = newItem.GetComponent<ShopItem2D>();
                if (item == null) continue;

                //add IAPItem to dictionary for later lookup
                IAPManager.shopItems.Add(product.ID, item);

                //upgrades overwrite, an IAP Item gets replaced with its current level
                List<string> upgrades = IAPManager.GetAllUpgrades(product.ID);
                if (upgrades != null && upgrades.Count > 0)
                {
                    for (int k = 0; k < upgrades.Count; k++)
                        IAPManager.shopItems.Add(upgrades[k], item);

                    string currentUpgrade = IAPManager.GetNextUpgrade(product.ID);

                    if (!string.IsNullOrEmpty(currentUpgrade))
                        product = IAPManager.GetIAPProduct(currentUpgrade);
                }

                //initialize and set up item properties based on the associated IAPObject
                //they could get overwritten by online data later
                item.Init(product);
            }
        }


        /// <summary>
        /// Executes the repositioning of UI elements within the grid layout.
        /// </summary>
        public void Reposition()
        {
            if (transform.childCount == 0) return;
            RectTransform rectTrans = GetComponent<RectTransform>();
            GridLayoutGroup grid = GetComponent<GridLayoutGroup>();

            if (rectTrans != null && grid != null)
            {
                RectTransform child = transform.GetChild(0).GetComponent<RectTransform>();

                switch (grid.startAxis)
                {
                    case GridLayoutGroup.Axis.Vertical:
                        grid.cellSize = new Vector2(rectTrans.rect.width, child.rect.height);
                        float newHeight = child.rect.height * transform.childCount;
                        newHeight += (transform.childCount - 1) * grid.spacing.y + grid.padding.top + grid.padding.bottom;
                        rectTrans.sizeDelta = new Vector2(rectTrans.sizeDelta.x, newHeight);
                        break;
                    case GridLayoutGroup.Axis.Horizontal:
                        grid.cellSize = new Vector2(child.rect.width, rectTrans.rect.height);
                        float newWidth = child.rect.width * transform.childCount;
                        newWidth += (transform.childCount - 1) * grid.spacing.x + grid.padding.left + grid.padding.right;
                        rectTrans.sizeDelta = new Vector2(newWidth, rectTrans.sizeDelta.y);
                        break;
                }

                if (maxCellSizeX > 0 && grid.cellSize.x > maxCellSizeX)
                    grid.cellSize = new Vector2(maxCellSizeX, grid.cellSize.y);
                if (maxCellSizeY > 0 && grid.cellSize.y > maxCellSizeY)
                    grid.cellSize = new Vector2(grid.cellSize.x, maxCellSizeY);

                grid.enabled = true;
            }
        }
    }
}
