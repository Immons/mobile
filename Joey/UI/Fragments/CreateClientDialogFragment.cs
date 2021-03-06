﻿using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Text;
using Android.Widget;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.ViewModels;

namespace Toggl.Joey.UI.Fragments
{
    public class CreateClientDialogFragment : BaseDialogFragment
    {
        private EditText nameEditText;
        private Button positiveButton;
        private CreateClientViewModel viewModel;
        private ProjectModel project;

        public CreateClientDialogFragment ()
        {
        }

        public CreateClientDialogFragment (IntPtr jref, Android.Runtime.JniHandleOwnership xfer) : base (jref, xfer)
        {
        }

        public CreateClientDialogFragment (ProjectModel project)
        {
            this.project = project;
        }

        public override async void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            if (viewModel == null) {
                viewModel = new CreateClientViewModel (project.Workspace.Id);
            }
            viewModel.OnIsLoadingChanged += OnModelLoaded;
            await viewModel.Init ();

            ValidateClientName ();
        }

        private void OnModelLoaded (object sender, EventArgs e)
        {
            if (!viewModel.IsLoading) {
                if (viewModel == null) {
                    Dismiss ();
                }
            }
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            nameEditText = new EditText (Activity);
            nameEditText.SetHint (Resource.String.CreateClientDialogHint);
            nameEditText.InputType = InputTypes.TextFlagCapSentences;
            nameEditText.TextChanged += OnNameEditTextTextChanged;

            return new AlertDialog.Builder (Activity)
                   .SetTitle (Resource.String.CreateClientDialogTitle)
                   .SetView (nameEditText)
                   .SetPositiveButton (Resource.String.CreateClientDialogOk, OnPositiveButtonClicked)
                   .Create ();
        }

        public override void OnStart ()
        {
            base.OnStart ();
            positiveButton = ((AlertDialog)Dialog).GetButton ((int)DialogButtonType.Positive);
            ValidateClientName ();
        }

        private void OnNameEditTextTextChanged (object sender, TextChangedEventArgs e)
        {
            ValidateClientName ();
        }

        private async void OnPositiveButtonClicked (object sender, DialogClickEventArgs e)
        {
            await viewModel.AssignClient (nameEditText.Text, project);
        }

        private void ValidateClientName ()
        {
            if (positiveButton == null || nameEditText == null) {
                return;
            }

            var valid = true;
            var name = nameEditText.Text;

            if (String.IsNullOrWhiteSpace (name)) {
                valid = false;
            }

            positiveButton.Enabled = valid;
        }

        public override void OnDestroy ()
        {
            if (viewModel != null) {
                viewModel.OnIsLoadingChanged += OnModelLoaded;
                viewModel.Dispose ();
            }

            base.OnDestroy ();
        }
    }
}
