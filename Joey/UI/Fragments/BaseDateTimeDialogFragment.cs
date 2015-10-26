using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Text.Format;
using Android.Util;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Joey.UI.Fragments
{
    public abstract class BaseDateTimeDialogFragment : BaseDialogFragment, DatePicker.IOnDateChangedListener
    {
        private static readonly string TimeEntryIdArgument = "com.toggl.timer.time_entry_id";

        protected BaseDateTimeDialogFragment (ITimeEntryModel model)
        {
            var args = new Bundle ();
            args.PutString (TimeEntryIdArgument, model.Id.ToString ());

            Arguments = args;
        }

        protected BaseDateTimeDialogFragment ()
        {
        }

        protected BaseDateTimeDialogFragment (IntPtr jref, Android.Runtime.JniHandleOwnership xfer) : base (jref, xfer)
        {
        }

        private Guid TimeEntryId
        {
            get {
                var id = Guid.Empty;
                if (Arguments != null) {
                    Guid.TryParse (Arguments.GetString (TimeEntryIdArgument), out id);
                }
                return id;
            }
        }

        private TimeEntryModel model;
        private bool viewsSetup;
        private bool modelLoaded;

        protected RadioGroup TabsRadioGroup { get; private set; }

        protected RadioButton TimeTabRadioButton { get; private set; }

        protected RadioButton DateTabRadioButton { get; private set; }

        protected TimePicker TimePicker { get; private set; }

        protected DatePicker DatePicker { get; private set; }

        public override void OnCreate (Bundle state)
        {
            base.OnCreate (state);

            LoadData ();
        }

        private async void LoadData ()
        {
            model = new TimeEntryModel (TimeEntryId);
            await model.LoadAsync ();
            if (model.Workspace == null || model.Workspace.Id == Guid.Empty) {
                Dismiss ();
            } else {
                modelLoaded = true;
                SetupViews ();
                Rebind ();
            }
        }

        public override Dialog OnCreateDialog (Bundle state)
        {
            var view = LayoutInflater.From (Activity)
                       .Inflate (Resource.Layout.ChangeTimeEntryStartTimeDialogFragment, null);
            TabsRadioGroup = view.FindViewById<RadioGroup> (Resource.Id.TabsRadioGroup);
            TimeTabRadioButton = view.FindViewById<RadioButton> (Resource.Id.TimeTabRadioButton).SetFont (Font.Roboto);
            DateTabRadioButton = view.FindViewById<RadioButton> (Resource.Id.DateTabRadioButton).SetFont (Font.Roboto);
            TimePicker = view.FindViewById<TimePicker> (Resource.Id.TimePicker);
            DatePicker = view.FindViewById<DatePicker> (Resource.Id.DatePicker);

            // WORKAROUND: Without these two lines the app will crash on rotation. See #258.
            TimePicker.SaveFromParentEnabled = false;
            TimePicker.SaveEnabled = true;

            TabsRadioGroup.CheckedChange += OnTabsRadioGroupCheckedChange;

            SetupViews ();
            Rebind ();

            var dia = new AlertDialog.Builder (Activity)
            .SetTitle (DialogTitleId ())
            .SetView (view)
            .SetPositiveButton (Resource.String.ChangeTimeEntryStartTimeDialogOk, OnOkButtonClicked)
            .Create ();

            return dia;
        }

        private void SetupViews ()
        {
            if (viewsSetup || !modelLoaded || TimePicker == null) {
                return;
            }

            viewsSetup = true;

            var time = GetInitialTime ();
            var date = GetInitialDate ();

            TimePicker.SetIs24HourView (new Java.Lang.Boolean (
                                            DateFormat.Is24HourFormat (ServiceContainer.Resolve<Context> ())));
            TimePicker.CurrentHour = new Java.Lang.Integer (time.Hour);
            TimePicker.CurrentMinute = new Java.Lang.Integer (time.Minute);
            TimePicker.TimeChanged += OnTimePickerTimeChanged;

            DatePicker.Init (date.Year, date.Month - 1, date.Day, this);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop) {
                var userData = ServiceContainer.Resolve<AuthManager> ().User;
                DatePicker.FirstDayOfWeek =  ((int) userData.StartOfWeek) + 1; // FirstDayOfWeek must be between 1 - 7, Our days go from 0 - 6.
            }
        }

        private void OnTabsRadioGroupCheckedChange (object sender, RadioGroup.CheckedChangeEventArgs e)
        {
            Rebind ();
        }

        private void Rebind ()
        {
            if (TabsRadioGroup == null) {
                return;
            }

            if (TabsRadioGroup.CheckedRadioButtonId == TimeTabRadioButton.Id) {
                TimeTabRadioButton.SetTextSize (ComplexUnitType.Dip, 18);
                DateTabRadioButton.SetTextSize (ComplexUnitType.Dip, 14);
                TimePicker.Visibility = ViewStates.Visible;
                DatePicker.Visibility = ViewStates.Gone;
            } else {
                TimeTabRadioButton.SetTextSize (ComplexUnitType.Dip, 14);
                DateTabRadioButton.SetTextSize (ComplexUnitType.Dip, 18);
                TimePicker.Visibility = ViewStates.Gone;
                DatePicker.Visibility = ViewStates.Visible;
            }

            var dateTime = DateTime;
            TimeTabRadioButton.Text = dateTime.ToDeviceTimeString ();
            DateTabRadioButton.Text = dateTime.ToDeviceDateString ();
        }

        private void OnTimePickerTimeChanged (object sender, TimePicker.TimeChangedEventArgs e)
        {
            Rebind ();
        }

        void DatePicker.IOnDateChangedListener.OnDateChanged (DatePicker view, int year, int monthOfYear, int dayOfMonth)
        {
            Rebind ();
        }

        private async void OnOkButtonClicked (object sender, DialogClickEventArgs args)
        {
            if (modelLoaded && model != null) {
                UpdateDate (DateTime);
            }
        }

        private DateTime DateTime
        {
            get {
                return DateTime.SpecifyKind (DatePicker.DateTime
                                             .AddHours (TimePicker.CurrentHour.IntValue ())
                                             .AddMinutes (TimePicker.CurrentMinute.IntValue ()), DateTimeKind.Local);
            }
        }

        protected TimeEntryModel Model
        {
            get { return model; }
        }

        protected abstract DateTime GetInitialTime ();

        protected abstract DateTime GetInitialDate ();

        protected abstract void UpdateDate (DateTime dateTime);

        protected abstract int DialogTitleId ();
    }
}
