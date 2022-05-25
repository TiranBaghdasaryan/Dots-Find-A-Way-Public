/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using SIS.SimpleJSON;

#if SIS_IAP
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
#endif

#if XSOLLA_IAP
using Xsolla.Core;
using Xsolla.Store;
using System.Collections;
#endif

namespace SIS
{
    #if SIS_IAP && !XSOLLA_IAP
    public class XsollaStore : IStore
    {
        public void FinishTransaction(ProductDefinition product, string transactionId) { throw new System.NotImplementedException(); }
        public void Initialize(IStoreCallback callback) { throw new System.NotImplementedException(); }
        public void Purchase(ProductDefinition product, string developerPayload) { throw new System.NotImplementedException(); }
        public void RetrieveProducts(ReadOnlyCollection<ProductDefinition> products) { throw new System.NotImplementedException(); }

        public static XsollaStore instance { get; private set; }
        public void ConfirmPurchase() { }
    }
    #endif

    #if SIS_IAP && XSOLLA_IAP
    /// <summary>
    /// Represents the public interface of the underlying store system for PlayFab.
    /// This is the store base class other PlayFab billing implementations are making use of.
    /// </summary>
    public class XsollaStore : IStore
    {
        /// <summary>
        /// Callback for hooking into the native Unity IAP logic.
        /// </summary>
        public IStoreCallback callback;

        /// <summary>
        /// List of products which are declared and retrieved by the billing system.
        /// </summary>
        public Dictionary<string, ProductDescription> products;

        /// <summary>
        /// Keeping track of the order that is currently being processed,
        /// so we can confirm and finish it later on.
        /// </summary>
        public static int orderId;

        //product catalog that has been retrieved from Xsolla
        private static List<StoreItem> catalog;
        //keeping track of the product that is currently being processed
        private string currentProduct;


        /// <summary>
        /// Reference to this store class, since the user needs to confirm the purchase
        /// transaction manually in-game, thus calling the confirm method of this script.
        /// </summary>
        public static XsollaStore instance { get; private set; }


        /// <summary>
        /// Setting this store reference on initialization.
        /// </summary>
        public XsollaStore()
        {
            instance = this;
        }

        /// <summary>
        /// Initialize the instance using the specified IStoreCallback.
        /// </summary>
        public void Initialize(IStoreCallback callback)
        {
            this.callback = callback;
        }


        /// <summary>
        /// Fetch the latest product metadata asynchronously with results returned via IStoreCallback.
        /// </summary>
        public void RetrieveProducts(ReadOnlyCollection<ProductDefinition> products)
        {
            this.products = new Dictionary<string, ProductDescription>();

            if(XsollaManager.GetInstance() == null && IAPManager.isDebug)
            {
                UnityEngine.Debug.LogWarning("XsollaStore: Missing XsollaManager in the scene. Unity IAP cannot complete initialization process.");
                return;
            }

            XsollaManager.GetInstance().StartCoroutine(WaitForCatalogCoroutine());
        }


        private IEnumerator WaitForCatalogCoroutine()
        {
            List<StoreItem> storeItems = new List<StoreItem>();
            bool hasCurrency = false;
            bool hasItems = false;
            bool hasBundles = false;

            Xsolla.Store.XsollaStore.Instance.GetVirtualCurrencyPackagesList(XsollaSettings.StoreProjectId, currencyResult =>
            {
                storeItems.AddRange(currencyResult.items);
                hasCurrency = true;
            },
            OnSetupFailed, int.MaxValue);

            Xsolla.Store.XsollaStore.Instance.GetCatalog(XsollaSettings.StoreProjectId, itemResult =>
            {
                storeItems.AddRange(itemResult.items);
                hasItems = true;
            },
            OnSetupFailed, int.MaxValue);

            Xsolla.Store.XsollaStore.Instance.GetBundles(XsollaSettings.StoreProjectId, bundleResult =>
            {
                for(int i = 0; i < bundleResult.items.Length; i++)
                {
                    storeItems.Add(new StoreItem()
                    {
                        sku = bundleResult.items[i].sku,
                        name = bundleResult.items[i].name,
                        description = bundleResult.items[i].description,
                        price = bundleResult.items[i].price,
                        virtual_prices = bundleResult.items[i].virtual_prices,
                    });
                }

                hasBundles = true;
            },
            OnSetupFailed, null, int.MaxValue);

            yield return new WaitUntil(() => hasCurrency && hasItems && hasBundles);
            OnCatalogRetrieved(storeItems);
        }


        //getting the items declared in Xsolla's catalog and converting them to Unity IAP format
        private void OnCatalogRetrieved(List<StoreItem> result)
        {
            catalog = result;
            string[] allIDs = IAPManager.GetAllIDs();

            for(int i = 0; i < result.Count; i++)
            {
                StoreItem catalogItem = result[i];

                string itemId = IAPManager.GetProductGlobalIdentifier(catalogItem.sku);
                if(!allIDs.Contains(itemId) || products.ContainsKey(itemId))
                    continue;
                
                decimal price = 0;
                string priceString = "";
                string currency = "USD";

                if (catalogItem.price != null)
                {
                    price = (decimal)catalogItem.price.GetAmount();
                    priceString = price.ToString("C");
                    currency = catalogItem.price.currency;
                }
                else if(catalogItem.virtual_prices.Length > 0)
                {
                    price = catalogItem.virtual_prices[0].GetAmount();
                    priceString = price.ToString();
                }

                ApplyCatalogItem(itemId, catalogItem, ref priceString);
                products.Add(itemId, new ProductDescription(catalogItem.sku, new ProductMetadata(priceString, catalogItem.name, catalogItem.description, currency, price)));
            }

            if (callback != null)
                callback.OnProductsRetrieved(products.Values.ToList());
        }


        /// <summary>
        /// Handle a purchase request from a user.
        /// Developer payload is provided for stores that define such a concept.
        /// </summary>
        public void Purchase(ProductDefinition productDefinition, string developerPayload)
        {
            IAPProduct product = IAPManager.GetIAPProduct(productDefinition.id);
            if(product.IsVirtual())
            {
                Purchase(product);
                return;
            }

            currentProduct = productDefinition.storeSpecificId;
            Xsolla.Store.XsollaStore.Instance.ItemPurchase(XsollaSettings.StoreProjectId, currentProduct, OnPurchaseStarted, OnPurchaseFailed);
        }


        /// <summary>
        /// Purchase overload for virtual products, as they differ in their workflow.
        /// Virtual currency funds should be checked locally before creating the request.
        /// </summary>
        public void Purchase(IAPProduct product)
        {
            Product storeProduct = IAPManager.controller.products.WithID(product.ID);
            IAPExchangeObject exchange = product.priceList.Find(x => x.type == IAPExchangeObject.ExchangeType.VirtualCurrency);
            if(storeProduct == null || exchange == null)
            {
                if (IAPManager.isDebug) UnityEngine.Debug.LogWarning("Product " + product.ID + " is not available on the store or has no virtual currency assigned for purchase exchange.");
                OnVirtualPurchaseFailed(new Error(ErrorType.ProductDoesNotExist, "", PurchaseFailureReason.ProductUnavailable.ToString(), ((int)ErrorType.ProductDoesNotExist).ToString()));
                return;
            }

            currentProduct = storeProduct.definition.storeSpecificId;
            Xsolla.Store.XsollaStore.Instance.ItemPurchaseForVirtualCurrency(XsollaSettings.StoreProjectId, storeProduct.definition.storeSpecificId, exchange.currency.ID, result => OnPurchaseSucceeded(result, product), OnVirtualPurchaseFailed);
        }


        /// <summary>
        /// Callback retrieved when an (real money) order on live servers has been initiated.
        /// Here the payment request for the order is being sent off, triggering native overlays.
        /// </summary>
        public void OnPurchaseStarted(PurchaseData result)
        {
            orderId = result.order_id;

            Xsolla.Store.XsollaStore.Instance.OpenPurchaseUi(result);
            OnPurchaseResult();
        }


        /// <summary>
        /// Method called when the payment result should be available.
        /// The purchase still needs to be acknowledged in this method.
        /// </summary>
        public void OnPurchaseResult()
        {
            if (UIShopFeedback.GetInstance() != null)
                UIShopFeedback.ShowConfirmation();

            Xsolla.Store.XsollaStore.Instance.AddOrderForTracking(XsollaSettings.StoreProjectId, orderId,
                () => ConfirmPurchase(),
                error => OnPurchaseFailed(error));
        }


        /// <summary>
        /// Automatic or manually triggering purchase confirmation after a payment has been made.
        /// This is so that the transaction gets finished and actually awarded in-game.
        /// </summary>
        public void ConfirmPurchase()
        {
            if (orderId <= 0)
                return;

            Xsolla.Store.XsollaStore.Instance.RemoveOrderFromTracking(orderId);
            Xsolla.Store.XsollaStore.Instance.CheckOrderStatus(XsollaSettings.StoreProjectId, orderId, result =>
            {
                if(result.status == "paid" || result.status == "done")
                {
                    if (UIShopFeedback.GetInstance() != null && UIShopFeedback.GetInstance().confirmWindow != null)
                        UIShopFeedback.GetInstance().confirmWindow.SetActive(false);

                    OnPurchaseSucceeded(result);
                }
            },
            error =>
            {
                OnPurchaseFailed(error);
            });
        }


        /// <summary>
        /// Callback from the service provider when a (real money) purchase completes successfully.
        /// </summary>
        public void OnPurchaseSucceeded(OrderStatus result)
        {
            //double check in case user confirmed manually too, to avoid duplicate rewards
            if (orderId <= 0)
                return;

            orderId = 0;

            foreach (OrderItem item in result.content.items)
            {
                if (item.is_free == "true")
                    continue;

                UpdateCustomData(null, item.sku);
                callback.OnPurchaseSucceeded(item.sku, System.DateTime.UtcNow.ToString(), result.order_id.ToString());
            }
        }


        /// <summary>
        /// Callback from the service provider when a (virtual) purchase completes successfully.
        /// </summary>
        public void OnPurchaseSucceeded(PurchaseData result, IAPProduct product)
        {
            //substract purchase price from the virtual currency locally
            //this is only for display purposes, as the funds are maintained on the server
            DBManager.PurchaseVirtual(product);

            //finish transaction using default workflow. Since there is no additional receipt validation when using
            //Xsolla, this just calls IAPManager.CompletePurchase and FinishTransaction below
            UpdateCustomData(product, currentProduct);
            callback.OnPurchaseSucceeded(product.ID, System.DateTime.UtcNow.ToString(), result.order_id.ToString());
        }


        private void UpdateCustomData(IAPProduct product, string storeID)
        {
            if (product == null) product = IAPManager.GetIAPProduct(storeID);
            if (product == null) return;

            switch (product.type)
            {
                case ProductType.Subscription:
                    StoreItem item = catalog.Find(x => x.sku == storeID);
                    if (item == null || item.inventory_options == null || item.inventory_options.expiration_period == null) break;

                    DateTime exDate = product.customData.Keys.Contains("expiration") ? DateTime.Parse(product.customData["expiration"]) : DateTime.Now;
                    exDate += item.inventory_options.expiration_period.ToTimeSpan();
                    product.customData.Remove("expiration");
                    product.customData.Add("expiration", exDate.ToString("u"));
                    break;
            }
        }


        /// <summary>
        ///
        /// </summary>
        public void Consume(IAPProduct product, int amount)
        {
            Product storeProduct = IAPManager.controller.products.WithID(product.ID);

            if (storeProduct == null)
            {
                if (IAPManager.isDebug) UnityEngine.Debug.LogWarning("Product " + product.ID + " is not available on the store.");
                OnConsumeFailed(new Error(ErrorType.ProductDoesNotExist, "", PurchaseFailureReason.ProductUnavailable.ToString(), ((int)ErrorType.ProductDoesNotExist).ToString()));
                return;
            }

            Xsolla.Store.XsollaStore.Instance.ConsumeInventoryItem(XsollaSettings.StoreProjectId, new ConsumeItem() { sku = storeProduct.definition.storeSpecificId, quantity = amount }, () => OnConsumeSucceeded(product, amount), OnConsumeFailed);
        }


        /// <summary>
        /// Callback from the service provider when a consume action completes successfully.
        /// </summary>
        public void OnConsumeSucceeded(IAPProduct product, int amount)
        {
            //consume purchase amount from the user inventory locally
            //this is only for display purposes, as the inventory is maintained on the server
            IAPManager.GetInstance().CompleteConsume(product.ID, amount);
        }


        /// <summary>
        /// 
        /// </summary>
        public void RedeemCoupon(string code)
        {
            Xsolla.Store.XsollaStore.Instance.RedeemCouponCode(XsollaSettings.StoreProjectId, new CouponCode() { coupon_code = code }, result =>
            {
                List<KeyValuePairStringInt> items = new List<KeyValuePairStringInt>();
                foreach (CouponRedeemedItem item in result.items)
                {
                    //we do not have a receipt or any funds to substract in order to call OnPurchaseSucceeded
                    //instead we grant the item directly ourselves
                    IAPManager.GetInstance().CompletePurchase(item.sku);

                    items.Add(new KeyValuePairStringInt() { Key = IAPManager.GetProductGlobalIdentifier(item.sku), Value = item.quantity });
                }

                if (UIShopFeedback.GetInstance() != null && UIShopFeedback.GetInstance().previewWindow != null)
                {
                    //hide other single purchase feedback windows when showing the current awarded content
                    if (UIShopFeedback.GetInstance().errorWindow != null)
                        UIShopFeedback.GetInstance().errorWindow.SetActive(false);

                    UIShopFeedback.ShowRewardPreview(items);
                }

            }, error =>
            {
                //overwriting with own callback since we do not know the product rewards yet
                IAPManager.OnPurchaseFailed("Invalid request or coupon code.");
            });
        }


        /// <summary>
        /// Called by Unity Purchasing when a transaction has been recorded.
        /// Store systems should perform any housekeeping here, such as closing transactions or consuming consumables.
        /// </summary>
        public void FinishTransaction(ProductDefinition product, string transactionId)
        {
            //nothing to do here!
            //consumables are consumed manually when needed
        }


        /// <summary>
        /// Indicate that IAP is unavailable for a specific reason, such as IAP being disabled in device settings.
        /// </summary>
        public void OnSetupFailed(Error error)
        {
            callback.OnSetupFailed(InitializationFailureReason.NoProductsAvailable);
        }


        /// <summary>
        /// Method we are calling for any failed (real money) results in the billing interaction.
        /// </summary>
        public void OnPurchaseFailed(Error error)
        {
            callback.OnPurchaseFailed(new PurchaseFailureDescription(currentProduct, PurchaseFailureReason.PaymentDeclined, ""));
        }
        
        
        /// <summary>
        /// Method we are calling for any failed (virtual) results in the billing interaction.
        /// </summary>
        public void OnVirtualPurchaseFailed(Error error)
        {
            IAPManager.OnPurchaseFailed("Error: " + error.errorCode + ", " + error.errorMessage);
        }


        /// <summary>
        /// Method we are calling for any failed consume results in the billing interaction.
        /// </summary>
        public void OnConsumeFailed(Error error)
        {
            IAPManager.OnConsumeFailed("Error: " + error.errorCode + ", " + error.errorMessage);
        }


        //method for remote catalog config. Converts a (downloaded) config string for products into JSON nodes
        //and overwrites existing IAP products with new properties, after doing a null reference check for empty nodes.
        private void ApplyCatalogItem(string id, StoreItem item, ref string priceString)
        {
            IAPProduct product = IAPManager.GetIAPProduct(id);
            if(!product.fetch) return;

            List<VirtualCurrencyPackage.Content> content = new List<VirtualCurrencyPackage.Content>();
            //if (item is BundleItem) content = (item as BundleItem).content.ToList();
            //BundleItems not supported yet since they do not derive from StoreItem
            if (item is VirtualCurrencyPackage) content = (item as VirtualCurrencyPackage).content;

            //overwrite currency to grant
            foreach (VirtualCurrencyPackage.Content c in content)
            {
                IAPExchangeObject ex = product.rewardList.Find(x => x.type == IAPExchangeObject.ExchangeType.VirtualCurrency && x.currency.ID == c.sku);
                if (ex != null) ex.amount = c.quantity;
            }

            //overwrite currency prices
            foreach (VirtualPrice price in item.virtual_prices)
            {
                IAPExchangeObject ex = product.priceList.Find(x => x.type == IAPExchangeObject.ExchangeType.VirtualCurrency && x.currency.ID == price.sku);
                if (ex != null)
                {
                    if (price.GetAmount() < price.GetAmountWithoutDiscount())
                    {
                        ex.realPrice = "<size=10><i>(" + price.amount_without_discount + ")</i></size> " + price.amount;
                        product.discount = true;
                    }

                    ex.amount = (int)price.GetAmount();
                }
            }

            //overwrite real price
            if(item.price != null)
            {
                IAPExchangeObject ex = product.priceList.Find(x => x.type == IAPExchangeObject.ExchangeType.RealMoney);
                if (ex != null)
                {
                    if (item.price.GetAmount() < item.price.GetAmountWithoutDiscount())
                    {
                        priceString = "<size=10><i>(" + item.price.amount_without_discount + ")</i></size> " + priceString;
                        product.discount = true;
                    }

                    ex.realPrice = priceString;
                }
            }

            if (!string.IsNullOrEmpty(item.name)) product.title = item.name;
            if(!string.IsNullOrEmpty(item.description)) product.description = item.description;
            
            //custom data for requirements is not supported on Xsolla
        }
    }
    #endif
}