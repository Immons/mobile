using System;
using Cirrious.FluentLayouts.Touch;
using Foundation;
using MonoTouch.TTTAttributedLabel;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Net;
using Toggl.Ross.Net;
using Toggl.Ross.Theme;
using Toggl.Ross.Views;
using UIKit;
using XPlatUtils;

namespace Toggl.Ross.ViewControllers
{
    public class SignupViewController : UIViewController
    {
        private UIView inputsContainer;
        private UIView topBorder;
        private UIView middleBorder;
        private UIView bottomBorder;
        private UITextField emailTextField;
        private UITextField passwordTextField;
        private UIButton passwordActionButton;
        private UIButton googleActionButton;
        private TTTAttributedLabel legalLabel;

        public SignupViewController ()
        {
            Title = "SignupTitle".Tr ();
        }

        public override void LoadView ()
        {
            View = new UIView ()
            .Apply (Style.Screen);

            View.Add (inputsContainer = new UIView ().Apply (Style.Signup.InputsContainer));

            inputsContainer.Add (topBorder = new UIView ().Apply (Style.Signup.InputsBorder));

            inputsContainer.Add (emailTextField = new UITextField () {
                Placeholder = "SignupEmailHint".Tr (),
                AutocapitalizationType = UITextAutocapitalizationType.None,
                KeyboardType = UIKeyboardType.EmailAddress,
                ReturnKeyType = UIReturnKeyType.Next,
                ClearButtonMode = UITextFieldViewMode.Always,
                ShouldReturn = HandleShouldReturn,
            } .Apply (Style.Signup.EmailField));
            emailTextField.EditingChanged += OnTextFieldEditingChanged;

            inputsContainer.Add (middleBorder = new UIView ().Apply (Style.Signup.InputsBorder));

            inputsContainer.Add (passwordTextField = new PasswordTextField () {
                Placeholder = "SignupPasswordHint".Tr (),
                AutocapitalizationType = UITextAutocapitalizationType.None,
                AutocorrectionType = UITextAutocorrectionType.No,
                SecureTextEntry = true,
                ReturnKeyType = UIReturnKeyType.Go,
                ShouldReturn = HandleShouldReturn,
            } .Apply (Style.Signup.PasswordField));
            passwordTextField.EditingChanged += OnTextFieldEditingChanged;

            inputsContainer.Add (bottomBorder = new UIView ().Apply (Style.Signup.InputsBorder));

            View.Add (passwordActionButton = new UIButton ()
            .Apply (Style.Signup.SignupButton));
            passwordActionButton.SetTitle ("SignupSignupButtonText".Tr (), UIControlState.Normal);
            passwordActionButton.TouchUpInside += OnPasswordActionButtonTouchUpInside;

            View.Add (googleActionButton = new UIButton ()
            .Apply (Style.Signup.GoogleButton));
            googleActionButton.SetTitle ("SignupGoogleButtonText".Tr (), UIControlState.Normal);
            googleActionButton.TouchUpInside += OnGoogleActionButtonTouchUpInside;

            View.Add (legalLabel = new TTTAttributedLabel () {
                Delegate = new LegalLabelDelegate (),
            } .Apply (Style.Signup.LegalLabel));
            SetLegalText (legalLabel);

            inputsContainer.AddConstraints (
                topBorder.AtTopOf (inputsContainer),
                topBorder.AtLeftOf (inputsContainer),
                topBorder.AtRightOf (inputsContainer),
                topBorder.Height ().EqualTo (1f),

                emailTextField.Below (topBorder),
                emailTextField.AtLeftOf (inputsContainer, 20f),
                emailTextField.AtRightOf (inputsContainer, 10f),
                emailTextField.Height ().EqualTo (42f),

                middleBorder.Below (emailTextField),
                middleBorder.AtLeftOf (inputsContainer, 20f),
                middleBorder.AtRightOf (inputsContainer),
                middleBorder.Height ().EqualTo (1f),

                passwordTextField.Below (middleBorder),
                passwordTextField.AtLeftOf (inputsContainer, 20f),
                passwordTextField.AtRightOf (inputsContainer),
                passwordTextField.Height ().EqualTo (42f),

                bottomBorder.Below (passwordTextField),
                bottomBorder.AtLeftOf (inputsContainer),
                bottomBorder.AtRightOf (inputsContainer),
                bottomBorder.AtBottomOf (inputsContainer),
                bottomBorder.Height ().EqualTo (1f)
            );

            inputsContainer.SubviewsDoNotTranslateAutoresizingMaskIntoConstraints ();

            View.AddConstraints (
                inputsContainer.AtTopOf (View, 80f),
                inputsContainer.AtLeftOf (View),
                inputsContainer.AtRightOf (View),

                passwordActionButton.Below (inputsContainer, 20f),
                passwordActionButton.AtLeftOf (View),
                passwordActionButton.AtRightOf (View),
                passwordActionButton.Height ().EqualTo (60f),

                googleActionButton.Below (passwordActionButton, 5f),
                googleActionButton.AtLeftOf (View),
                googleActionButton.AtRightOf (View),
                googleActionButton.Height ().EqualTo (60f),

                legalLabel.AtBottomOf (View, 30f),
                legalLabel.AtLeftOf (View, 40f),
                legalLabel.AtRightOf (View, 40f)
            );

            View.SubviewsDoNotTranslateAutoresizingMaskIntoConstraints ();

            ResetSignupButtonState ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);

            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "Signup";
        }

        private void OnTextFieldEditingChanged (object sender, EventArgs e)
        {
            ResetSignupButtonState ();
        }

        private void ResetSignupButtonState ()
        {
            var enabled = !IsAuthenticating
                          && !String.IsNullOrWhiteSpace (emailTextField.Text) && emailTextField.Text.Contains ("@")
                          && !String.IsNullOrWhiteSpace (passwordTextField.Text) && passwordTextField.Text.Length >= 6;
            passwordActionButton.Enabled = enabled;
        }

        private static void SetLegalText (TTTAttributedLabel label)
        {
            var template = "SignupLegal".Tr ();
            var arg0 = "SignupToS".Tr ();
            var arg1 = "SignupPrivacy".Tr ();

            var arg0idx = String.Format (template, "{0}", arg1).IndexOf ("{0}", StringComparison.Ordinal);
            var arg1idx = String.Format (template, arg0, "{1}").IndexOf ("{1}", StringComparison.Ordinal);

            label.Text = (NSString)String.Format (template, arg0, arg1);
            label.AddLinkToURL (
                new NSUrl (Phoebe.Build.TermsOfServiceUrl.ToString ()),
                new NSRange (arg0idx, arg0.Length));
            label.AddLinkToURL (
                new NSUrl (Phoebe.Build.PrivacyPolicyUrl.ToString ()),
                new NSRange (arg1idx, arg1.Length));
        }

        private bool HandleShouldReturn (UITextField textField)
        {
            if (textField == emailTextField) {
                passwordTextField.BecomeFirstResponder ();
            } else if (textField == passwordTextField) {
                textField.ResignFirstResponder ();
                TryPasswordSignup ();
            } else {
                return false;
            }
            return true;
        }

        private void OnPasswordActionButtonTouchUpInside (object sender, EventArgs e)
        {
            TryPasswordSignup ();
        }

        private void OnGoogleActionButtonTouchUpInside (object sender, EventArgs e)
        {
            var oauthManager = ServiceContainer.Resolve<OAuthManager> ();
            oauthManager.Authenticated = Finished;
            PresentViewController (oauthManager.UI, true, null);
        }

        private async void TryPasswordSignup ()
        {
            if (IsAuthenticating) {
                return;
            }

            IsAuthenticating = true;

            try {
                var authManager = ServiceContainer.Resolve<AuthManager> ();
                var authRes = await authManager.SignupAsync (emailTextField.Text, passwordTextField.Text);

                if (authRes != AuthResult.Success) {
                    AuthErrorAlert.Show (this, emailTextField.Text, authRes, AuthErrorAlert.Mode.Signup);
                }
            } finally {
                IsAuthenticating = false;
            }
        }

        public void Finished (string token, bool googleFailed)
        {
            DismissViewController (true, null);
            InvokeOnMainThread (async delegate {
                try {
                    if (!googleFailed) {
                        if (token == null) {
                            return;
                        }
                        IsAuthenticating = true;

                        var authManager = ServiceContainer.Resolve<AuthManager> ();
                        var authRes = await authManager.SignupWithGoogleAsync (token);

                        if (authRes != AuthResult.Success) {
                            AuthErrorAlert.Show (this, null, authRes, AuthErrorAlert.Mode.Signup, googleAuth: true);
                        }
                    } else {
                        new UIAlertView (
                            "WelcomeGoogleErrorTitle".Tr (),
                            "WelcomeGoogleErrorMessage".Tr (),
                            null, "WelcomeGoogleErrorOk".Tr (), null).Show ();
                    }
                } finally {
                    IsAuthenticating = false;
                }
            });
        }

        private bool isAuthenticating;

        private bool IsAuthenticating
        {
            get { return isAuthenticating; }
            set {
                isAuthenticating = value;
                emailTextField.Enabled = !isAuthenticating;
                passwordTextField.Enabled = !isAuthenticating;
                passwordActionButton.Enabled = !isAuthenticating;
                googleActionButton.Enabled = !isAuthenticating;

                passwordActionButton.SetTitle ("SignupSignupProgressText".Tr (), UIControlState.Disabled);
            }
        }

        private class LegalLabelDelegate : TTTAttributedLabelDelegate
        {
            public override void DidSelectLinkWithURL (TTTAttributedLabel label, NSUrl url)
            {
                UIApplication.SharedApplication.OpenUrl (url);
            }
        }
    }
}
