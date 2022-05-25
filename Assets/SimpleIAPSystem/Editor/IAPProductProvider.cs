/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.UIElements;

#if SIS_IAP
using UnityEngine.Purchasing;
#endif

namespace SIS
{
    class IAPProductProvider : SettingsProvider
    {
        private SerializedObject serializedObject;
        private IAPScriptableObject asset;

        ReorderableList m_List;
        IAPProduct selectedItem;

        string[] categoryNames;
        string[] priceCurrencyNames;
        string[] rewardCurrencyNames;
        string[] productNames;
        string[] storeNames;

        public static int categoryIndex = 0;
        Vector2 scrollPos = Vector2.zero;
        int toolbarIndex = 0;
        string[] toolbarNames = new string[] { "Definition", "Overrides", "Requirements" };
        string errorMessage = "";


        class Styles
        {
            public static GUIContent Header = new GUIContent("In App Products");
            public static GUIContent Info = new GUIContent("Products are associated with a category and define a virtual object within your shop that your users can interact with. Product IDs need to be unique. " +
                                                           "Products can be visualized in your shop using the ShopItem or a custom component.");
        }

        public IAPProductProvider(string path, SettingsScope scope = SettingsScope.Project)
            : base(path, scope) { }


        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            serializedObject = IAPScriptableObject.GetSerializedSettings();
            asset = serializedObject.targetObject as IAPScriptableObject;

            categoryNames = new string[] { "Choose Category..." }.Union(asset.categoryList.Select(category => category.ID)).ToArray();
            priceCurrencyNames = new string[] { "Add Currency...", "Real Money" }.Union(asset.currencyList.Select(currency => currency.ID)).ToArray();
            rewardCurrencyNames = new string[] { "Add Currency..." }.Union(asset.currencyList.Select(currency => currency.ID)).ToArray();
            productNames = new string[] { "Add Product..." }.Union(asset.productList.Select(product => product.ID)).ToArray();
            storeNames = new string[] { "Add Store Override..." }.Union(System.Enum.GetNames(typeof(IAPPlatform))).ToArray();

            InitReorderableList();
        }


        public override void OnDeactivate()
        {
            AssetDatabase.SaveAssets();
        }


        public override void OnGUI(string searchContext)
        {
            serializedObject.Update();

            // Preferences GUI
            EditorGUILayout.HelpBox(Styles.Info.text, MessageType.None);

            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(5);

            EditorGUILayout.BeginVertical(GUILayout.Width(180));

            //Category Dropdown
            int newIndex = EditorGUILayout.Popup(categoryIndex, categoryNames);
            if (newIndex > 0 && categoryIndex != newIndex)
            {
                categoryIndex = newIndex;
                selectedItem = null;
                InitReorderableList();
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            if(categoryIndex > 0)
                m_List.DoLayoutList();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();

            GUILayout.Box("", GUILayout.Width(1), GUILayout.ExpandHeight(true));

            EditorGUILayout.BeginVertical();

            DrawListElement();

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorUtility.SetDirty(serializedObject.targetObject);
            serializedObject.ApplyModifiedProperties();
        }


        void InitReorderableList(int oldIndex = -1)
        {
            List<IAPProduct> sourceList = new List<IAPProduct>();

            for (int i = 0; i < asset.productList.Count; i++)
            {
                if (asset.productList[i].category != null &&
                    asset.productList[i].category.referenceID == asset.categoryList[Mathf.Clamp(categoryIndex - 1, 0, asset.categoryList.Count - 1)].referenceID)
                    sourceList.Add(asset.productList[i]);
            }

            m_List = new ReorderableList(sourceList, typeof(IAPProduct), true, true, true, true);
            if (sourceList.Count == 0) selectedItem = null;
            else if (oldIndex >= 0)
            {
                m_List.index = oldIndex;
                selectedItem = sourceList[oldIndex];
            }

            m_List.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, Styles.Header);
            };

            m_List.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = sourceList[index];
                rect.y += 2;
                EditorGUI.LabelField(new Rect(rect.x, rect.y, 150, EditorGUIUtility.singleLineHeight), element.ID);
            };

            m_List.onAddCallback = list =>
            {
                int newIndex = sourceList.Count == 0 ? 0 : sourceList.Count;
                IAPProduct newItem = new IAPProduct { referenceID = System.Guid.NewGuid().ToString("D"),
                                                      ID = "product" + Random.Range(1000, 10000),
                                                      category = asset.categoryList[categoryIndex - 1] };
                asset.productList.Add(newItem);

                InitReorderableList(newIndex);
            };

            m_List.onSelectCallback = list =>
            {
                errorMessage = string.Empty;
                selectedItem = sourceList[list.index];
            };

            m_List.onReorderCallbackWithDetails = (list, reOld, reNew) =>
            {
                sourceList.Remove(selectedItem);
                asset.productList.Remove(selectedItem);

                if (reNew == sourceList.Count)
                {
                    sourceList.Add(selectedItem);
                    asset.productList.Add(selectedItem);
                }
                else
                {
                    int insertAt = asset.productList.IndexOf(sourceList[reNew]);
                    sourceList.Insert(reNew, selectedItem);
                    asset.productList.Insert(insertAt, selectedItem);
                }
            };

            m_List.onRemoveCallback = list =>
            {
                int newIndex = Mathf.Clamp(list.index - 1, 0, list.count - 1);
                asset.productList.Remove(selectedItem);

                InitReorderableList(newIndex);
            };
        }


        void DrawListElement()
        {
            if (selectedItem == null) return;

            toolbarIndex = GUILayout.Toolbar(toolbarIndex, toolbarNames);

            switch(toolbarIndex)
            {
                case 0:
                    DrawToolBar0();
                    break;
                case 1:
                    DrawToolBar1();
                    break;
                case 2:
                    DrawToolBar2();
                    break;
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
                EditorGUILayout.Space();
                EditorGUILayout.EndVertical();

                GUI.color = Color.yellow;
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                GUI.color = Color.white;
                EditorGUILayout.HelpBox(errorMessage, MessageType.Warning, true);
                if (GUILayout.Button("OK", GUILayout.Width(80), GUILayout.Height(38)))
                {
                    errorMessage = string.Empty;
                }

                EditorGUILayout.EndHorizontal();
                GUI.color = Color.white;
                EditorGUILayout.Space();
            }
        }


        void DrawToolBar0()
        {
            GUI.enabled = false;
            EditorGUILayout.LabelField("Reference ID:", selectedItem.referenceID);
            GUI.enabled = true;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Metadata", EditorStyles.boldLabel);
            selectedItem.ID = EditorGUILayout.TextField("ID:", selectedItem.ID);

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                selectedItem.type = (ProductType)EditorGUILayout.EnumPopup("Type:", selectedItem.type);

                #if !SIS_IAP
                if(selectedItem.type == ProductType.Subscription)
                {
                    selectedItem.type = ProductType.NonConsumable;
                    Debug.LogWarning("Subscription Products are not supported without Unity IAP! Resetting to Non-Consumable.");
                }
                #endif
                
                if (check.changed && selectedItem.type != ProductType.Consumable)
                {
                    if(!selectedItem.rewardList.Exists(x => x.product != null && x.product.referenceID == selectedItem.referenceID))
                        selectedItem.rewardList.Insert(0, new IAPExchangeObject() { type = IAPExchangeObject.ExchangeType.VirtualProduct, product = selectedItem, amount = 1 });
                }
            }           

            selectedItem.title = EditorGUILayout.TextField("Title:", selectedItem.title);
            selectedItem.description = EditorGUILayout.TextField("Description:", selectedItem.description);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Icon:", GUILayout.MaxWidth(150));
            selectedItem.icon = (Sprite)EditorGUILayout.ObjectField(selectedItem.icon, typeof(Sprite), false, GUILayout.Width(60), GUILayout.Height(60));
            GUILayout.EndHorizontal();
            selectedItem.discount = EditorGUILayout.Toggle("Discount:", selectedItem.discount);
            selectedItem.fetch = EditorGUILayout.Toggle("Fetch:", selectedItem.fetch);
            EditorGUILayout.Space();

            int newIndex = 0;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Costs", EditorStyles.boldLabel);
            List<IAPExchangeObject> prices = selectedItem.priceList;
            List<IAPExchangeObject> pricesFilter = null;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(IAPSettingsStyles.boxStyle);

            GUI.enabled = selectedItem.IsVirtual();
            newIndex = EditorGUILayout.Popup(0, priceCurrencyNames);
            switch(newIndex)
            {
                case 0:
                    break;
                case 1:
                    if (prices.Exists(x => x.type == IAPExchangeObject.ExchangeType.RealMoney)) errorMessage = "Cost currency \"" + "Real Money" + "\" was already added to the product \"" + selectedItem.ID + "\".";
                    else
                    {
                        if(prices.Count > 0)
                        {
                            errorMessage = "Real Money currency is not compatible with other currency or product costs. They were removed.";
                            prices.Clear();
                        }

                        prices.Add(new IAPExchangeObject() { type = IAPExchangeObject.ExchangeType.RealMoney });
                    }
                    break;
                default:
                    IAPCurrency newCur = asset.currencyList[newIndex - 2];

                    if (prices.Exists(x => x.currency != null && x.currency.referenceID == newCur.referenceID)) errorMessage = "Cost currency \"" + newCur.ID + "\" was already added to the product \"" + selectedItem.ID + "\".";
                    else prices.Add(new IAPExchangeObject() { type = IAPExchangeObject.ExchangeType.VirtualCurrency, currency = newCur, amount = 0 });
                    break;
            }
            if (newIndex != 0)
            {
                newIndex = 0;
                EditorUtility.SetDirty(asset);
            }
            GUI.enabled = true;

            pricesFilter = prices.FindAll(x => x.type == IAPExchangeObject.ExchangeType.RealMoney);
            for (int i = 0; i < pricesFilter.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(priceCurrencyNames[1]);
                pricesFilter[i].realPrice = EditorGUILayout.TextField(pricesFilter[i].realPrice);

                if (GUILayout.Button("", IAPSettingsStyles.deleteButtonStyle))
                {
                    prices.Remove(pricesFilter[i]);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            pricesFilter = prices.FindAll(x => x.type == IAPExchangeObject.ExchangeType.VirtualCurrency);
            for (int i = 0; i < pricesFilter.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(pricesFilter[i].currency.ID);
                pricesFilter[i].amount = EditorGUILayout.IntField(pricesFilter[i].amount);

                if (GUILayout.Button("", IAPSettingsStyles.deleteButtonStyle))
                {
                    prices.Remove(pricesFilter[i]);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical(IAPSettingsStyles.boxStyle);

            GUI.enabled = selectedItem.IsVirtual();
            newIndex = EditorGUILayout.Popup(0, productNames);
            if (newIndex != 0)
            {
                IAPProduct newProduct = asset.productList.Single(x => x.ID == productNames[newIndex]);
                prices.Add(new IAPExchangeObject() { type = IAPExchangeObject.ExchangeType.VirtualProduct, product = newProduct, amount = 1 });

                newIndex = 0;
                EditorUtility.SetDirty(asset);
            }
            GUI.enabled = true;

            pricesFilter = prices.FindAll(x => x.type == IAPExchangeObject.ExchangeType.VirtualProduct);
            for (int i = 0; i < pricesFilter.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(pricesFilter[i].product.ID);

                GUI.enabled = asset.productList.Exists(x => x.referenceID == pricesFilter[i].product.referenceID && x.type == ProductType.Consumable);
                pricesFilter[i].amount = Mathf.Clamp(EditorGUILayout.IntField(pricesFilter[i].amount), 1, int.MaxValue);
                GUI.enabled = true;

                if (GUILayout.Button("", IAPSettingsStyles.deleteButtonStyle))
                {
                    prices.Remove(pricesFilter[i]);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rewards", EditorStyles.boldLabel);
            List<IAPExchangeObject> rewards = selectedItem.rewardList;
            List<IAPExchangeObject> rewardsFilter = null;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(IAPSettingsStyles.boxStyle);

            rewardsFilter = rewards.FindAll(x => x.type == IAPExchangeObject.ExchangeType.VirtualCurrency);
            newIndex = EditorGUILayout.Popup(0, rewardCurrencyNames);
            if (newIndex != 0)
            {
                IAPCurrency newCur = asset.currencyList[newIndex - 1];

                if (rewardsFilter.Exists(x => x.currency.referenceID == newCur.referenceID)) errorMessage = "Reward currency \"" + newCur.ID + "\" was already added to the product \"" + selectedItem.ID + "\".";
                else rewards.Add(new IAPExchangeObject() { type = IAPExchangeObject.ExchangeType.VirtualCurrency, currency = newCur, amount = 0 });

                newIndex = 0;
                EditorUtility.SetDirty(asset);
            }

            for (int i = 0; i < rewardsFilter.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(rewardsFilter[i].currency.ID);
                rewardsFilter[i].amount = EditorGUILayout.IntField(rewardsFilter[i].amount);

                if (GUILayout.Button("", IAPSettingsStyles.deleteButtonStyle))
                {
                    rewards.Remove(rewardsFilter[i]);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical(IAPSettingsStyles.boxStyle);

            rewardsFilter = rewards.FindAll(x => x.type == IAPExchangeObject.ExchangeType.VirtualProduct);
            newIndex = EditorGUILayout.Popup(0, productNames);
            if (newIndex != 0)
            {
                IAPProduct newProduct = asset.productList.Single(x => x.ID == productNames[newIndex]);
                rewards.Add(new IAPExchangeObject() { type = IAPExchangeObject.ExchangeType.VirtualProduct, product = newProduct, amount = 1 });

                newIndex = 0;
                EditorUtility.SetDirty(asset);
            }

            
            for (int i = 0; i < rewardsFilter.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(rewardsFilter[i].product.ID);

                GUI.enabled = asset.productList.Exists(x => x.referenceID == rewardsFilter[i].product.referenceID && x.type == ProductType.Consumable);
                rewardsFilter[i].amount = Mathf.Clamp(EditorGUILayout.IntField(rewardsFilter[i].amount), 1, int.MaxValue);
                GUI.enabled = true;

                if (GUILayout.Button("", IAPSettingsStyles.deleteButtonStyle))
                {
                    rewards.Remove(rewardsFilter[i]);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            if(selectedItem.type != ProductType.Consumable && !selectedItem.rewardList.Exists(x => x.product != null && x.product.referenceID == selectedItem.referenceID))
            {
                errorMessage = "This Non-Consumable product does not include itself as a Reward. Therefore, the user does not receive it on purchase and could try to buy it repeatedly. If this is intentional, you can ignore this warning.";
            }
        }


        void DrawToolBar1()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Billing Provider", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Active:", GUILayout.MaxWidth(50));
            selectedItem.customBilling.active = EditorGUILayout.Toggle(selectedItem.customBilling.active, GUILayout.MaxWidth(40));

            EditorGUILayout.LabelField("Provider:", GUILayout.MaxWidth(60));
            GUI.enabled = selectedItem.customBilling.active;
            selectedItem.customBilling.provider = (BillingProvider)EditorGUILayout.EnumPopup(selectedItem.customBilling.provider);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Store Meta", EditorStyles.boldLabel);

            int newIndex = EditorGUILayout.Popup(0, storeNames);
            if (newIndex != 0)
            {
                StoreMetaDefinition newStoreDef = new StoreMetaDefinition();
                newStoreDef.store = storeNames[newIndex];

                if(selectedItem.storeIDs.Exists(x => x.store == newStoreDef.store)) errorMessage = "Store override \"" + newStoreDef.store + "\" was already added to the product \"" + selectedItem.ID + "\".";
                else selectedItem.storeIDs.Add(newStoreDef);

                newIndex = 0;
                EditorUtility.SetDirty(asset);
            }

            for(int i = 0; i < selectedItem.storeIDs.Count; i++)
            {
                StoreMetaDefinition storeID = selectedItem.storeIDs[i];

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(storeID.store, EditorStyles.boldLabel, GUILayout.MaxWidth(120));
                EditorGUILayout.LabelField("Available:", GUILayout.MaxWidth(70));
                storeID.active = EditorGUILayout.Toggle(storeID.active, GUILayout.MaxWidth(40));
                EditorGUILayout.LabelField("Store ID:", GUILayout.MaxWidth(60));

                GUI.enabled = storeID.active;
                storeID.ID = EditorGUILayout.TextField(storeID.ID);
                GUI.enabled = true;

                if (GUILayout.Button("", IAPSettingsStyles.deleteButtonStyle))
                {
                    selectedItem.storeIDs.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
        }


        void DrawToolBar2()
        {
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Unlocks", EditorStyles.boldLabel);

            if (GUILayout.Button("Add Unlock Requirement"))
            {
                selectedItem.requirement.pairs.Add(new KeyValuePairStringInt());
            }

            for (int i = 0; i < selectedItem.requirement.pairs.Count; i++)
            {
                KeyValuePairStringInt keyValue = selectedItem.requirement.pairs[i];

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Key:", GUILayout.MaxWidth(70));
                keyValue.Key = EditorGUILayout.TextField(keyValue.Key);
                EditorGUILayout.LabelField("Value:", GUILayout.MaxWidth(60));
                keyValue.Value = EditorGUILayout.IntField(keyValue.Value, GUILayout.MaxWidth(60));

                if (GUILayout.Button("", IAPSettingsStyles.deleteButtonStyle))
                {
                    selectedItem.requirement.pairs.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            selectedItem.requirement.label = EditorGUILayout.TextField("Description:", selectedItem.requirement.label);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Upgrades", EditorStyles.boldLabel);

            int newIndex = 0;
            if (selectedItem.nextUpgrade != null)
                newIndex = 1 + asset.productList.FindIndex(x => x.referenceID == selectedItem.nextUpgrade.referenceID);

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                newIndex = EditorGUILayout.Popup("Next Upgrade:", newIndex, productNames);
                if (!check.changed) return;

                IAPProduct newProduct = newIndex == 0 ? null : asset.productList.Single(x => x.ID == productNames[newIndex]);
                if (newProduct != null && selectedItem.referenceID == newProduct.referenceID)
                {
                    newIndex = 0;
                    errorMessage = "Next upgrade product \"" + newProduct.ID + "\" cannot be referenced by itself.";
                }
                else
                    selectedItem.nextUpgrade = newProduct;

                EditorUtility.SetDirty(asset);
            }
        }


        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new IAPProductProvider("Project/Simple IAP System/Products", SettingsScope.Project);

            // Automatically extract all keywords from the Styles.
            provider.keywords = GetSearchKeywordsFromGUIContentProperties<Styles>();
            return provider;
        }
    }
}
