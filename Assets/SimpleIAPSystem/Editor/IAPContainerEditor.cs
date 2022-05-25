/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using UnityEditor;
using System.Linq;

namespace SIS
{
    [CustomEditor(typeof(IAPContainer))]
    public class IAPContainerEditor : Editor
    {
        private IAPContainer script;
        private IAPScriptableObject asset;

        private string[] dropdownNames;
        private int dropdownIndex = 0;


        void OnEnable()
        {
            script = (IAPContainer)target;
            asset = IAPScriptableObject.GetOrCreateSettings();
            dropdownNames = new string[] { "Choose Category... " }.Union(asset.categoryList.Select(element => element.ID)).ToArray();
            
            if(script.category != null)
                dropdownIndex = 1 + asset.categoryList.FindIndex(x => x.referenceID == script.category.referenceID);
        }

        public override void OnInspectorGUI()
        {
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                dropdownIndex = EditorGUILayout.Popup("Category", dropdownIndex, dropdownNames);
                if (check.changed)
                {
                    Undo.RecordObject(script, "Change Category Dropdown");

                    IAPCategory newElement = dropdownIndex == 0 ? null : asset.categoryList.Single(x => x.ID == dropdownNames[dropdownIndex]);
                    script.category = newElement;

                    EditorUtility.SetDirty(asset);
                }
            }

            DrawDefaultInspector();
        }
    }
}