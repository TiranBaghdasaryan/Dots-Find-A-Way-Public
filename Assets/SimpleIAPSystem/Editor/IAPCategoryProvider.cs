using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.UIElements;

namespace SIS
{
    class IAPCategoryProvider : SettingsProvider
    {
        private SerializedObject serializedObject;
        private IAPScriptableObject asset;

        ReorderableList m_List;
        IAPCategory selectedItem;

        string[] storeNames;

        Vector2 scrollPos = Vector2.zero;
        int toolbarIndex = 0;
        string[] toolbarNames = new string[] { "Definition", "Overrides" };
        string errorMessage = "";


        class Styles
        {
            public static GUIContent Header = new GUIContent("Categories");
            public static GUIContent Info = new GUIContent("Categories can be used to divide products into sections. Category IDs need to be unique. In your shop UI, you can choose to instantiate " +
                                                           "the products from a category automatically using the IAPContainer component.");
        }

        public IAPCategoryProvider(string path, SettingsScope scope = SettingsScope.Project)
            : base(path, scope) { }


        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            serializedObject = IAPScriptableObject.GetSerializedSettings();
            asset = serializedObject.targetObject as IAPScriptableObject;

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

            EditorGUILayout.HelpBox(Styles.Info.text, MessageType.None);

            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(5);

            EditorGUILayout.BeginVertical(GUILayout.Width(180));

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
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


        void InitReorderableList()
        {
            List<IAPCategory> sourceList = asset.categoryList;

            m_List = new ReorderableList(sourceList, typeof(IAPCategory), true, true, true, true);
            selectedItem = null;

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
                IAPCategory newItem = new IAPCategory() { referenceID = System.Guid.NewGuid().ToString("D"),
                                                          ID = "category" + Random.Range(1000, 10000) };
                asset.categoryList.Add(newItem);

                list.index = newIndex;
                selectedItem = newItem;
            };

            m_List.onSelectCallback = list =>
            {
                errorMessage = string.Empty;
                selectedItem = sourceList[list.index];
            };

            m_List.onRemoveCallback = list =>
            {
                List<IAPProduct> toRemove = asset.productList.FindAll(x => x.category.referenceID == asset.categoryList[list.index].referenceID);

                if(toRemove.Count > 0 && !EditorUtility.DisplayDialog("Attention!",
                                               "There are currently " + toRemove.Count + " products associated with this category. " +
                                               "By deleting this category, these products will get deleted too.\n\nDo you wish to continue?",
                                               "Continue", "Cancel"))
                {
                    return;
                }

                for (int i = 0; i < toRemove.Count; i++)
                    asset.productList.Remove(toRemove[i]);

                int newIndex = Mathf.Clamp(list.index - 1, 0, list.count - 1);
                asset.categoryList.Remove(selectedItem);
                IAPProductProvider.categoryIndex = 0;

                list.index = newIndex;
                selectedItem = list.count > 0 ? sourceList[newIndex] : null;
            };
        }


        void DrawListElement()
        {
            if (selectedItem == null) return;

            toolbarIndex = GUILayout.Toolbar(toolbarIndex, toolbarNames);

            switch (toolbarIndex)
            {
                case 0:
                    DrawToolBar0();
                    break;
                case 1:
                    DrawToolBar1();
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

                if (selectedItem.storeIDs.Exists(x => x.store == newStoreDef.store)) errorMessage = "Store override \"" + newStoreDef.store + "\" was already added to the product \"" + selectedItem.ID + "\".";
                else selectedItem.storeIDs.Add(newStoreDef);

                newIndex = 0;
                EditorUtility.SetDirty(asset);
            }

            for (int i = 0; i < selectedItem.storeIDs.Count; i++)
            {
                StoreMetaDefinition storeID = selectedItem.storeIDs[i];

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(storeID.store, EditorStyles.boldLabel, GUILayout.MaxWidth(120));
                EditorGUILayout.LabelField("Available:", GUILayout.MaxWidth(70));
                storeID.active = EditorGUILayout.Toggle(storeID.active, GUILayout.MaxWidth(40));

                if (GUILayout.Button("", IAPSettingsStyles.deleteButtonStyle, GUILayout.MaxWidth(100)))
                {
                    selectedItem.storeIDs.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
        }


        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new IAPCategoryProvider("Project/Simple IAP System/Categories", SettingsScope.Project);

            // Automatically extract all keywords from the Styles.
            provider.keywords = GetSearchKeywordsFromGUIContentProperties<Styles>();
            return provider;
        }
    }
}
