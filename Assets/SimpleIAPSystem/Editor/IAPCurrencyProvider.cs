using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.UIElements;

namespace SIS
{
    class IAPCurrencyProvider : SettingsProvider
    {
        private SerializedObject serializedObject;
        private IAPScriptableObject asset;

        ReorderableList m_List;
        IAPCurrency selectedItem;

        Vector2 scrollPos = Vector2.zero;


        class Styles
        {
            public static GUIContent Header = new GUIContent("Virtual Currencies");
            public static GUIContent Info = new GUIContent("Currencies are soft currencies which only exist within your app. Currency IDs need to be unique. Currency can be bought using real money, earned in-game " +
                                                           "or spent in exchange for other products or virtual currencies.");
        }

        public IAPCurrencyProvider(string path, SettingsScope scope = SettingsScope.Project)
            : base(path, scope) { }


        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            serializedObject = IAPScriptableObject.GetSerializedSettings();
            asset = serializedObject.targetObject as IAPScriptableObject;

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
            List<IAPCurrency> sourceList = asset.currencyList;

            m_List = new ReorderableList(sourceList, typeof(IAPCurrency), true, true, true, true);
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
                IAPCurrency newItem = new IAPCurrency() { referenceID = System.Guid.NewGuid().ToString("D"),
                                                          ID = "currency" + Random.Range(1000, 10000) };
                asset.currencyList.Add(newItem);

                list.index = newIndex;
                selectedItem = newItem;
            };

            m_List.onSelectCallback = list =>
            {
                selectedItem = sourceList[list.index];
            };

            m_List.onRemoveCallback = list =>
            {
                int newIndex = Mathf.Clamp(list.index - 1, 0, list.count - 1);
                asset.currencyList.Remove(selectedItem);

                list.index = newIndex;
                selectedItem = list.count > 0 ? sourceList[newIndex] : null;
            };
        }


        void DrawListElement()
        {
            if (selectedItem == null) return;

            GUI.enabled = false;
            EditorGUILayout.LabelField("Reference ID:", selectedItem.referenceID);
            GUI.enabled = true;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Metadata", EditorStyles.boldLabel);
            selectedItem.ID = EditorGUILayout.TextField("ID:", selectedItem.ID);
            selectedItem.baseAmount = EditorGUILayout.IntField("Base Amount:", selectedItem.baseAmount);
            selectedItem.maxAmount = EditorGUILayout.IntField("Max Amount:", selectedItem.maxAmount);

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Icon:", GUILayout.MaxWidth(150));
            selectedItem.icon = (Sprite)EditorGUILayout.ObjectField(selectedItem.icon, typeof(Sprite), false, GUILayout.Width(60), GUILayout.Height(60));
            GUILayout.EndHorizontal();
        }


        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new IAPCurrencyProvider("Project/Simple IAP System/Currencies", SettingsScope.Project);

            // Automatically extract all keywords from the Styles.
            provider.keywords = GetSearchKeywordsFromGUIContentProperties<Styles>();
            return provider;
        }
    }
}
