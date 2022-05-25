/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.UIElements;

namespace SIS
{
    class IAPSettingsProvider : SettingsProvider
    {
        private SerializedObject serializedObject;
        private IAPScriptableObject asset;

        private int toolbarIndex = 0;
        private string[] toolbarNames = new string[] { "Setup", "Tools", "About" };

        public bool isChanged = false;
        public bool isPackageImported = false;
        public bool isIAPEnabled = false;
        public ListRequest pckList;

        private BuildTargetIAP targetIAPGroup;
        private string[] iapNames = new string[] { "", "SIS_IAP" };

        private DesktopPlugin desktopPlugin = DesktopPlugin.UnityIAP;
        private WebPlugin webPlugin = WebPlugin.UnityIAP;
        private AndroidPlugin androidPlugin = AndroidPlugin.UnityIAP;
        private IOSPlugin iosPlugin = IOSPlugin.UnityIAP;
        private ThirdPartyPlugin thirdPartyPlugin = ThirdPartyPlugin.None;

        private UIAssetPlugin uiPlugin = UIAssetPlugin.UnityUI;

        private bool customStoreFoldout = false; 
        private string databaseContent;

        class Styles
        {
            public static GUIContent Info = new GUIContent("Welcome! This section contains the billing setup, store settings and other tools for Simple IAP System. When you are happy with your billing configuration, " +
                                                           "expand this section in the menu on the left, in order to define your in-app purchase categories and products.");
        }

        public IAPSettingsProvider(string path, SettingsScope scope = SettingsScope.Project)
            : base(path, scope) { }


        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            serializedObject = IAPScriptableObject.GetSerializedSettings();
            asset = serializedObject.targetObject as IAPScriptableObject;

            GetScriptingDefines();
            GetDatabaseContent();
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

            DrawListElement();

            EditorUtility.SetDirty(serializedObject.targetObject);
            serializedObject.ApplyModifiedProperties();
        }


        void DrawListElement()
        {
            toolbarIndex = GUILayout.Toolbar(toolbarIndex, toolbarNames);

            switch (toolbarIndex)
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
        }


        void DrawToolBar0()
        {
            EditorGUILayout.Space();
            DrawBillingSetup();

            EditorGUILayout.Space();
            DrawCustomStores();
        }


        void DrawBillingSetup()
        {
            GUILayout.Label("Unity IAP", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh", GUILayout.Width(IAPSettingsStyles.buttonWidth)))
            {
                GetScriptingDefines();
                return;
            }

            //Check Unity PackageManager package
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Package Imported: ", GUILayout.Width(IAPSettingsStyles.buttonWidth));
            if (pckList == null || !pckList.IsCompleted)
            {
                GUILayout.Label("CHECKING...");
            }
            else if (!isPackageImported)
            {
                PackageCollection col = pckList.Result;
                foreach (UnityEditor.PackageManager.PackageInfo info in col)
                {
                    if (info.packageId.StartsWith("com.unity.purchasing", System.StringComparison.Ordinal))
                    {
                        isPackageImported = true;
                        break;
                    }
                }

                if (!isPackageImported)
                    isIAPEnabled = false;
            }

            GUILayout.Label(isPackageImported == true ? "OK" : "NOT OK");
            EditorGUILayout.EndHorizontal();

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                bool unityIAPActive = isPackageImported;
                GUI.enabled = unityIAPActive;

                EditorGUILayout.Space();
                targetIAPGroup = (BuildTargetIAP)EditorGUILayout.EnumFlagsField("Billing Platforms:", targetIAPGroup);
                isIAPEnabled = EditorGUILayout.Toggle("Activate Unity IAP:", isIAPEnabled);

                GUI.enabled = true;
                GUI.enabled = unityIAPActive == true ? isIAPEnabled : false;
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Store Implementation", EditorStyles.boldLabel);
                desktopPlugin = (DesktopPlugin)EditorGUILayout.EnumPopup("Standalone:", desktopPlugin);
                webPlugin = (WebPlugin)EditorGUILayout.EnumPopup("WebGL:", webPlugin);
                androidPlugin = (AndroidPlugin)EditorGUILayout.EnumPopup("Android:", androidPlugin);
                iosPlugin = (IOSPlugin)EditorGUILayout.EnumPopup("IOS:", iosPlugin);
                
                EditorGUILayout.Space();
                if (desktopPlugin.ToString().Contains("Playfab") || webPlugin.ToString().Contains("Playfab")) thirdPartyPlugin = ThirdPartyPlugin.PlayFab;
                if (desktopPlugin == DesktopPlugin.Xsolla || webPlugin == WebPlugin.Xsolla || androidPlugin == AndroidPlugin.Xsolla || iosPlugin == IOSPlugin.Xsolla) thirdPartyPlugin = ThirdPartyPlugin.Xsolla;
                thirdPartyPlugin = (ThirdPartyPlugin)EditorGUILayout.EnumPopup("Third Party Service:", thirdPartyPlugin);
                GUI.enabled = true;

                if (check.changed)
                {
                    isChanged = check.changed;
                }
            }

            if (isChanged) GUI.color = Color.yellow;

            EditorGUILayout.Space();
            if (GUILayout.Button("Apply"))
            {
                ApplyScriptingDefines();
                isChanged = false;
            }
            GUI.color = Color.white;
        }


        void DrawCustomStores()
        {
            EditorGUILayout.LabelField("Custom Stores", EditorStyles.boldLabel);
            customStoreFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(customStoreFoldout, "PayPal");
            if (customStoreFoldout)
            {
                asset.customStoreConfig.PayPal.enabled = EditorGUILayout.Toggle("Enabled:", asset.customStoreConfig.PayPal.enabled);

                GUI.enabled = asset.customStoreConfig.PayPal.enabled;
                asset.customStoreConfig.PayPal.currencyCode = EditorGUILayout.TextField("Currency Code:", asset.customStoreConfig.PayPal.currencyCode);
                asset.customStoreConfig.PayPal.sandbox.clientID = EditorGUILayout.TextField("Sandbox Client ID:", asset.customStoreConfig.PayPal.sandbox.clientID);
                asset.customStoreConfig.PayPal.sandbox.secretKey = EditorGUILayout.TextField("Sandbox Secret:", asset.customStoreConfig.PayPal.sandbox.secretKey);
                asset.customStoreConfig.PayPal.live.clientID = EditorGUILayout.TextField("Live Client ID:", asset.customStoreConfig.PayPal.live.clientID);
                asset.customStoreConfig.PayPal.live.secretKey = EditorGUILayout.TextField("Live Secret:", asset.customStoreConfig.PayPal.live.secretKey);
                asset.customStoreConfig.PayPal.returnUrl = EditorGUILayout.TextField("Return URL:", asset.customStoreConfig.PayPal.returnUrl);
                GUI.enabled = true;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }


        void DrawToolBar1()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Backup", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Import from JSON"))
            {
                string path = EditorUtility.OpenFolderPanel("Import IAP Settings from JSON", "", "");
                if (path.Length != 0)
                {
                    asset.currencyList = IAPSettingsExporter.FromJSONCurrency(File.ReadAllText(path + "/SimpleIAPSystem_Currencies.json"));
                    asset.categoryList = IAPSettingsExporter.FromJSONCategory(File.ReadAllText(path + "/SimpleIAPSystem_IAPSettings.json"));
                    asset.productList = IAPSettingsExporter.FromJSONProduct(File.ReadAllText(path + "/SimpleIAPSystem_IAPSettings.json"));
                    return;
                }
            }

            if (GUILayout.Button("Export to JSON"))
            {
                string path = EditorUtility.SaveFolderPanel("Save IAP Settings as JSON", "", "");
                if (path.Length != 0)
                {
                    File.WriteAllBytes(path + "/SimpleIAPSystem_IAPSettings.json", System.Text.Encoding.UTF8.GetBytes(IAPSettingsExporter.ToJSON(asset.productList)));
                    File.WriteAllBytes(path + "/SimpleIAPSystem_IAPSettings_PlayFab.json", System.Text.Encoding.UTF8.GetBytes(IAPSettingsExporter.ToJSON(asset.productList, true)));
                    File.WriteAllBytes(path + "/SimpleIAPSystem_Currencies.json", System.Text.Encoding.UTF8.GetBytes(IAPSettingsExporter.ToJSON(asset.currencyList)));                    
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Local Storage", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh"))
            {
                GetDatabaseContent();
            }

            if (GUILayout.Button("Clear"))
            {
                if (EditorUtility.DisplayDialog("Clear Local Database Entries",
                                                "Are you sure you want to clear the PlayerPref data for this project? (This includes Simple IAP System data, but also all other PlayerPrefs)", "Clear", "Cancel"))
                {
                    string unityPurchasingPath = Path.Combine(Path.Combine(Application.persistentDataPath, "Unity"), "UnityPurchasing");
                    
                    #if SIS_IAP
                    if (Directory.Exists(unityPurchasingPath))
                        UnityEngine.Purchasing.UnityPurchasing.ClearTransactionLog();
                    #endif

                    if (Directory.Exists(unityPurchasingPath))
                        Directory.Delete(unityPurchasingPath, true);

                    DBManager.ClearAll();
                    if (DBManager.GetInstance() != null)
                        DBManager.GetInstance().Init();

                    GetDatabaseContent();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(databaseContent) ? "- no data -" : databaseContent, GUI.skin.GetStyle("HelpBox"), GUILayout.MaxHeight(150));

            EditorGUILayout.Space();
            DrawSourceCustomization();
        }


        void DrawSourceCustomization()
        {
            EditorGUILayout.LabelField("Source Customization", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("UI", GUILayout.Width(IAPSettingsStyles.buttonWidth));

            uiPlugin = (UIAssetPlugin)EditorGUILayout.EnumPopup(uiPlugin);

            if (GUILayout.Button("Convert", GUILayout.Width(IAPSettingsStyles.buttonWidth)))
            {
                if (EditorUtility.DisplayDialog("Convert UI Source Code",
                                                "Are you sure you want to convert the UI references in code? If choosing a UI solution other than Unity UI, " +
                                                "all sample shop prefabs and demo scenes will break. You should not do this without a backup.", "Continue", "Cancel"))
                {
                    UISourceConverterData.Convert(uiPlugin);
                }
            }
            EditorGUILayout.EndHorizontal();
        }


        void DrawToolBar2()
        {
            EditorGUILayout.Space();
            GUILayout.Label("Info", EditorStyles.boldLabel);
            if (GUILayout.Button("Homepage", GUILayout.Width(IAPSettingsStyles.buttonWidth)))
            {
                Help.BrowseURL("https://flobuk.com");
            }

            if (GUILayout.Button("YouTube", GUILayout.Width(IAPSettingsStyles.buttonWidth)))
            {
                Help.BrowseURL("https://www.youtube.com/channel/UCCY5CCgf96mbWYXawyW8RuQ");
            }

            EditorGUILayout.Space();
            GUILayout.Label("Support", EditorStyles.boldLabel);
            if (GUILayout.Button("Online Documentation", GUILayout.Width(IAPSettingsStyles.buttonWidth)))
            {
                Help.BrowseURL("https://flobuk.com/docs/sis");
            }

            if (GUILayout.Button("Scripting Reference", GUILayout.Width(IAPSettingsStyles.buttonWidth)))
            {
                Help.BrowseURL("https://flobuk.com/docs/sis/api");
            }

            if (GUILayout.Button("Unity Forum", GUILayout.Width(IAPSettingsStyles.buttonWidth)))
            {
                Help.BrowseURL("https://forum.unity3d.com/threads/194975");
            }

            EditorGUILayout.Space();
            GUILayout.BeginVertical(EditorStyles.helpBox);
            {
                GUILayout.Label("Support me! :-)", EditorStyles.boldLabel);
                GUILayout.Label("Please consider leaving a positive rating on the Unity Asset Store. As a solo developer, each review counts! " +
                                "Your support helps me stay motivated, improving this asset and making it more popular. \n\nIf you are looking for support, please head over to the support forum instead.", new GUIStyle(EditorStyles.label) { wordWrap = true });
                GUILayout.Space(5f);

                GUI.color = Color.yellow;
                if (GUILayout.Button("Review Asset", GUILayout.Width(IAPSettingsStyles.buttonWidth)))
                {
                    Help.BrowseURL("https://assetstore.unity.com/packages/slug/192362?aid=1011lGiF&pubref=editor_sis");
                }
                GUI.color = Color.white;
            }
            GUILayout.EndVertical();
        }


        void GetScriptingDefines()
        {
            int targetBits = 0;
            foreach (var enumValue in System.Enum.GetValues(typeof(BuildTargetIAP)))
            {
                if (PlayerSettings.GetScriptingDefineSymbolsForGroup((BuildTargetGroup)System.Enum.Parse(typeof(BuildTargetGroup), enumValue.ToString())).Contains(iapNames[1]))
                    targetBits |= (int)enumValue;
            }

            if (targetBits > 0) isIAPEnabled = true;
            if (targetBits == 0 || targetBits == 63) targetBits = -1;
            targetIAPGroup = (BuildTargetIAP)targetBits;

            desktopPlugin = (DesktopPlugin)FindScriptingDefineIndex(BuildTargetGroup.Standalone);
            webPlugin = (WebPlugin)FindScriptingDefineIndex(BuildTargetGroup.WebGL);
            androidPlugin = (AndroidPlugin)FindScriptingDefineIndex(BuildTargetGroup.Android);
            iosPlugin = (IOSPlugin)FindScriptingDefineIndex(BuildTargetGroup.iOS);

            //check if cross-platform use exists
            thirdPartyPlugin = (ThirdPartyPlugin)FindScriptingDefineIndex(BuildTargetGroup.Unknown);

            //check Unity IAP
            isPackageImported = false;

            //download PackageManager list, retrieved later
            pckList = Client.List(true);
        }


        int FindScriptingDefineIndex(BuildTargetGroup group)
        {
            BuildTarget activeTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup activeGroup = BuildPipeline.GetBuildTargetGroup(activeTarget);

            string str = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            string[] defines = null;

            switch (group)
            {
                case BuildTargetGroup.Standalone:
                    defines = EnumHelper.GetEnumDescriptions(desktopPlugin);
                    break;
                case BuildTargetGroup.WebGL:
                    defines = EnumHelper.GetEnumDescriptions(webPlugin);
                    break;
                case BuildTargetGroup.Android:
                    defines = EnumHelper.GetEnumDescriptions(androidPlugin);
                    break;
                case BuildTargetGroup.iOS:
                    defines = EnumHelper.GetEnumDescriptions(iosPlugin);
                    break;
                case BuildTargetGroup.Unknown:
                    str = PlayerSettings.GetScriptingDefineSymbolsForGroup(activeGroup);
                    defines = EnumHelper.GetEnumDescriptions(thirdPartyPlugin);
                    break;
            }

            for (int i = 1; i < defines.Length; i++)
            {
                if (str.Contains(defines[i]))
                {
                    return i;
                }
            }

            return 0;
        }


        void ApplyScriptingDefines()
        {
            List<BuildTargetIAP> selectedElements = new List<BuildTargetIAP>();
            System.Array arrayElements = System.Enum.GetValues(typeof(BuildTargetIAP));
            for (int i = 0; i < arrayElements.Length; i++)
            {
                int layer = 1 << i;
                if (((int)targetIAPGroup & layer) != 0)
                {
                    selectedElements.Add((BuildTargetIAP)arrayElements.GetValue(i));
                }
            }

            for (int i = 0; i < selectedElements.Count; i++)
                SetScriptingDefine((BuildTargetGroup)System.Enum.Parse(typeof(BuildTargetGroup), selectedElements[i].ToString()), iapNames, isIAPEnabled ? 1 : 0);

            SetScriptingDefine(BuildTargetGroup.Standalone, EnumHelper.GetEnumDescriptions(desktopPlugin), (int)desktopPlugin);
            SetScriptingDefine(BuildTargetGroup.WebGL, EnumHelper.GetEnumDescriptions(webPlugin), (int)webPlugin);
            SetScriptingDefine(BuildTargetGroup.Android, EnumHelper.GetEnumDescriptions(androidPlugin), (int)androidPlugin);
            SetScriptingDefine(BuildTargetGroup.iOS, EnumHelper.GetEnumDescriptions(iosPlugin), (int)iosPlugin);

            BuildTargetGroup[] thirdPartyTargets = new BuildTargetGroup[] { BuildTargetGroup.Android, BuildTargetGroup.iOS, BuildTargetGroup.tvOS,
                                                                         BuildTargetGroup.Standalone, BuildTargetGroup.WebGL };

            for (int i = 0; i < thirdPartyTargets.Length; i++)
                SetScriptingDefine(thirdPartyTargets[i], EnumHelper.GetEnumDescriptions(thirdPartyPlugin), (int)thirdPartyPlugin);
        }


        void SetScriptingDefine(BuildTargetGroup target, string[] oldDefines, int newDefine)
        {
            string str = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
            List<string> defs = new List<string>(str.Split(';'));
            if (defs.Count == 0 && !string.IsNullOrEmpty(str)) defs.Add(str);

            for (int i = 0; i < oldDefines.Length; i++)
            {
                if (string.IsNullOrEmpty(oldDefines[i])) continue;
                defs.Remove(oldDefines[i]);
            }

            if (newDefine > 0)
                defs.Add(oldDefines[newDefine]);

            str = "";
            for (int i = 0; i < defs.Count; i++)
                str = defs[i] + ";" + str;

            PlayerSettings.SetScriptingDefineSymbolsForGroup(target, str);
        }


        void GetDatabaseContent()
        {
            if (Application.isPlaying && IAPManager.GetInstance() != null && DBManager.GetInstance() != null)
                databaseContent = DBManager.Read();
            else
                databaseContent = PlayerPrefs.GetString(DBManager.prefsKey, "");

            GUIUtility.keyboardControl = 0;
        }


        // Register the SettingsProvider
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new IAPSettingsProvider("Project/Simple IAP System", SettingsScope.Project);

            // Automatically extract all keywords from the Styles.
            provider.keywords = GetSearchKeywordsFromGUIContentProperties<Styles>();
            return provider;
        }
    }
}
