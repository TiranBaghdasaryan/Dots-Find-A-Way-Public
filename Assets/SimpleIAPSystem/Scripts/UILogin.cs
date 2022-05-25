using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace SIS
{
    /// <summary>
    /// Sample UI script for registering new accounts and logging in with PlayFab.
    /// The current implementation makes use of email addresses for creating new users.
    /// </summary>
    public class UILogin : MonoBehaviour
    {
        /// <summary>
        /// Scene to load immediately after successfully logging in.
        /// </summary>
        public string nextScene;

        /// <summary>
        /// Loading screen game object to activate between login attempts.
        /// </summary>
        public GameObject loadingScreen;

        /// <summary>
        /// Email address field to register or log in.
        /// </summary>
        public InputField emailField;

        /// <summary>
        /// Password field to register or log in.
        /// </summary>
        public InputField passwordField;

        /// <summary>
        /// Error text displayed in case of login issues.
        /// </summary>
        public Text errorText;

        /// <summary>
        /// Button for using login via device identifier. Not supported on all services.
        /// </summary>
        public GameObject deviceLoginButton;

        /// <summary>
        /// Button for using login via social provider. Not supported on all services.
        /// </summary>
        public GameObject socialLoginButton;

        //PlayerPref key used for storing the latest entered email address
        private const string emailPref = "AccountEmail";



        void Awake()
        {
            #if !PLAYFAB && !XSOLLA
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
            #endif

            #if !PLAYFAB
            deviceLoginButton.SetActive(false);
            #endif

            #if !XSOLLA
            socialLoginButton.SetActive(false);
            #endif
        }


        //pre-load login values
        void Start()
        {
            if (PlayerPrefs.HasKey(emailPref))
                emailField.text = PlayerPrefs.GetString(emailPref);
            
            #if PLAYFAB_FACEBOOK
                loadingScreen.SetActive(true);
            #endif
        }


        void OnEnable()
        {
            #if PLAYFAB
            PlayFabManager.loginSucceededEvent += OnLoggedIn;
            PlayFabManager.loginFailedEvent += OnLoginFail;
            #endif

            #if XSOLLA
            XsollaManager.loginSucceededEvent += OnLoggedIn;
            XsollaManager.loginFailedEvent += OnLoginFail;
            #endif
        }


        void OnDisable()
        {
            #if PLAYFAB
            PlayFabManager.loginSucceededEvent -= OnLoggedIn;
            PlayFabManager.loginFailedEvent -= OnLoginFail;
            #endif

            #if XSOLLA
            XsollaManager.loginSucceededEvent -= OnLoggedIn;
            XsollaManager.loginFailedEvent -= OnLoginFail;
            #endif
        }


        #if PLAYFAB
        //loads the desired scene immediately after loggin in
        private void OnLoggedIn(PlayFab.ClientModels.LoginResult result)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
        }
        #endif


        #if XSOLLA
        private void OnLoggedIn(Xsolla.Login.UserInfo info)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
        }
        #endif


        //hides the loading screen in case of failed login, so the user can try again
        private void OnLoginFail(string error)
        {
            #if XSOLLA
            //working around an error passed in every time on browser close, even after successful auth
            if (!Xsolla.Login.XsollaLogin.Instance.Token.IsNullOrEmpty())
                return;
            #endif

            loadingScreen.SetActive(false);
            errorText.text = error;
        }


        /// <summary>
        /// Registers a new account with PlayFab, mapped to a UI button.
        /// </summary>
        public void RegisterAccount()
        {
            string inputError = Validate();
            if (!string.IsNullOrEmpty(inputError))
            {
                errorText.text = inputError;
                return;
            }

            loadingScreen.SetActive(true);
            PlayerPrefs.SetString(emailPref, emailField.text);

            #if PLAYFAB
            PlayFabManager.RegisterAccount(emailField.text, passwordField.text);
            #elif XSOLLA
            XsollaManager.RegisterAccount(emailField.text, passwordField.text);
            #endif
        }


        /// <summary>
        /// Tries to login via email, mapped to a UI button.
        /// </summary>
        public void LoginWithEmail()
        {
            string inputError = Validate();
            if (!string.IsNullOrEmpty(inputError))
            {
                errorText.text = inputError;
                return;
            }

            loadingScreen.SetActive(true);
            PlayerPrefs.SetString(emailPref, emailField.text);

            #if PLAYFAB
            PlayFabManager.LoginWithEmail(emailField.text, passwordField.text);
            #elif XSOLLA
            XsollaManager.LoginWithEmail(emailField.text, passwordField.text);
            #endif
        }


        public void LoginWithSocial(string providerName)
        {
            loadingScreen.SetActive(true);

            #if XSOLLA
            XsollaManager.LoginWithSocial(providerName);
            #endif
        }


        public void LoginWithDevice()
        {
            #if PLAYFAB
            if (PlayFabManager.GetInstance())
                PlayFabManager.GetInstance().LoginWithDevice();
            #endif
        }


        /// <summary>
        /// Requests a new password, mapped to a UI button.
        /// </summary>
        public void ForgotPassword()
        {
            errorText.text = "";
            if (emailField.text.Length == 0)
            {
                errorText.text = "Please enter your email and retry.";
                return;
            }
            
            #if PLAYFAB
            PlayFabManager.ForgotPassword(emailField.text);
            #elif XSOLLA
            XsollaManager.ForgotPassword(emailField.text);
            #endif
        }


        private string Validate()
        {
            if (emailField.text.Length == 0|| passwordField.text.Length == 0)
            {
                return "All fields are required.";
            }

            if (passwordField.text.Length <= 5)
            {
                return "Password must be longer than 5 characters.";
            }

            string emailPattern = "^[a-zA-Z0-9-_.+]+[@][a-zA-Z0-9-_.]+[.][a-zA-Z]+$";
            Regex regex = new Regex(emailPattern);
            if(!regex.IsMatch(emailField.text))
            {
                return "Invalid email.";
            }

            return string.Empty;
        }
    }
}
