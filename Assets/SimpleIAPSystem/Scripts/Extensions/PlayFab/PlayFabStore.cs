/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

#if PLAYFAB_PAYPAL || PLAYFAB_STEAM
#define PLAYFAB
#endif

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

#if PLAYFAB
using PlayFab;
using PlayFab.ClientModels;
#endif

namespace SIS
{
    #if SIS_IAP && !PLAYFAB
    public class PlayFabStore : IStore
    {
        public static PlayFabStore instance { get; private set; }
        public void FinishTransaction(ProductDefinition product, string transactionId) { throw new System.NotImplementedException(); }
        public void Initialize(IStoreCallback callback) { throw new System.NotImplementedException(); }
        public void Purchase(ProductDefinition product, string developerPayload) { throw new System.NotImplementedException(); }
        public void RetrieveProducts(ReadOnlyCollection<ProductDefinition> products) { throw new System.NotImplementedException(); }
    }
    #endif

    #if SIS_IAP && PLAYFAB
    /// <summary>
    /// Represents the public interface of the underlying store system for PlayFab.
    /// This is the store base class other PlayFab billing implementations are making use of.
    /// </summary>
    public class PlayFabStore : IStore
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
        /// Declaring the store name used in Unity IAP for product store identifiers.
        /// </summary>
        public string storeId = "StoreName";

        /// <summary>
        /// Keeping track of the order that is currently being processed,
        /// so we can confirm and finish it later on.
        /// </summary>
        public static string orderId;

        //product catalog that has been retrieved from PlayFab
        private static List<CatalogItem> catalog;
        //keep track of UserInventory item instances or newly purchased for consumption later
        private Dictionary<string, string> itemInstances;
        //keeping track of the product that is currently being processed
        private string currentProduct;


        /// <summary>
        /// Reference to this store class, since the user needs to confirm the purchase
        /// transaction manually in-game, thus calling the confirm method of this script.
        /// </summary>
        public static PlayFabStore instance;


        /// <summary>
        /// Setting this store reference on initialization.
        /// </summary>
        public PlayFabStore()
        {
            instance = this;
        }


        /// <summary>
        /// Initialize the instance using the specified IStoreCallback.
        /// </summary>
        public virtual void Initialize(IStoreCallback callback)
        {
            this.callback = callback;
        }


        /// <summary>
        /// Fetch the latest product metadata asynchronously with results returned via IStoreCallback.
        /// </summary>
        public virtual void RetrieveProducts(ReadOnlyCollection<ProductDefinition> products)
        {
            this.products = new Dictionary<string, ProductDescription>();

            PlayFabClientAPI.GetCatalogItems(new GetCatalogItemsRequest(), OnCatalogRetrieved, OnSetupFailed);
        }


        //getting the items declared in PlayFab's catalog and converting them to Unity IAP format
        private void OnCatalogRetrieved(GetCatalogItemsResult result)
        {
            catalog = result.Catalog;
            string[] allIDs = IAPManager.GetAllIDs();

            for(int i = 0; i < catalog.Count; i++)
            {
                CatalogItem catalogItem = catalog[i];

                string itemId = IAPManager.GetProductGlobalIdentifier(catalogItem.ItemId);
                if(!allIDs.Contains(itemId) || products.ContainsKey(itemId))
                    continue;
                
                decimal price = 0;
                string priceString = "";
                string currency = "";
                if(catalogItem.VirtualCurrencyPrices.Count > 0)
                   currency = catalogItem.VirtualCurrencyPrices.Keys.First();

                if (currency == "RM")
                {
                    price = (decimal)catalogItem.VirtualCurrencyPrices[currency] / 100m;
                    priceString = price.ToString("C");
                }
                else if (!string.IsNullOrEmpty(currency))
                {
                    price = catalogItem.VirtualCurrencyPrices[currency];
                    priceString = price.ToString();
                }

                ApplyCatalogItem(itemId, catalogItem, ref priceString);
                products.Add(itemId, new ProductDescription(catalogItem.ItemId, new ProductMetadata(priceString, catalogItem.DisplayName, catalogItem.Description, "USD", price)));
            }

            if(callback != null)
                callback.OnProductsRetrieved(products.Values.ToList());
        }


        /// <summary>
        /// Handle a purchase request from a user.
        /// Developer payload is provided for stores that define such a concept.
        /// </summary>
        public virtual void Purchase(ProductDefinition productDefinition, string developerPayload)
        {
            IAPProduct product = IAPManager.GetIAPProduct(productDefinition.id);
            if(product.IsVirtual())
            {
                Purchase(product);
                return;
            }

            StartPurchaseRequest request = new StartPurchaseRequest()
            {
                Items = new List<ItemPurchaseRequest>()
                {
                    new ItemPurchaseRequest() { ItemId = productDefinition.id, Quantity = 1 }
                }
            };

            currentProduct = productDefinition.id;
            PlayFabClientAPI.StartPurchase(request, OnPurchaseStarted, OnPurchaseFailed);
        }


        /// <summary>
        /// Purchase overload for virtual products, as they differ in their workflow.
        /// Virtual currency funds should be checked locally before creating the request.
        /// </summary>
        public void Purchase(IAPProduct product)
        {
            Product storeProduct = IAPManager.controller.products.WithID(product.ID);
            IAPExchangeObject exchange = product.priceList.Find(x => x.type == IAPExchangeObject.ExchangeType.VirtualCurrency);

            if (storeProduct == null || exchange == null)
            {
                if (IAPManager.isDebug) Debug.LogWarning("Product " + product.ID + " is not available on the store or has no virtual currency assigned for purchase exchange.");
                OnVirtualPurchaseFailed(new PlayFabError() { Error = PlayFabErrorCode.ItemNotFound, ErrorMessage = PurchaseFailureReason.ProductUnavailable.ToString() });
                return;
            }

            PurchaseItemRequest request = new PurchaseItemRequest()
            {
                ItemId = product.ID,
                VirtualCurrency = exchange.currency.ID.Substring(0, 2).ToUpper(),
                Price = exchange.amount    
            };

            currentProduct = storeProduct.definition.storeSpecificId;
            PlayFabClientAPI.PurchaseItem(request, OnPurchaseSucceeded, OnVirtualPurchaseFailed);
        }


        /// <summary>
        /// Callback retrieved when an (real money) order on live servers has been initiated.
        /// Here the payment request for the order is being sent off, triggering native overlays.
        /// </summary>
        public virtual void OnPurchaseStarted(StartPurchaseResult result)
        {
            orderId = result.OrderId;
            currentProduct = result.Contents[0].ItemId;

            PayForPurchaseRequest request = new PayForPurchaseRequest()
            {
                OrderId = orderId,
                ProviderName = storeId,
                Currency = "RM"
            };

            PlayFabClientAPI.PayForPurchase(request, OnPurchaseResult, OnPurchaseFailed);
        }


        /// <summary>
        /// Callback retrieved when the payment result is received from live servers.
        /// The purchase still needs to be acknowledged in this method.
        /// </summary>
        public virtual void OnPurchaseResult(PayForPurchaseResult result)
        {
            ConfirmPurchaseRequest request = new ConfirmPurchaseRequest()
            {
                OrderId = orderId
            };

            PlayFabClientAPI.ConfirmPurchase(request, OnPurchaseSucceeded, OnPurchaseFailed);
        }


        /// <summary>
        /// Callback from the billing system when a (real money) purchase completes successfully.
        /// </summary>
        public void OnPurchaseSucceeded(ConfirmPurchaseResult result)
        {
            orderId = string.Empty;
            ItemInstance item = result.Items[0];
            IAPProduct product = IAPManager.GetIAPProduct(item.ItemId);
            PlayFabManager.GetInstance().AddItemInstanceID(product.ID, item.ItemInstanceId);
            UpdateCustomData(null, item.ItemId);

            if(callback != null) callback.OnPurchaseSucceeded(item.ItemId, item.PurchaseDate.ToString(), item.ItemInstanceId);
            else IAPManager.GetInstance().CompletePurchase(product.ID);
        }


        /// <summary>
        /// Callback from the billing system when a (virtual) purchase completes successfully.
        /// </summary>
        public void OnPurchaseSucceeded(PurchaseItemResult result)
        {
            orderId = string.Empty;
            ItemInstance item = result.Items[0];
            IAPProduct product = IAPManager.GetIAPProduct(item.ItemId);
            PlayFabManager.GetInstance().AddItemInstanceID(product.ID, item.ItemInstanceId);

            //substract purchase price from the virtual currency locally
            //this is only for display purposes, as the funds are maintained on the server
            if (product != null)
                DBManager.PurchaseVirtual(product);

            //can't call the native callback because PlayFab returns the same ItemInstanceId for stackable products
            //this would result in an Unity IAP 'Already recorded transaction' message thus not doing anything
            //callback.OnPurchaseSucceeded(item.ItemId, item.PurchaseDate.ToString(), item.ItemInstanceId);

            //instead we call the finish events ourselves
            UpdateCustomData(product, currentProduct);
            IAPManager.GetInstance().CompletePurchase(item.ItemId);
            FinishTransaction(IAPManager.controller.products.WithID(item.ItemId).definition, item.ItemInstanceId);
        }


        private void UpdateCustomData(IAPProduct product, string storeID)
        {
            if (product == null) product = IAPManager.GetIAPProduct(storeID);
            if (product == null) return;

            switch (product.type)
            {
                case ProductType.Subscription:
                    CatalogItem item = catalog.Find(x => x.ItemId == storeID);
                    if (item == null || item.Consumable == null || item.Consumable.UsagePeriod == null) break;

                    DateTime exDate = product.customData.Keys.Contains("expiration") ? DateTime.Parse(product.customData["expiration"]) : DateTime.Now;
                    exDate = exDate.AddSeconds(item.Consumable.UsagePeriod.Value);
                    product.customData.Remove("expiration");
                    product.customData.Add("expiration", exDate.ToString("u"));
                    break;
            }
        }


        /// <summary>
        ///
        /// </summary>
        public void Consume(IAPProduct product, string itemInstanceID, int amount)
        {
            Product storeProduct = IAPManager.controller.products.WithID(product.ID);

            if (storeProduct == null)
            {
                if (IAPManager.isDebug) Debug.LogWarning("Product " + product.ID + " is not available on the store.");
                OnConsumeFailed(new PlayFabError() { Error = PlayFabErrorCode.ItemNotFound, ErrorMessage = PurchaseFailureReason.ProductUnavailable.ToString() });
                return;
            }

            ConsumeItemRequest request = new ConsumeItemRequest()
            {
                ItemInstanceId = itemInstanceID,
                ConsumeCount = amount
            };

            PlayFabClientAPI.ConsumeItem(request, (result) => OnConsumeSucceeded(product, amount), OnConsumeFailed);
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
            PlayFabClientAPI.RedeemCoupon(new RedeemCouponRequest() { CouponCode = code }, result =>
            {
                List<KeyValuePairStringInt> items = new List<KeyValuePairStringInt>();
                foreach (ItemInstance item in result.GrantedItems)
                {
                    //we do not have a receipt or any funds to substract in order to call OnPurchaseSucceeded
                    //instead we grant the item directly ourselves
                    IAPManager.GetInstance().CompletePurchase(item.ItemId);

                    items.Add(new KeyValuePairStringInt() { Key = IAPManager.GetProductGlobalIdentifier(item.ItemId), Value = item.UsesIncrementedBy.HasValue ? item.UsesIncrementedBy.Value : 1 });
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
                IAPManager.OnPurchaseFailed("Invalid request or coupon code.\n" + error.ErrorMessage);
            });
        }


        /// <summary>
        /// Called by Unity Purchasing when a transaction has been recorded.
        /// Store systems should perform any housekeeping here, such as closing transactions or consuming consumables.
        /// </summary>
        public virtual void FinishTransaction(ProductDefinition product, string transactionId)
        {
            //nothing to do here!
            //consumables are consumed manually or automatically on PlayFab's side after x seconds.
        }


        /// <summary>
        /// Indicate that IAP is unavailable for a specific reason, such as IAP being disabled in device settings.
        /// </summary>
        public void OnSetupFailed(PlayFabError error)
        {
            callback.OnSetupFailed(InitializationFailureReason.NoProductsAvailable);
        }


        /// <summary>
        /// Method we are calling for any failed (real money) results in the billing interaction.
        /// </summary>
        public void OnPurchaseFailed(PlayFabError error)
        {
            callback.OnPurchaseFailed(new PurchaseFailureDescription(currentProduct, PurchaseFailureReason.PaymentDeclined, error.GenerateErrorReport()));
        }
        
        
        /// <summary>
        /// Method we are calling for any failed (virtual) results in the billing interaction.
        /// </summary>
        public void OnVirtualPurchaseFailed(PlayFabError error)
        {
            IAPManager.OnPurchaseFailed("Error: " + (int)error.Error + ", " + error.ErrorMessage);
        }


        /// <summary>
        /// Method we are calling for any failed consume results in the billing interaction.
        /// </summary>
        public void OnConsumeFailed(PlayFabError error)
        {
            IAPManager.OnConsumeFailed("Error: " + (int)error.Error + ", " + error.ErrorMessage);
        }


        //method for remote catalog config
        //converts a (downloaded) config string for virtual products into JSON nodes and overwrites
        //existing IAP products with new properties, after doing a null reference check for empty nodes.
        private void ApplyCatalogItem(string id, CatalogItem item, ref string priceString)
        {
            IAPProduct product = IAPManager.GetIAPProduct(id);
            if(!product.fetch) return;

            //overwrite currency to grant
            if (item.Bundle != null && item.Bundle.BundledVirtualCurrencies != null && item.Bundle.BundledVirtualCurrencies.Count > 0)
            {
                foreach (string curKey in item.Bundle.BundledVirtualCurrencies.Keys)
                {
                    IAPExchangeObject ex = product.rewardList.Find(x => x.type == IAPExchangeObject.ExchangeType.VirtualCurrency && x.currency.ID.StartsWith(curKey, StringComparison.OrdinalIgnoreCase));
                    if (ex != null) ex.amount = (int)item.Bundle.BundledVirtualCurrencies[curKey];
                }
            }

            //overwrite currency prices
            if (item.VirtualCurrencyPrices != null && item.VirtualCurrencyPrices.Count > 0)
            {
                foreach (string curKey in item.VirtualCurrencyPrices.Keys)
                {
                    IAPExchangeObject ex = product.priceList.Find(x => x.type == IAPExchangeObject.ExchangeType.VirtualCurrency && x.currency.ID.StartsWith(curKey, StringComparison.OrdinalIgnoreCase));
                    if (ex != null)
                    {
                        int livePrice = (int)item.VirtualCurrencyPrices[curKey];
                        if (livePrice < ex.amount)
                        {
                            ex.realPrice = "<size=10><i>(" + ex.amount + ")</i></size> " + livePrice;
                            product.discount = true;
                        }

                        ex.amount = livePrice;
                    }
                }
            }

            //overwrite real price
            if(item.VirtualCurrencyPrices != null && item.VirtualCurrencyPrices.ContainsKey("RM"))
            {
                IAPExchangeObject ex = product.priceList.Find(x => x.type == IAPExchangeObject.ExchangeType.RealMoney);
                if(ex != null)
                {
                    uint livePrice = item.VirtualCurrencyPrices["RM"];
                    string price = new string(ex.realPrice.Where(c => "01234567890.,".Contains(c)).ToArray());
                    price = price.Replace('.', ',');
                                       
                    uint currentPrice = 0;
                    if (uint.TryParse(price, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out currentPrice))
                    {
                        //price exists and could be converted to uint for comparison
                        if(livePrice < currentPrice)
                        {
                            priceString = "<size=10><i>(" + price + ")</i></size> " + "$" + (livePrice / 100f);
                            product.discount = true;                            
                        }

                        ex.realPrice = priceString;
                    }
                }
            }

            //only fetch other details from platforms that do not provide this
            #if PLAYFAB_STEAM || PLAYFAB_PAYPAL
            if(!string.IsNullOrEmpty(item.DisplayName)) product.title = item.DisplayName;
            if(!string.IsNullOrEmpty(item.Description)) product.description = item.Description;
            #endif
            
            if(!string.IsNullOrEmpty(item.CustomData))
            {
                JSONNode data = JSON.Parse(item.CustomData);

                if(!string.IsNullOrEmpty(data["requirement"].ToString()))
                {
                    if (!string.IsNullOrEmpty(data["requirement"]["labelText"]))
                        product.requirement.label = data["requirement"]["labelText"];

                    if (!string.IsNullOrEmpty(data["requirement"]["pairs"].ToString()))
                    {
                        product.requirement.pairs.Clear();
                        JSONArray array = data["requirement"]["pairs"].AsArray;
                        for (int i = 0; i < array.Count; i++)
                            product.requirement.pairs.Add(new KeyValuePairStringInt() { Key = array[i]["key"], Value = array[i]["value"].AsInt });
                    }
                }
            }
        }
    }
    #endif
}