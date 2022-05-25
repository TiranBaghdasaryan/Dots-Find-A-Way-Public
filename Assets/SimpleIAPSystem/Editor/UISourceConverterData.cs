/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 *  You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 *  otherwise make available to any third party the Service or the Content. */

using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace SIS
{
    public class UISourceConverterData
    {
        public static readonly string[] uiNamespaces =
        {
            "UnityEngine.UI",
            "TMPro"
        };

        public static readonly string[] uiSourceFiles =
        {
            "CurrencyContainer",
            "ShopItem2D",
            "UIButtonCoupon",
            "UILogin",
            "UIShopFeedback",
            "UIWindowShowReward",
            "ShopItemEditor"
        };

        public static readonly string[] uiSourceVarsTMPro =
        {
            " Text : TextMeshProUGUI ",
            " InputField : TMP_InputField "
        };


        public static void Convert(UIAssetPlugin target)
        {
            //get all files
            string[] files = GetFiles();

            List<string> oldVars = new List<string>();
            List<string> newVars = new List<string>();

            switch(target)
            {
                case UIAssetPlugin.UnityUI:
                    oldVars.AddRange(uiSourceVarsTMPro);
                    break;

                case UIAssetPlugin.TextMeshPro:
                    newVars.AddRange(uiSourceVarsTMPro);
                    break;
            }
           
            foreach (string filePath in files)
            {
                //cache file content
                List<string> content = File.ReadAllLines(filePath).ToList();

                //remove previous namespace directives
                //just always keep Unity UI in case of other UI references
                for (int i = 1; i < uiNamespaces.Length; i++)
                {
                    content.Remove("using " + uiNamespaces[i] + ";");
                }

                //add new namespace at the top
                int namespaceIndex = content.FindIndex(x => x == "namespace SIS");
                if (namespaceIndex < 2) namespaceIndex = 0;
                if (!content.Contains("using " + uiNamespaces[(int)target] + ";"))
                    content.Insert(namespaceIndex - 1, "using " + uiNamespaces[(int)target] + ";");

                //revert previous code changes, if any
                for (int i = 0; i < oldVars.Count; i++)
                {
                    string[] var = oldVars[i].Split(':');

                    for(int j = 0; j < content.Count; j++)
                    {
                        if(content[j].Contains(var[1]))
                            content[j] = content[j].Replace(var[1], var[0]);
                    }
                }

                //apply new code changes, if any
                for (int i = 0; i < newVars.Count; i++)
                {
                    string[] var = newVars[i].Split(':');

                    for (int j = 0; j < content.Count; j++)
                    {
                        if (content[j].Contains(var[0]))
                            content[j] = content[j].Replace(var[0], var[1]);
                    }
                }

                //write the modified content back to the file
                File.WriteAllLines(filePath, content);
            }

            AssetDatabase.Refresh();
        }


        static string[] GetFiles([System.Runtime.CompilerServices.CallerFilePath] string scriptPath = "")
        {
            string folderPath = Directory.GetParent(scriptPath).Parent.FullName;
            string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories).Where(s => s.EndsWith(".cs")).ToArray();
            List<string> filter = new List<string>();

            foreach(string filePath in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                if (uiSourceFiles.Contains(fileName))
                    filter.Add(filePath);
            }

            return filter.ToArray();
        }
    }
}