/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using UnityEditor;
using System.Linq;

namespace SIS
{
    [CustomEditor(typeof(CurrencyContainer))]
    public class CurrencyContainerEditor : Editor
    {
        private CurrencyContainer script;
        private IAPScriptableObject asset;

        private string[] dropdownNames;
        private int dropdownIndex = 0;


        void OnEnable()
        {
            script = (CurrencyContainer)target;
            asset = IAPScriptableObject.GetOrCreateSettings();
            dropdownNames = new string[] { "Choose Currency..." }.Union(asset.currencyList.Select(element => element.ID)).ToArray();
            
            if(script.currency != null)
                dropdownIndex = 1 + asset.currencyList.FindIndex(x => x.referenceID == script.currency.referenceID);
        }

        public override void OnInspectorGUI()
        {
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                dropdownIndex = EditorGUILayout.Popup("Currency", dropdownIndex, dropdownNames);
                if (check.changed)
                {
                    Undo.RecordObject(script, "Change Currency Dropdown");

                    IAPCurrency newElement = dropdownIndex == 0 ? null : asset.currencyList.Single(x => x.ID == dropdownNames[dropdownIndex]);
                    script.currency = newElement;

                    EditorUtility.SetDirty(asset);
                }
            }

            DrawDefaultInspector();
        }
    }
}