/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 *  You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 *  otherwise make available to any third party the Service or the Content. */

using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using SIS.SimpleJSON;

#if SIS_IAP
using UnityEngine.Purchasing;
#endif

namespace SIS
{
    public class IAPSettingsExporter
    {
        public static List<IAPCurrency> FromJSONCurrency(string text)
        {
            IAPScriptableObject asset = IAPScriptableObject.GetOrCreateSettings();
            List <IAPCurrency> currencies = new List<IAPCurrency>();
            JSONNode data = JSON.Parse(text);
            
            for (int i = 0; i < data.Count; i++)
            {
                IAPCurrency cur = asset.currencyList.Find(x => x.ID == data[i]["DisplayName"].Value);
                if (cur == null) cur = currencies.Find(x => x.ID == data[i]["DisplayName"].Value);
                if (cur == null) cur = new IAPCurrency();

                if (!string.IsNullOrEmpty(data[i]["Ref"].Value)) cur.referenceID = data[i]["Ref"].Value;
                else cur.referenceID = System.Guid.NewGuid().ToString("D");

                cur.ID = data[i]["DisplayName"].Value;
                cur.baseAmount = data[i]["InitialDeposit"].AsInt;
                cur.maxAmount = data[i]["MaximumDeposit"].AsInt;
                currencies.Add(cur);
            }

            return currencies.Distinct().ToList();
        }


        public static List<IAPCategory> FromJSONCategory(string text)
        {
            IAPScriptableObject asset = IAPScriptableObject.GetOrCreateSettings();
            List<IAPCategory> categories = new List<IAPCategory>();
            JSONNode data = JSON.Parse(text)["Catalog"];

            for (int i = 0; i < data.Count; i++)
            {
                IAPCategory cat = asset.categoryList.Find(x => x.ID == data[i]["SIS"]["Group"]["Name"].Value);
                if (cat == null) cat = categories.Find(x => x.ID == data[i]["SIS"]["Group"]["Name"].Value);
                if (cat == null) cat = new IAPCategory();

                if (!string.IsNullOrEmpty(data[i]["SIS"]["Group"]["Ref"].Value)) cat.referenceID = data[i]["SIS"]["Group"]["Ref"].Value;
                else cat.referenceID = System.Guid.NewGuid().ToString("D");

                cat.ID = data[i]["SIS"]["Group"]["Name"].Value;
                categories.Add(cat);
            }

            return categories.Distinct().ToList();
        }


        public static List<IAPProduct> FromJSONProduct(string text)
        {
            IAPScriptableObject asset = IAPScriptableObject.GetOrCreateSettings();
            List<IAPProduct> products = new List<IAPProduct>();
            JSONNode data = JSON.Parse(text)["Catalog"];

            for (int i = 0; i < data.Count; i++)
            {
                IAPProduct product = asset.productList.Find(x => x.ID == data[i]["ItemId"].Value);
                if (product == null) product = new IAPProduct();

                if (!string.IsNullOrEmpty(data[i]["SIS"]["Ref"].Value)) product.referenceID = data[i]["SIS"]["Ref"].Value;
                else product.referenceID = System.Guid.NewGuid().ToString("D");

                product.ID = data[i]["ItemId"].Value;
                product.title = data[i]["DisplayName"].Value;
                product.description = data[i]["Description"].Value;
                product.type = (ProductType)data[i]["SIS"]["Type"].AsInt;
                product.fetch = data[i]["SIS"]["Fetch"].AsBool;
                product.icon = (Sprite)AssetDatabase.LoadAssetAtPath(data[i]["SIS"]["Icon"].Value, typeof(Sprite));

                product.priceList.Clear();
                if (!string.IsNullOrEmpty(data[i]["SIS"]["Price"].Value))
                {
                    product.priceList.Add(new IAPExchangeObject()
                    {
                        type = IAPExchangeObject.ExchangeType.RealMoney,
                        realPrice = data[i]["SIS"]["Price"].Value.Replace(',', '.')
                    });
                }

                if (!string.IsNullOrEmpty(data[i]["VirtualCurrencyPrices"].ToString()))
                {
                    foreach (string key in data[i]["VirtualCurrencyPrices"].AsObject.Keys)
                    {
                        IAPCurrency cur = asset.currencyList.Find(x => x.ID.StartsWith(key, System.StringComparison.OrdinalIgnoreCase));
                        if (cur != null)
                        {
                            product.priceList.Add(new IAPExchangeObject()
                            {
                                type = IAPExchangeObject.ExchangeType.VirtualCurrency,
                                amount = data[i]["VirtualCurrencyPrices"][key].AsInt,
                                currency = cur
                            });
                        }
                    }
                }

                product.rewardList.Clear();
                if (!string.IsNullOrEmpty(data[i]["Bundle"]["BundledVirtualCurrencies"].ToString()))
                {
                    foreach (string key in data[i]["Bundle"]["BundledVirtualCurrencies"].AsObject.Keys)
                    {
                        IAPCurrency cur = asset.currencyList.Find(x => x.ID.StartsWith(key, System.StringComparison.OrdinalIgnoreCase));
                        if(cur != null)
                        {
                            product.rewardList.Add(new IAPExchangeObject()
                            {
                                type = IAPExchangeObject.ExchangeType.VirtualCurrency,
                                amount = data[i]["Bundle"]["BundledVirtualCurrencies"][key].AsInt,
                                currency = cur
                            });
                        }
                    }
                }

                product.storeIDs.Clear();
                if (!product.IsVirtual())
                {
                    //old format
                    int platformCount = System.Enum.GetValues(typeof(IAPPlatform)).Length;
                    for (int j = 0; j < platformCount; j++)
                    {
                        if (string.IsNullOrEmpty(data[i]["SIS"]["PlatformId"][j.ToString()].Value))
                            continue;

                        product.storeIDs.Add(new StoreMetaDefinition()
                        {
                            active = true,
                            store = ((IAPPlatform)j).ToString(),
                            ID = data[i]["SIS"]["PlatformId"][j.ToString()].Value
                        });
                    }

                    //new format
                    if (!string.IsNullOrEmpty(data[i]["SIS"]["Overrides"]["Ids"].ToString()))
                    {
                        JSONArray platformIDs = data[i]["SIS"]["Overrides"]["Ids"].AsArray;
                        for (int j = 0; j < platformIDs.Count; j++)
                        {
                            product.storeIDs.Add(new StoreMetaDefinition()
                            {
                                active = platformIDs[j]["Active"].AsBool,
                                store = platformIDs[j]["Platform"].Value,
                                ID = platformIDs[j]["Id"].Value
                            });
                        }
                    }
                }

                JSONNode customBilling = data[i]["SIS"]["Overrides"]["Billing"];
                if(customBilling.ToString() == string.Empty)
                {
                    product.customBilling.active = false;
                }
                else
                {
                    product.customBilling.active = customBilling["Active"].AsBool;
                    product.customBilling.provider = (BillingProvider)System.Enum.Parse(typeof(BillingProvider), customBilling["Provider"].Value);
                }               

                IAPCategory cat = asset.categoryList.Find(x => x.ID == data[i]["SIS"]["Group"]["Name"].Value);
                if (cat != null)
                    product.category = cat;

                products.Add(product);
            }

            //second loop for cross linked products only, since now with all products existing already,
            //the product relationships from the product price/reward and requirement/upgrade links can be connected properly
            for (int i = 0; i < data.Count; i++)
            {
                IAPProduct product = products.Find(x => x.ID == data[i]["ItemId"].Value);

                if (!string.IsNullOrEmpty(data[i]["SIS"]["VirtualItemPrices"].ToString()))
                {
                    JSONArray productPrices = data[i]["SIS"]["VirtualItemPrices"].AsArray;

                    for(int j = 0; j < productPrices.Count; j++)
                    {
                        IAPProduct p = products.Find(x => x.ID == productPrices[j]["ItemId"].Value);
                        if (p == null) continue;

                        product.priceList.Add(new IAPExchangeObject()
                        {
                            type = IAPExchangeObject.ExchangeType.VirtualProduct,
                            amount = productPrices[j]["Amount"].AsInt,
                            product = p
                        });
                    }
                }

                //PlayFab BundledItems string enumeration
                if (!string.IsNullOrEmpty(data[i]["Bundle"]["BundledItems"].Value))
                {
                    Dictionary<string, int> bundledItems = new Dictionary<string, int>();
                    string[] prods = data[i]["Bundle"]["BundledItems"].Value.Split(',');

                    for (int j = 0; j < prods.Length; j++)
                    {
                        IAPProduct p = products.Find(x => x.ID == prods[j]);
                        if (p == null) continue;

                        if (bundledItems.ContainsKey(p.ID)) bundledItems[p.ID] += 1;
                        else bundledItems.Add(p.ID, 1);
                    }

                    foreach (string key in bundledItems.Keys)
                    {
                        if (product.rewardList.Exists(x => x.product != null && x.product.ID == key))
                            continue;

                        product.rewardList.Add(new IAPExchangeObject()
                        {
                            type = IAPExchangeObject.ExchangeType.VirtualProduct,
                            amount = bundledItems[key],
                            product = products.Find(x => x.ID == key)
                        });
                    }
                }

                //Non-PlayFab, ItemId+Amount
                if (!string.IsNullOrEmpty(data[i]["Bundle"]["BundledItems"].ToString()))
                {
                    JSONArray bundledItems = data[i]["Bundle"]["BundledItems"].AsArray;
                    for(int j = 0; j < bundledItems.Count; j++)
                    {
                        product.rewardList.Add(new IAPExchangeObject()
                        {
                            type = IAPExchangeObject.ExchangeType.VirtualProduct,
                            amount = bundledItems[j]["Amount"].AsInt,
                            product = products.Find(x => x.ID == bundledItems[j]["ItemId"].Value)
                        });
                    }
                }

                //auto add reward itself for non-consumables
                if (product.type != ProductType.Consumable && !product.rewardList.Exists(x => x.product != null && x.product.ID == product.ID))
                {
                    product.rewardList.Add(new IAPExchangeObject()
                    {
                        type = IAPExchangeObject.ExchangeType.VirtualProduct,
                        amount = 1,
                        product = product
                    });

                    Debug.Log("IAPSettingsImporter: added product " + product.ID + " to itself as a reward, please check. Non-Consumable products should always " +
                              "be granted to the user as a best practice. If you used a JSON from an older version, this message is normal and you can ignore it.");
                }

                if (!string.IsNullOrEmpty(data[i]["SIS"]["Requirement"].ToString()))
                {
                    product.requirement.pairs.Clear();

                    //v5+
                    if(!string.IsNullOrEmpty(data[i]["SIS"]["Requirement"]["Pairs"].ToString()))
                    {
                        JSONArray pairs = data[i]["SIS"]["Requirement"]["Pairs"].AsArray;
                        for(int j = 0; j < pairs.Count; j++)
                        {
                            product.requirement.pairs.Add(new KeyValuePairStringInt()
                            {
                                Key = pairs[j]["Id"].Value,
                                Value = pairs[j]["Value"].AsInt
                            });
                        }
                    }
                    //old format
                    else if (!string.IsNullOrEmpty(data[i]["SIS"]["Requirement"]["Id"]))
                    {
                        product.requirement.pairs.Add(new KeyValuePairStringInt()
                        {
                            Key = data[i]["SIS"]["Requirement"]["Id"].Value,
                            Value = data[i]["SIS"]["Requirement"]["Value"].AsInt
                        });
                    }

                    product.requirement.label = data[i]["SIS"]["Requirement"]["Text"].Value;

                    if (!string.IsNullOrEmpty(data[i]["SIS"]["Requirement"]["Next"].Value))
                    {
                        IAPProduct p = products.Find(x => x.ID == data[i]["SIS"]["Requirement"]["Next"].Value);
                        if(p != null) product.nextUpgrade = p;
                    }
                }
            }

            return products.Distinct().ToList();
        }


        public static string ToJSON(List<IAPCurrency> currency)
        {
            JSONNode data = new JSONClass();
            JSONArray curArray = new JSONArray();
            
            for (int i = 0; i < currency.Count; i++)
            {
                IAPCurrency cur = currency[i];

                curArray[i]["Ref"] = new JSONData(cur.referenceID);
                curArray[i]["CurrencyCode"] = new JSONData(cur.ID.Substring(0, 2).ToUpper());
                curArray[i]["DisplayName"] = new JSONData(cur.ID);
                curArray[i]["InitialDeposit"] = new JSONData(cur.baseAmount);
                curArray[i]["MaxDeposit"] = new JSONData(cur.maxAmount);
            }
            
            data = curArray;
            return data.ToString();
        }
            

        public static string ToJSON(List<IAPProduct> productList, bool playfabVersion = false)
        {
            JSONNode data = new JSONClass();
            data["CatalogVersion"] = new JSONData(1);
            JSONArray itemArray = new JSONArray();

            for (int i = 0; i < productList.Count; i++)
            {
                IAPProduct product = productList[i];
                JSONNode node = new JSONClass();
                node["CatalogVersion"] = new JSONData(1);
				node["ItemId"] = new JSONData(product.ID);
				if(!string.IsNullOrEmpty(product.title)) node["DisplayName"] = new JSONData(product.title);
                if(!string.IsNullOrEmpty(product.description)) node["Description"] = new JSONData(product.description);

                switch (product.type)
                {
                    case ProductType.Consumable:
                        node["Consumable"]["UsagePeriod"] = new JSONData(5);
                        if (product.IsVirtual() || (!product.IsVirtual() && !product.rewardList.Exists(x => x.type == IAPExchangeObject.ExchangeType.VirtualCurrency)))
                            node["IsStackable"] = new JSONData(true);
                        break;

                    case ProductType.Subscription:
                        node["IsStackable"] = new JSONData(true);
                        break;
                }

                //free products don't go into PlayFab
                if (playfabVersion && (product.priceList.Count == 0 || product.priceList.All(x => x.currency != null && x.amount == 0))) continue;

                IAPExchangeObject realPrice = product.priceList.Find(x => x.type == IAPExchangeObject.ExchangeType.RealMoney);
                if(realPrice != null)
                {
                    string allowedChars = "01234567890.,";
                    string price = new string(product.priceList[0].realPrice.Where(c => allowedChars.Contains(c)).ToArray());
                    price = price.Replace('.', ',');
                    double doublePrice = 0;

                    if (!string.IsNullOrEmpty(price))
                    {
                        double.TryParse(price, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out doublePrice);
                    }
                    node["VirtualCurrencyPrices"]["RM"] = new JSONData(doublePrice);
                }

                List<IAPExchangeObject> currencyExchange = product.priceList.FindAll(x => x.type == IAPExchangeObject.ExchangeType.VirtualCurrency);
                for(int j = 0; j < currencyExchange.Count; j ++)
                {
                    node["VirtualCurrencyPrices"][currencyExchange[j].currency.ID.Substring(0, 2).ToUpper()] = new JSONData(currencyExchange[j].amount);
                }

                List<IAPExchangeObject> productExchange = product.priceList.FindAll(x => x.type == IAPExchangeObject.ExchangeType.VirtualProduct);
                if(productExchange.Count > 0) node["SIS"]["VirtualItemPrices"] = new JSONArray();
                for (int j = 0; j < productExchange.Count; j++)
                {
                    node["SIS"]["VirtualItemPrices"][j]["ItemId"] = new JSONData(productExchange[j].product.ID);
                    node["SIS"]["VirtualItemPrices"][j]["Amount"] = new JSONData(productExchange[j].amount);
                }

                currencyExchange.Clear();
                productExchange.Clear();

                currencyExchange = product.rewardList.FindAll(x => x.type == IAPExchangeObject.ExchangeType.VirtualCurrency);
                for (int j = 0; j < currencyExchange.Count; j++)
                {
                    node["Bundle"]["BundledVirtualCurrencies"][currencyExchange[j].currency.ID.Substring(0, 2).ToUpper()] = new JSONData(Mathf.Abs(currencyExchange[j].amount));
                }

                productExchange = product.rewardList.FindAll(x => x.type == IAPExchangeObject.ExchangeType.VirtualProduct);
                if (playfabVersion)
                {
                    string bundledItemReward = string.Empty;
                    for (int j = 0; j < productExchange.Count; j++)
                    {
                        //consumables via UsageCount (not UsagePeriod) are currently not supported
                        //implementing that would require adding an API call to /Client/ConsumeItem too
                        //the current implementation makes use of Player data, instead of Content data
                        if (product.ID == productExchange[j].product.ID)
                            continue;

                        //very weird PlayFab format, counting each product individually
                        for (int k = 0; k < productExchange[j].amount; k++)
                        {
                            bundledItemReward += productExchange[j].product.ID + ",";
                        }
                    }
                    if (!string.IsNullOrEmpty(bundledItemReward))
                    {
                        //remove last comma
                        bundledItemReward = bundledItemReward.Substring(0, bundledItemReward.Length - 1);
                        node["Bundle"]["BundledItems"] = new JSONData(bundledItemReward);
                    }
                }
                else if(productExchange.Count != 0)
                {
                    node["Bundle"]["BundledItems"] = new JSONArray();
                    for (int j = 0; j < productExchange.Count; j++)
                    {
                        node["Bundle"]["BundledItems"][j]["ItemId"] = new JSONData(productExchange[j].product.ID);
                        node["Bundle"]["BundledItems"][j]["Amount"] = new JSONData(productExchange[j].amount);
                    }
                }

                node["SIS"]["Ref"] = new JSONData(product.referenceID);
                node["SIS"]["Group"]["Name"] = new JSONData(product.category.ID);
                node["SIS"]["Group"]["Ref"] = new JSONData(product.category.referenceID);
                node["SIS"]["Fetch"] = new JSONData(product.fetch);
                node["SIS"]["Price"] = new JSONData(realPrice == null ? "" : realPrice.realPrice);
                node["SIS"]["Type"] = new JSONData((int)product.type);
                if(product.icon) node["SIS"]["Icon"] = new JSONData(AssetDatabase.GetAssetPath(product.icon));

                if (product.requirement.pairs.Count > 0)
                {
                    node["SIS"]["Requirement"]["Pairs"] = new JSONArray();
                    node["SIS"]["Requirement"]["Text"] = new JSONData(product.requirement.label);
                }
                
                for (int j = 0; j < product.requirement.pairs.Count; j++)
                {
                    if (string.IsNullOrEmpty(product.requirement.pairs[j].Key)) continue;
                    node["SIS"]["Requirement"]["Pairs"][j]["Id"] = new JSONData(product.requirement.pairs[j].Key);
                    node["SIS"]["Requirement"]["Pairs"][j]["Value"] = new JSONData(product.requirement.pairs[j].Value);
                }

                if (product.nextUpgrade != null)
                {
                    node["SIS"]["Requirement"]["Next"] = new JSONData(product.nextUpgrade.ID);
                }

                if (product.storeIDs.Count > 0) node["SIS"]["Overrides"]["Ids"] = new JSONArray();
                for (int j = 0; j < product.storeIDs.Count; j++)
                {
                    node["SIS"]["Overrides"]["Ids"][j]["Platform"] = new JSONData(product.storeIDs[j].store);
                    node["SIS"]["Overrides"]["Ids"][j]["Active"] = new JSONData(product.storeIDs[j].active);
                    node["SIS"]["Overrides"]["Ids"][j]["Id"] = new JSONData(product.storeIDs[j].ID);
                }

                if (product.customBilling.active)
                {
                    node["SIS"]["Overrides"]["Billing"]["Active"] = new JSONData(product.customBilling.active);
                    node["SIS"]["Overrides"]["Billing"]["Provider"] = new JSONData(product.customBilling.provider.ToString());
                }

                itemArray[itemArray.Count] = node;
            }

            data["Catalog"] = itemArray;

            //preformatted result, eventual JSON errors with empty strings
            string result = data.ToString();
            result = result.Replace(":,", ":\"\",");
            result = result.Replace(":}", ":\"\"}");

            return result;
        }
    }
}
