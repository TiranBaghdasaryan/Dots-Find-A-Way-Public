/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

#if SIS_IAP
using UnityEngine.Purchasing;
#endif

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SIS
{
    /// <summary>
    /// Shop item properties: stores all necessary variables for visualizing a product in the 2D shop UI.
    /// </summary>
    public class ShopItem2D : MonoBehaviour
    {
        [Header("Metadata")]
        /// <summary>
        /// ID of the product. Do not enter if you are letting IAPContainers instantiate your shop items.
        /// </summary>
        public string productID;

        /// <summary>
        /// Label for product name or title.
        /// </summary>
        public Text title;

        /// <summary>
        /// Label for product description.
        /// </summary>
        public Text description;

        /// <summary>
        /// Boolean for setting all label contents to uppercase.
        /// </summary>
        public bool uppercase = false;

        /// <summary>
        /// Icon sprite for visualization.
        /// </summary>
        public Image icon;

        [Header("Inventory")]
        /// <summary>
        /// Label displaying currently owned amount of a consumable product.
        /// </summary>
        public Text amount;

        /// <summary>
        /// Label displaying the expiration time of a subscription product.
        /// </summary>
        public Text expiration;

        [Header("Buttons")]
        /// <summary>
        /// Buy button that invokes the actual purchase.
        /// </summary>
        public GameObject buyButton;

        /// <summary>
        /// Buy trigger, used for making the buy button visible.
        /// (optional - can be used for 'double tap to purchase')
        /// </summary>
        public GameObject buyTrigger;

        /// <summary>
        /// Button for previewing bundle contents, if any.
        /// </summary>
        public GameObject previewButton;

        /// <summary>
        /// Button for selecting this item.
        /// </summary>
        public GameObject selectButton;

        /// <summary>
        /// Button for deselecting this item.
        /// </summary>
        public GameObject deselectButton;

        [Header("Unlocks")]
        /// <summary>
        /// Label that displays text while this item is locked.
        /// </summary>
        public Text lockedLabel;

        /// <summary>
        /// UI elements that will be de-activated when unlocking this item.
        /// </summary>
        public GameObject[] hideOnUnlock;

        /// <summary>
        /// UI elements that will be activated when unlocking this item.
        /// </summary>
        public GameObject[] showOnUnlock;

        [Header("States")]
        /// <summary>
        /// Additional UI element that will be activated on sold items.
        /// </summary>
        public GameObject discounted;

        /// <summary>
        /// Additional UI element that will be activated on sold items.
        /// </summary>
        public GameObject sold;

        /// <summary>
        /// Additional UI element that will be activated on selected items.
        /// </summary>
        public GameObject selected;

        /// <summary>
        /// Array of cost labels, associated to a real or virtual currency property.
        /// There could be more than one currency for virtual product purchases.
        /// </summary>
        [HideInInspector]
        public List<ShopItemCost> costs = new List<ShopItemCost>();

        //selection checkbox, cached for triggering other checkboxes
        //in the same group on selection/deselection
        private Toggle selCheck;
        //boolean for detecting automated or manual initialization
        private bool IsInitialized = false;


        //set up delegates and selection checkboxes
        void Start()
        {
            //this item does not seem to be instantiated by an IAPContainer
            //do manual initialization using the provided product identifier
            if (!IsInitialized && !string.IsNullOrEmpty(productID))
                Init(IAPManager.GetIAPProduct(productID));

            //if a selection of this item is possible
            if (selectButton)
            {
                //get checkbox component
                selCheck = selectButton.GetComponent<Toggle>();
                if (selCheck) selCheck.group = transform.parent.GetComponent<ToggleGroup>();
            }

            IAPManager.initializeSucceededEvent += OverwriteWithFetch;
        }


        //if we have a possible purchase confirmation set up or pending,
        //hide the buy button when disabling this item to reset it
        void OnDisable()
        {
            if (buyTrigger)
                ConfirmPurchase(false);
        }


        void OnDestroy()
        {
            IAPManager.initializeSucceededEvent -= OverwriteWithFetch;
        }


        /// <summary>
        /// Initialize virtual or real item properties based on IAPProduct set in IAP Project Settings.
        /// Called by IAPContainer, or manually.
        /// </summary>
        public void Init(IAPProduct product)
        {
            if (product == null) return;

            //cache
            IsInitialized = true;
            productID = product.ID;
            if(!IAPManager.shopItems.ContainsKey(productID))
                IAPManager.shopItems.Add(productID, this);

            string name = product.title;
            string descr = product.description.Replace("\\n", "\n");
            string lockText = product.requirement.label;
           
            //set icon to the matching sprite
            if (icon) icon.sprite = product.icon;

            //when 'uppercase' has been checked,
            //convert title and description text to uppercase,
            //otherwise just keep and set them as they are
            if (uppercase)
            {
                name = name.ToUpper();
                descr = descr.ToUpper();
                if (!string.IsNullOrEmpty(lockText))
                    lockText = lockText.ToUpper();
            }

            if (title) title.text = name;
            if (description) description.text = descr;

            foreach (ShopItemCost cost in costs)
            {
                if (cost.label == null)
                    continue;

                IAPExchangeObject exchange = null;
                switch (cost.type)
                {

                    case IAPExchangeObject.ExchangeType.RealMoney:
                        exchange = product.priceList.Find(x => x.type == cost.type);
                        if (exchange != null) cost.label.text = exchange.realPrice;
                        break;
                    case IAPExchangeObject.ExchangeType.VirtualCurrency:
                        exchange = product.priceList.Find(x => x.type == cost.type && x.currency.referenceID == cost.currency.referenceID);
                        if (exchange != null) cost.label.text = !string.IsNullOrEmpty(exchange.realPrice) ? exchange.realPrice : exchange.amount.ToString();
                        break;
                    case IAPExchangeObject.ExchangeType.VirtualProduct:
                        //skip costs for virtual products, due to difficulties in displaying them correctly
                        //virtual product costs should instead be mentioned in the product description
                        break;
                }
            }

            //set locked label text in case a requirement has been set
            if (lockedLabel && !string.IsNullOrEmpty(lockText))
                lockedLabel.text = lockText;

            if (previewButton) previewButton.SetActive(IAPManager.HasProductRewards(productID));

            //set item states based on database
            Refresh();
        }


        /// <summary>
        /// Method for overwriting shop item's properties with localized IAP data from the Store server. 
        /// When receiving this callback, we check if 'fetch' was checked for this product in the Project
        /// Settings editor, then simply reinitialize the items using the new data.
        /// </summary>
        public void OverwriteWithFetch()
        {
            IAPProduct product = IAPManager.GetIAPProduct(productID);
            if (product == null || !product.fetch) return;

            Init(product);
        }


        /// <summary>
        /// Refreshes the visual representation of this shop item.
        /// This is called automatically because of subscribing to the DBManager update event.
        /// It also means saving performance due to not refreshing all items every time.
        /// </summary>
        public void Refresh()
        {
            IAPProduct product = IAPManager.GetIAPProduct(productID);
            if (product == null) return;

            bool isSelected = DBManager.IsSelected(productID);
            bool isPurchased = product.type != ProductType.Consumable && DBManager.IsPurchased(productID);
            bool isDiscounted = !isPurchased && product.discount;

            if (amount && product.type == ProductType.Consumable)
                amount.text = DBManager.GetPurchase(productID) > 0 ? DBManager.GetPurchase(productID).ToString() : "";

            if (expiration && product.customData.ContainsKey("expiration"))
            {
                TimeSpan remaining = DateTime.Parse(product.customData["expiration"]) - DateTime.Now;
                expiration.text = remaining.TotalDays > 2 ? Mathf.FloorToInt((float)remaining.TotalDays) + "d" : Mathf.FloorToInt((float)remaining.TotalHours) + "h";
            }

            //double check that selected items are actually owned
            //if not, correct the entry by setting it to deselected
            if (isSelected && !isPurchased)
            {
                DBManager.SetDeselected(productID);
                isSelected = false;
            }

            if (isDiscounted && discounted != null)
                discounted.SetActive(true);

            if (isPurchased)
            {
                Purchased(true);

                //in case the item has been selected before, but also auto-select one item per group
                //more items per group can be pre-selected manually e.g. on app launch
                if (isSelected || (selectButton && !deselectButton && DBManager.GetSelectedGroup(IAPManager.GetProductCategoryName(productID)).Count == 0))
                {
                    IsSelected(true);
                }
            }
            else if (product.requirement.Exists() && DBManager.IsRequirementMet(product.requirement))
            {
                //check if a requirement is set up for this item,
                //then unlock if the requirement has been met    
                if (IAPManager.isDebug) Debug.Log("ShopItem: requirement is met for: " + product.ID + ". Unlocking.");                
                Unlock();
            }
        }


        /// <summary>
        /// Unlocks this item by hiding the 'locked' gameobject and setting up the default state.
        /// Called by ShopManager.
        /// </summary>
        public void Unlock()
        {
            for (int i = 0; i < hideOnUnlock.Length; i++)
                hideOnUnlock[i].SetActive(false);

            for (int i = 0; i < showOnUnlock.Length; i++)
                showOnUnlock[i].SetActive(true);
        }


        /// <summary>
        /// Show the buy button based on the bool passed in. This simulates 'double tap to purchase' behavior,
        /// and only works when setting a buyTrigger button.
        /// </summary>
        public void ConfirmPurchase(bool selected)
        {
            if (!selected)
                buyButton.SetActive(false);
        }


        /// <summary>
        /// 
        /// </summary>
        public void ShowPreview()
        {
            if (UIShopFeedback.GetInstance())
                UIShopFeedback.ShowRewardPreview(IAPManager.GetProductRewards(productID));
        }


        /// <summary>
        /// When the buy button has been clicked, here we try to purchase this item.
        /// This calls into the corresponding billing workflow of the IAPManager.
        /// </summary>
        public void Purchase()
        {
            IAPManager.Purchase(this.productID);

            //hide buy button once a purchase was made
            //only when an additional buy trigger was set
            if (buyTrigger)
                ConfirmPurchase(false);
        }


        /// <summary>
        /// Set this item to 'purchased' state (true), or unpurchased state (false) for fake purchases.
        /// </summary>
        public void Purchased(bool state)
        {
            //in case we restored an old purchase on a
            //locked item, we have to unlock it first
            Unlock();

            //toggle additional buttons
            //back to unpurchased state, deselect
            if (!state) Deselect();
            if (selectButton) selectButton.SetActive(state);
            if (previewButton) previewButton.SetActive(!state);
            
            //initialize variables for a product with upgrades
            IAPProduct product = IAPManager.GetIAPProduct(productID);
            bool hasUpgrade = false;

            //in case this good has upgrades, here we find the next upgrade
            //and replace displayed item data in the store with its upgrade details
            if (product.nextUpgrade != null)
            {
                hasUpgrade = true;
                Init(product.nextUpgrade);
            }

            //take upgrade state into account
            if (state && hasUpgrade)
                state = !hasUpgrade;

            //activate the sold gameobject
            if (sold) sold.SetActive(state);
            if (discounted) discounted.SetActive(false);

            //hide both buy trigger and buy button, for ignoring further purchase clicks.
            //but don't do that for subscriptions, so that the user could easily renew it
            if ((int)product.type > 1) return;

            if (buyTrigger)
            {
                buyTrigger.SetActive(!state);
                buyButton.SetActive(false);
            }
            else
                buyButton.SetActive(!state);
        }


        /// <summary>
        /// Handles selection state for this item, but this method gets called on other radio buttons within the same group too.
        /// Called by selectButton's Toggle component.
        /// </summary>
        public void IsSelected(bool thisSelect)
        {
            //if this object has been selected
            if (thisSelect)
            {
                //check if the item allows for single or multi selection,
                //this depends on whether the item has a deselect button
                bool single = deselectButton ? false : true;
                //pass arguments to DBManager
                DBManager.SetSelected(productID, single);

                //if we have a deselect button or a 'selected' gameobject, show them
                //and hide the select button for ignoring further selections              
                if (deselectButton) deselectButton.SetActive(true);
                if (selected) selected.SetActive(true);

                Toggle toggle = selectButton.GetComponent<Toggle>();
                if (toggle.group)
                {
                    //hacky way to deselect all other toggles, even deactivated ones
                    //(toggles on deactivated gameobjects do not receive onValueChanged events)
                    ShopItem2D[] others = toggle.group.GetComponentsInChildren<ShopItem2D>(true);
                    for (int i = 0; i < others.Length; i++)
                    {
                        if (others[i].selCheck.isOn && others[i] != this)
                        {
                            others[i].IsSelected(false);
                            break;
                        }
                    }
                }

                toggle.isOn = true;
                selectButton.SetActive(false);
            }
            else
            {
                //if another object has been selected, show the
                //select button for this item and hide the 'selected' state
                if (!deselectButton) selectButton.SetActive(true);
                if (selected) selected.SetActive(false);
            }
        }

        /// <summary>
        /// Called when deselecting this item via the deselectButton.
		/// <summary>
        public void Deselect()
        {
            //hide the deselect button and 'selected' state
            if (deselectButton) deselectButton.SetActive(false);
            if (selected) selected.SetActive(false);

            //tell our checkbox component that this object isn't checked
            if (selCheck) selCheck.isOn = false;
            //pass argument to DBManager
            DBManager.SetDeselected(productID);
            //re-show the select button
            if (selectButton) selectButton.SetActive(true);
        }
    }


    /// <summary>
    /// 
    /// </summary>
    [System.Serializable]
    public class ShopItemCost
    {
        public IAPExchangeObject.ExchangeType type;

        [SerializeReference]
        public IAPCurrency currency;

        public Text label;
    }
}
