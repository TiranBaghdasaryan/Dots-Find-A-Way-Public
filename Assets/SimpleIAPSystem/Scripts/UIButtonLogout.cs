/*  This file is part of the "Simple IAP System" project by FLOBUK.
 *  You are only allowed to use these resources if you've bought them from the Unity Asset Store.
 * 	You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * 	otherwise make available to any third party the Service or the Content. */

using UnityEngine;
using UnityEngine.SceneManagement;

namespace SIS
{
    /// <summary>
    /// Simple script that clears current user session data and loads the login scene.
    /// Only necessary with service providers where users can actually login.
    /// </summary>
    public class UIButtonLogout : MonoBehaviour
    {
        //only show the logout button with services supporting it
        #if !PLAYFAB && !XSOLLA
        void Start()
        {
            gameObject.SetActive(false);  
        }
        #endif


        public void Logout()
        {
            #if PLAYFAB
            PlayFabManager.Logout();
            #endif

            #if XSOLLA
            XsollaManager.Logout();
            #endif
        }
    }
}
