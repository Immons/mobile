using System;
using System.Collections.Generic;
using CoreGraphics;
using System.Linq;
using Cirrious.FluentLayouts.Touch;
using CoreAnimation;
using CoreFoundation;
using Foundation;
using UIKit;
using Toggl.Phoebe;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Data.Views;
using Toggl.Phoebe.Data.DataObjects;
using XPlatUtils;
using Toggl.Ross.Theme;
using Toggl.Ross.Views;
using Toggl.Ross.DataSources;

namespace Toggl.Ross.ViewControllers
{
    public class EditTimeEntryViewController : UIViewController
    {
        private enum LayoutVariant {
            Default,
            Description
        }

        private LayoutVariant layoutVariant = LayoutVariant.Default;
        private readonly TimerNavigationController timerController;
        private NSLayoutConstraint[] trackedWrapperConstraints;
        private UIView wrapper;
        private StartStopView startStopView;
        private UIDatePicker datePicker;
        private ProjectClientTaskButton projectButton;
        private TextField descriptionTextField;
        private UIButton tagsButton;
        private LabelSwitchView billableSwitch;
        private UIButton deleteButton;
        private bool hideDatePicker = true;
        private readonly List<NSObject> notificationObjects = new List<NSObject> ();
        private readonly TimeEntryModel model;
        private readonly TimeEntryTagsView tagsView;
        private PropertyChangeTracker propertyTracker = new PropertyChangeTracker ();
        private bool descriptionChanging;
        private bool autoCommitScheduled;
        private int autoCommitId;
        private bool shouldRebindOnAppear;
        private UITableView autoCompletionTableView;
        private UIBarButtonItem autoCopmletionDoneBarButtonItem;

        public EditTimeEntryViewController (TimeEntryModel model)
        {
            this.model = model;

            tagsView = new TimeEntryTagsView (model.Id);

            timerController = new TimerNavigationController (model);
        }

        protected override void Dispose (bool disposing)
        {
            base.Dispose (disposing);

            if (disposing) {
                if (tagsView != null) {
                    tagsView.Updated -= OnTagsUpdated;
                }
                if (propertyTracker != null) {
                    propertyTracker.Dispose ();
                    propertyTracker = null;
                }
            }
        }

        private void ScheduleDescriptionChangeAutoCommit ()
        {
            if (autoCommitScheduled) {
                return;
            }

            var commitId = ++autoCommitId;
            autoCommitScheduled = true;
            DispatchQueue.MainQueue.DispatchAfter (TimeSpan.FromSeconds (1), delegate {
                if (!autoCommitScheduled || commitId != autoCommitId) {
                    return;
                }

                autoCommitScheduled = false;
                CommitDescriptionChanges ();
            });
        }

        private void CancelDescriptionChangeAutoCommit ()
        {
            autoCommitScheduled = false;
        }

        private void CommitDescriptionChanges ()
        {
            if (descriptionChanging) {
                model.Description = descriptionTextField.Text;
                model.SaveAsync ();
            }
            descriptionChanging = false;
            CancelDescriptionChangeAutoCommit ();
        }

        private void DiscardDescriptionChanges ()
        {
            descriptionChanging = false;
            CancelDescriptionChangeAutoCommit ();
        }

        private void OnTagsUpdated (object sender, EventArgs args)
        {
            RebindTags ();
        }

        private void ResetTrackedObservables ()
        {
            if (propertyTracker == null) {
                return;
            }

            propertyTracker.MarkAllStale ();

            if (model != null) {
                propertyTracker.Add (model, HandleTimeEntryPropertyChanged);

                if (model.Project != null) {
                    propertyTracker.Add (model.Project, HandleProjectPropertyChanged);

                    if (model.Project.Client != null) {
                        propertyTracker.Add (model.Project.Client, HandleClientPropertyChanged);
                    }
                }

                if (model.Task != null) {
                    propertyTracker.Add (model.Task, HandleTaskPropertyChanged);
                }
            }

            propertyTracker.ClearStale ();
        }

        private void HandleTimeEntryPropertyChanged (string prop)
        {
            if (prop == TimeEntryModel.PropertyProject
                    || prop == TimeEntryModel.PropertyTask
                    || prop == TimeEntryModel.PropertyStartTime
                    || prop == TimeEntryModel.PropertyStopTime
                    || prop == TimeEntryModel.PropertyState
                    || prop == TimeEntryModel.PropertyIsBillable
                    || prop == TimeEntryModel.PropertyDescription) {
                Rebind ();
            }
        }

        private void HandleProjectPropertyChanged (string prop)
        {
            if (prop == ProjectModel.PropertyClient
                    || prop == ProjectModel.PropertyName
                    || prop == ProjectModel.PropertyColor) {
                Rebind ();
            }
        }

        private void HandleClientPropertyChanged (string prop)
        {
            if (prop == ClientModel.PropertyName) {
                Rebind ();
            }
        }

        private void HandleTaskPropertyChanged (string prop)
        {
            if (prop == TaskModel.PropertyName) {
                Rebind ();
            }
        }

        private void BindStartStopView (StartStopView v)
        {
            v.StartTime = model.StartTime;
            v.StopTime = model.StopTime;
        }

        private void BindDatePicker (UIDatePicker v)
        {
            if (startStopView == null) {
                return;
            }

            var currentValue = v.Date.ToDateTime ().ToUtc ();

            switch (startStopView.Selected) {
            case TimeKind.Start:
                if (currentValue != model.StartTime) {
                    v.SetDate (model.StartTime.ToNSDate (), !v.Hidden);
                }
                break;
            case TimeKind.Stop:
                if (currentValue != model.StopTime) {
                    v.SetDate (model.StopTime.Value.ToNSDate (), !v.Hidden);
                }
                break;
            }
        }

        private void BindProjectButton (ProjectClientTaskButton v)
        {
            var projectName = "EditEntryProjectHint".Tr ();
            var projectColor = Color.White;
            var clientName = String.Empty;
            var taskName = String.Empty;

            if (model.Project != null) {
                projectName = model.Project.Name;
                projectColor = UIColor.Clear.FromHex (model.Project.GetHexColor ());

                if (model.Project.Client != null) {
                    clientName = model.Project.Client.Name;
                }

                if (model.Task != null) {
                    taskName = model.Task.Name;
                }
            }

            v.ProjectColor = projectColor;
            v.ProjectName = projectName;
            v.ClientName = clientName;
            v.TaskName = taskName;
        }

        private void BindDescriptionField (TextField v)
        {
            if (!descriptionChanging && v.Text != model.Description) {
                v.Text = model.Description;
            }

        }

        private void BindTagsButton (UIButton v)
        {
            if (tagsView == null) {
                return;
            }

            // Construct tags attributed strings:
            NSMutableAttributedString text = null;
            foreach (var tag in tagsView.Data) {
                if (String.IsNullOrWhiteSpace (tag)) {
                    continue;
                }

                var chip = NSAttributedString.CreateFrom (new NSTextAttachment () {
                    Image = ServiceContainer.Resolve<TagChipCache> ().Get (tag, v),
                });

                if (text == null) {
                    text = new NSMutableAttributedString (chip);
                } else {
                    text.Append (new NSAttributedString (" ", Style.EditTimeEntry.WithTags));
                    text.Append (chip);
                }
            }

            if (text == null) {
                v.SetAttributedTitle (new NSAttributedString ("EditEntryTagsHint".Tr (), Style.EditTimeEntry.NoTags), UIControlState.Normal);
            } else {
                v.SetAttributedTitle (text, UIControlState.Normal);
            }
        }

        private void BindBillableSwitch (LabelSwitchView v)
        {
            v.Hidden = model.Workspace == null || !model.Workspace.IsPremium;
            v.Switch.On = model.IsBillable;
        }

        private Source autocompletionTableViewSource;

        private void BindAutocompletionTableView (UITableView v)
        {
            autocompletionTableViewSource = new Source (this, v);
            autocompletionTableViewSource.Attach ();
        }

        private void BindAutoCompletionDoneBarButtonItem (UINavigationItem v)
        {
            autoCopmletionDoneBarButtonItem = new UIBarButtonItem (UIBarButtonSystemItem.Done);
            autoCopmletionDoneBarButtonItem.Clicked += (object sender, EventArgs e) => {
                DescriptionEditingMode = false;
            };
            v.SetRightBarButtonItem (autoCopmletionDoneBarButtonItem, true);
        }

        private void UnBindAutoCompletionDoneBarButtonItem (UINavigationItem v)
        {
            if (v.RightBarButtonItem == autoCopmletionDoneBarButtonItem) {
                v.SetRightBarButtonItem (null, true);
                autoCopmletionDoneBarButtonItem = null;
            }
        }

        private class Source : GroupedDataViewSource<TimeEntryData, string, TimeEntryData>
        {
            private readonly static NSString EntryCellId = new NSString ("autocompletionCell");
            private readonly EditTimeEntryViewController controller;

            private readonly SuggestionEntriesView dataView;

            public Source (EditTimeEntryViewController controller, UITableView tableView) : this (controller, tableView, new SuggestionEntriesView (controller.model.Description))
            {
            }

            private Source (EditTimeEntryViewController controller, UITableView tableView, SuggestionEntriesView dataView) : base (tableView, dataView)
            {
                this.dataView = dataView;
                this.controller = controller;
                tableView.RegisterClassForCellReuse (typeof (LogViewController.TimeEntryCell), EntryCellId);
            }


            public void UpdateDescription (string descriptionString)
            {
                Console.WriteLine ("description string " + descriptionString);
                dataView.FilterBySuffix (descriptionString);

            }

            protected override IEnumerable<string> GetSections ()
            {
                return new List<string> () { "" };
            }

            protected override IEnumerable<TimeEntryData> GetRows (string section)
            {
                return dataView.Data;
            }

            public override float EstimatedHeight (UITableView tableView, NSIndexPath indexPath)
            {
                return 60f;
            }

            public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
            {
                return EstimatedHeight (tableView, indexPath);
            }

            public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
            {
                var cell = (LogViewController.TimeEntryCell)tableView.DequeueReusableCell (EntryCellId, indexPath);
                cell.Bind ((TimeEntryModel)GetRow (indexPath));
                return cell;
            }

            public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
            {
                tableView.DeselectRow (indexPath, false);
                TimeEntryModel model;
                model = (TimeEntryModel)GetRow (indexPath);
                controller.ChangeDescription (model.Description);
            }
        }

        private void Rebind ()
        {
            ResetTrackedObservables ();

            var billableHidden = billableSwitch.Hidden;

            startStopView.Apply (BindStartStopView);
            datePicker.Apply (BindDatePicker);
            projectButton.Apply (BindProjectButton);
            descriptionTextField.Apply (BindDescriptionField);
            billableSwitch.Apply (BindBillableSwitch);

            if (billableHidden != billableSwitch.Hidden) {
                ResetWrapperConstraints ();
            }
        }



        private void ResetWrapperConstraints()
        {
            if (trackedWrapperConstraints != null) {
                wrapper.RemoveConstraints (trackedWrapperConstraints);
                trackedWrapperConstraints = null;
            }

            switch (layoutVariant) {
            case LayoutVariant.Default:
                trackedWrapperConstraints = VerticalLinearLayout (wrapper).ToLayoutConstraints ();
                break;
            case LayoutVariant.Description:
                trackedWrapperConstraints = new [] {
                    descriptionTextField.AtTopOf (wrapper),
                    descriptionTextField.AtLeftOf (wrapper),
                    descriptionTextField.AtRightOf (wrapper),
                    descriptionTextField.Height ().EqualTo (60.0f),
                    autoCompletionTableView.Below (descriptionTextField, 5.0f),
                    autoCompletionTableView.AtLeftOf (wrapper),
                    autoCompletionTableView.AtRightOf (wrapper),
                    autoCompletionTableView.AtBottomOf (wrapper)
                } .ToLayoutConstraints ();
                break;
            }

            wrapper.AddConstraints (trackedWrapperConstraints);
        }

        private void RebindTags ()
        {
            tagsButton.Apply (BindTagsButton);
        }

        public override void LoadView ()
        {
            var scrollView = new UIScrollView ().Apply (Style.Screen);

            scrollView.Add (wrapper = new UIView () {
                TranslatesAutoresizingMaskIntoConstraints = false,
            });

            wrapper.Add (startStopView = new StartStopView () {
                TranslatesAutoresizingMaskIntoConstraints = false,
                StartTime = model.StartTime,
                StopTime = model.StopTime,
            } .Apply (BindStartStopView));
            startStopView.SelectedChanged += OnStartStopViewSelectedChanged;

            wrapper.Add (datePicker = new UIDatePicker () {
                TranslatesAutoresizingMaskIntoConstraints = false,
                Hidden = DatePickerHidden,
                Alpha = 0,
            } .Apply (Style.EditTimeEntry.DatePicker).Apply (BindDatePicker));
            datePicker.ValueChanged += OnDatePickerValueChanged;

            wrapper.Add (projectButton = new ProjectClientTaskButton () {
                TranslatesAutoresizingMaskIntoConstraints = false,
            } .Apply (BindProjectButton));
            projectButton.TouchUpInside += OnProjectButtonTouchUpInside;

            wrapper.Add (descriptionTextField = new TextField () {
                TranslatesAutoresizingMaskIntoConstraints = false,
                AttributedPlaceholder = new NSAttributedString (
                    "EditEntryDesciptionTimerHint".Tr (),
                    foregroundColor: Color.Gray
                ),
                ShouldReturn = tf => tf.ResignFirstResponder (),
            } .Apply (Style.EditTimeEntry.DescriptionField).Apply (BindDescriptionField));
            descriptionTextField.EditingChanged += OnDescriptionFieldEditingChanged;
            descriptionTextField.EditingDidEnd += (s, e) => CommitDescriptionChanges ();
            descriptionTextField.ShouldBeginEditing += s => {
                DescriptionEditingMode = true;
                return true;
            };
            descriptionTextField.ShouldEndEditing += s => {
                DescriptionEditingMode = false;
                return true;
            };

            wrapper.Add (tagsButton = new UIButton () {
                TranslatesAutoresizingMaskIntoConstraints = false,
            } .Apply (Style.EditTimeEntry.TagsButton).Apply (BindTagsButton));
            tagsButton.TouchUpInside += OnTagsButtonTouchUpInside;

            wrapper.Add (billableSwitch = new LabelSwitchView () {
                TranslatesAutoresizingMaskIntoConstraints = false,
                Text = "EditEntryBillable".Tr (),
            } .Apply (Style.EditTimeEntry.BillableContainer).Apply (BindBillableSwitch));
            billableSwitch.Label.Apply (Style.EditTimeEntry.BillableLabel);
            billableSwitch.Switch.ValueChanged += OnBillableSwitchValueChanged;

            wrapper.Add (deleteButton = new UIButton () {
                TranslatesAutoresizingMaskIntoConstraints = false,
            } .Apply (Style.EditTimeEntry.DeleteButton));
            deleteButton.SetTitle ("EditEntryDelete".Tr (), UIControlState.Normal);
            deleteButton.TouchUpInside += OnDeleteButtonTouchUpInside;

            wrapper.Add (autoCompletionTableView = new UITableView() {
                TranslatesAutoresizingMaskIntoConstraints = false,
                EstimatedRowHeight = 60.0f
            } .Apply (BindAutocompletionTableView));

            ResetWrapperConstraints ();
            scrollView.AddConstraints (
                wrapper.AtTopOf (scrollView),
                wrapper.AtBottomOf (scrollView),
                wrapper.AtLeftOf (scrollView),
                wrapper.AtRightOf (scrollView),
                wrapper.WithSameWidth (scrollView),
                wrapper.Height ().GreaterThanOrEqualTo ().HeightOf (scrollView).Minus (64f),
                null
            );

            View = scrollView;

            ResetTrackedObservables ();

            DescriptionEditingMode = false;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            timerController.Attach (this);
        }

        private bool changedModeOnce = false;
        private bool descriptionEditingMode__;
        private bool DescriptionEditingMode
        {
            get { return descriptionEditingMode__; }
            set {
                if (value) {
                    layoutVariant = LayoutVariant.Description;
                    NavigationItem.Apply (BindAutoCompletionDoneBarButtonItem);
                } else {
                    descriptionTextField.ResignFirstResponder ();
                    layoutVariant = LayoutVariant.Default;
                    NavigationItem.Apply (UnBindAutoCompletionDoneBarButtonItem);
                }
                ResetWrapperConstraints ();
                UIView.Animate (changedModeOnce ? 0.3f : 0.0f, delegate {
                    SetEditingModeViewsHidden (value);
                    wrapper.LayoutIfNeeded();
                });
                descriptionEditingMode__ = value;
                changedModeOnce = true;
            }
        }

        private void SetEditingModeViewsHidden (bool editingMode)
        {
            tagsButton.Alpha = startStopView.Alpha = projectButton.Alpha = deleteButton.Alpha = editingMode ? 0 : 1;
            autoCompletionTableView.Alpha = 1 - tagsButton.Alpha;
        }

        private void OnDatePickerValueChanged (object sender, EventArgs e)
        {
            switch (startStopView.Selected) {
            case TimeKind.Start:
                model.StartTime = datePicker.Date.ToDateTime ();
                break;
            case TimeKind.Stop:
                model.StopTime = datePicker.Date.ToDateTime ();
                break;
            }

            model.SaveAsync ();
        }

        private void OnProjectButtonTouchUpInside (object sender, EventArgs e)
        {
            var controller = new ProjectSelectionViewController (model);
            NavigationController.PushViewController (controller, true);
        }

        public void ChangeDescription (string newDescription)
        {
            descriptionTextField.Text = newDescription;
            OnDescriptionFieldEditingChanged (this, null);
            DescriptionEditingMode = false;
        }

        private void OnDescriptionFieldEditingChanged (object sender, EventArgs e)
        {
            // Mark description as changed
            descriptionChanging = descriptionTextField.Text != model.Description;

            // Make sure that we're commiting 1 second after the user has stopped typing
            CancelDescriptionChangeAutoCommit ();
            if (descriptionChanging && !DescriptionEditingMode) {
                ScheduleDescriptionChangeAutoCommit ();
            }

            if (autocompletionTableViewSource != null) {
                autocompletionTableViewSource.UpdateDescription (descriptionTextField.Text);
            }
        }

        private void OnTagsButtonTouchUpInside (object sender, EventArgs e)
        {
            var controller = new TagSelectionViewController (model);
            NavigationController.PushViewController (controller, true);
        }

        private void OnBillableSwitchValueChanged (object sender, EventArgs e)
        {
            model.IsBillable = billableSwitch.Switch.On;
            model.SaveAsync ();
        }

        private void OnDeleteButtonTouchUpInside (object sender, EventArgs e)
        {
            var alert = new UIAlertView (
                "EditEntryConfirmTitle".Tr (),
                "EditEntryConfirmMessage".Tr (),
                null,
                "EditEntryConfirmCancel".Tr (),
                "EditEntryConfirmDelete".Tr ());
            alert.Clicked += async (s, ev) => {
                if (ev.ButtonIndex == 1) {
                    NavigationController.PopToRootViewController (true);
                    await model.DeleteAsync ();
                }
            };
            alert.Show ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            timerController.Start ();

            ObserveNotification (UIKeyboard.WillHideNotification, notif => {
                OnKeyboardHeightChanged (0);
            });
            ObserveNotification (UIKeyboard.WillShowNotification, notif => {
                var val = notif.UserInfo.ObjectForKey (UIKeyboard.FrameEndUserInfoKey) as NSValue;
                if (val != null) {
                    OnKeyboardHeightChanged ((int)val.CGRectValue.Height);
                }
            });
            ObserveNotification (UIKeyboard.WillChangeFrameNotification, notif => {
                var val = notif.UserInfo.ObjectForKey (UIKeyboard.FrameEndUserInfoKey) as NSValue;
                if (val != null) {
                    OnKeyboardHeightChanged ((int)val.CGRectValue.Height);
                }
            });

            if (tagsView != null) {
                tagsView.Updated += OnTagsUpdated;
            }
            RebindTags ();

            if (shouldRebindOnAppear) {
                Rebind ();
            } else {
                shouldRebindOnAppear = true;
            }
        }

        private void ObserveNotification (string name, Action<NSNotification> callback)
        {
            var obj = NSNotificationCenter.DefaultCenter.AddObserver (new NSString ( name), callback);
            if (obj != null) {
                notificationObjects.Add (obj);
            }
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);

            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "Edit Time Entry";
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);

            if (tagsView != null) {
                tagsView.Updated -= OnTagsUpdated;
            }

            NSNotificationCenter.DefaultCenter.RemoveObservers (notificationObjects);
            notificationObjects.Clear ();
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);

            timerController.Stop ();
        }

        private void OnKeyboardHeightChanged (int height)
        {
            var scrollView = (UIScrollView)View;

            var inset = scrollView.ContentInset;
            inset.Bottom = height;
            scrollView.ContentInset = inset;

            inset = scrollView.ScrollIndicatorInsets;
            inset.Bottom = height;
            scrollView.ScrollIndicatorInsets = inset;
        }

        private void OnStartStopViewSelectedChanged (object sender, EventArgs e)
        {
            datePicker.Apply (BindDatePicker);
            DatePickerHidden = startStopView.Selected == TimeKind.None;
        }

        private bool DatePickerHidden
        {
            get { return hideDatePicker; }
            set {
                if (hideDatePicker == value) {
                    return;
                }
                hideDatePicker = value;

                if (hideDatePicker) {
                    UIView.AnimateKeyframes (
                        0.4, 0, 0,
                    delegate {
                        UIView.AddKeyframeWithRelativeStartTime (0, 0.4, delegate {
                            datePicker.Alpha = 0;
                        });
                        UIView.AddKeyframeWithRelativeStartTime (0.2, 0.8, delegate {
                            ResetWrapperConstraints();
                            View.LayoutIfNeeded ();
                        });
                    },
                    delegate {
                        if (hideDatePicker) {
                            datePicker.Hidden = true;
                        }
                    }
                    );
                } else {
                    datePicker.Hidden = false;

                    UIView.AnimateKeyframes (
                        0.4, 0, 0,
                    delegate {
                        UIView.AddKeyframeWithRelativeStartTime (0, 0.6, delegate {
                            ResetWrapperConstraints();
                            View.LayoutIfNeeded ();
                        });
                        UIView.AddKeyframeWithRelativeStartTime (0.4, 0.8, delegate {
                            datePicker.Alpha = 1;
                        });
                    },
                    delegate {
                    }
                    );
                }
            }
        }

        private IEnumerable<FluentLayout> VerticalLinearLayout (UIView container)
        {
            UIView prev = null;

            var subviews = container.Subviews.Where (v => !v.Hidden && ! (v == datePicker && DatePickerHidden)).ToList ();
            foreach (var v in subviews) {
                var isLast = subviews [subviews.Count - 1] == v;

                if (prev == null) {
                    yield return v.AtTopOf (container);
                } else if (isLast) {
                    yield return v.Top ().GreaterThanOrEqualTo ().BottomOf (prev).Plus (5f);
                } else {
                    yield return v.Below (prev, 5f);
                }
                yield return v.Height ().EqualTo (60f).SetPriority (UILayoutPriority.DefaultLow);
                yield return v.Height ().GreaterThanOrEqualTo (60f);
                yield return v.AtLeftOf (container);
                yield return v.AtRightOf (container);

                prev = v;
            }

            if (prev != null) {
                yield return prev.AtBottomOf (container);
            }
        }

        private class StartStopView : UIView
        {
            private readonly List<NSLayoutConstraint> trackedConstraints = new List<NSLayoutConstraint>();
            private readonly UILabel startDateLabel;
            private readonly UILabel startTimeLabel;
            private readonly UIButton startDateTimeButton;
            private readonly UILabel stopDateLabel;
            private readonly UILabel stopTimeLabel;
            private readonly UIButton stopDateTimeButton;
            private readonly UIImageView arrowImageView;
            private LayoutVariant viewLayout = LayoutVariant.StartOnly;
            private bool stopTimeHidden = true;
            private DateTime startTime;
            private DateTime? stopTime;
            private TimeKind selectedTime;

            public StartStopView ()
            {
                startDateTimeButton = new UIButton () {
                    TranslatesAutoresizingMaskIntoConstraints = false,
                };
                startDateTimeButton.TouchUpInside += OnStartDateTimeButtonTouchUpInside;
                arrowImageView = new UIImageView () {
                    TranslatesAutoresizingMaskIntoConstraints = false,
                    Image = Image.IconDurationArrow,
                };
                stopDateTimeButton = new UIButton () {
                    TranslatesAutoresizingMaskIntoConstraints = false,
                };
                stopDateTimeButton.TouchUpInside += OnStopDateTimeButtonTouchUpInside;

                ConstructDateTimeView (startDateTimeButton, ref startDateLabel, ref startTimeLabel);
                ConstructDateTimeView (stopDateTimeButton, ref stopDateLabel, ref stopTimeLabel);

                Add (startDateTimeButton);

                StartTime = DateTime.Now - TimeSpan.FromHours (4);
            }

            private void OnStartDateTimeButtonTouchUpInside (object sender, EventArgs e)
            {
                Selected = Selected == TimeKind.Start ? TimeKind.None : TimeKind.Start;
            }

            private void OnStopDateTimeButtonTouchUpInside (object sender, EventArgs e)
            {
                Selected = Selected == TimeKind.Stop ? TimeKind.None : TimeKind.Stop;
            }

            public DateTime StartTime
            {
                get { return startTime; }
                set {
                    if (startTime == value) {
                        return;
                    }

                    startTime = value;
                    var time = startTime.ToLocalTime ();
                    startDateLabel.Text = time.ToLocalizedDateString ();
                    startTimeLabel.Text = time.ToLocalizedTimeString ();
                }
            }

            public DateTime? StopTime
            {
                get { return stopTime; }
                set {
                    if (stopTime == value) {
                        return;
                    }

                    stopTime = value;
                    if (stopTime.HasValue) {
                        var time = stopTime.Value.ToLocalTime ();
                        stopDateLabel.Text = time.ToLocalizedDateString ();
                        stopTimeLabel.Text = time.ToLocalizedTimeString ();
                    }
                    SetStopTimeHidden (stopTime == null, animate: Superview != null);

                    if (stopTime == null && Selected == TimeKind.Stop) {
                        Selected = TimeKind.None;
                    }
                }
            }

            public TimeKind Selected
            {
                get { return selectedTime; }
                set {
                    if (selectedTime == value) {
                        return;
                    }

                    selectedTime = value;

                    if (selectedTime == TimeKind.Start) {
                        startDateLabel.Apply (Style.EditTimeEntry.DateLabelActive);
                        startTimeLabel.Apply (Style.EditTimeEntry.TimeLabelActive);
                    } else {
                        startDateLabel.Apply (Style.EditTimeEntry.DateLabel);
                        startTimeLabel.Apply (Style.EditTimeEntry.TimeLabel);
                    }

                    if (selectedTime == TimeKind.Stop) {
                        stopDateLabel.Apply (Style.EditTimeEntry.DateLabelActive);
                        stopTimeLabel.Apply (Style.EditTimeEntry.TimeLabelActive);
                    } else {
                        stopDateLabel.Apply (Style.EditTimeEntry.DateLabel);
                        stopTimeLabel.Apply (Style.EditTimeEntry.TimeLabel);
                    }

                    var handler = SelectedChanged;
                    if (handler != null) {
                        handler (this, EventArgs.Empty);
                    }
                }
            }

            public event EventHandler SelectedChanged;

            private void SetStopTimeHidden (bool hidden, bool animate)
            {
                if (stopTimeHidden == hidden) {
                    return;
                }
                stopTimeHidden = hidden;

                if (!animate) {
                    ViewLayout = hidden ? LayoutVariant.StartOnly : LayoutVariant.BothCenterAll;
                } else if (hidden) {
                    ViewLayout = LayoutVariant.BothCenterAll;
                    stopDateTimeButton.Alpha = 1;
                    arrowImageView.Alpha = 1;
                    LayoutIfNeeded ();

                    UIView.AnimateKeyframes (
                        0.4, 0,
                        UIViewKeyframeAnimationOptions.CalculationModeCubic | UIViewKeyframeAnimationOptions.BeginFromCurrentState,
                    delegate {
                        UIView.AddKeyframeWithRelativeStartTime (0, 1, delegate {
                            ViewLayout = LayoutVariant.BothCenterStart;
                            LayoutIfNeeded ();
                        });
                        UIView.AddKeyframeWithRelativeStartTime (0, 0.8, delegate {
                            stopDateTimeButton.Alpha = 0;
                            arrowImageView.Alpha = 0;
                        });
                    },
                    delegate {
                        if (ViewLayout == LayoutVariant.BothCenterStart) {
                            ViewLayout = LayoutVariant.StartOnly;
                            LayoutIfNeeded ();
                        }
                    });
                } else {
                    ViewLayout = LayoutVariant.BothCenterStart;
                    stopDateTimeButton.Alpha = 0;
                    arrowImageView.Alpha = 0;
                    LayoutIfNeeded ();

                    UIView.AnimateKeyframes (
                        0.4, 0,
                        UIViewKeyframeAnimationOptions.CalculationModeCubic | UIViewKeyframeAnimationOptions.BeginFromCurrentState,
                    delegate {
                        UIView.AddKeyframeWithRelativeStartTime (0, 1, delegate {
                            ViewLayout = LayoutVariant.BothCenterAll;
                            LayoutIfNeeded ();
                        });
                        UIView.AddKeyframeWithRelativeStartTime (0.2, 1, delegate {
                            stopDateTimeButton.Alpha = 1;
                            arrowImageView.Alpha = 1;
                        });
                    },
                    delegate {
                    });
                }
            }

            private static void ConstructDateTimeView (UIView view, ref UILabel dateLabel, ref UILabel timeLabel)
            {
                view.Add (dateLabel = new UILabel ().Apply (Style.EditTimeEntry.DateLabel));
                view.Add (timeLabel = new UILabel ().Apply (Style.EditTimeEntry.TimeLabel));
                view.AddConstraints (
                    dateLabel.AtTopOf (view, 10f),
                    dateLabel.AtLeftOf (view, 10f),
                    dateLabel.AtRightOf (view, 10f),

                    timeLabel.Below (dateLabel, 2f),
                    timeLabel.AtBottomOf (view, 10f),
                    timeLabel.AtLeftOf (view, 10f),
                    timeLabel.AtRightOf (view, 10f)
                );
                view.SubviewsDoNotTranslateAutoresizingMaskIntoConstraints ();
            }

            public override void UpdateConstraints ()
            {
                if (trackedConstraints.Count > 0) {
                    RemoveConstraints (trackedConstraints.ToArray());
                    trackedConstraints.Clear ();
                }

                switch (viewLayout) {
                case LayoutVariant.StartOnly:
                    arrowImageView.RemoveFromSuperview ();
                    stopDateTimeButton.RemoveFromSuperview ();

                    trackedConstraints.AddRange (new [] {
                        startDateTimeButton.WithSameCenterX (this),
                        startDateTimeButton.WithSameCenterY (this),
                        startDateTimeButton.AtTopOf (this, 10f),
                        startDateTimeButton.AtBottomOf (this, 10f)
                    } .ToLayoutConstraints());
                    break;
                case LayoutVariant.BothCenterStart:
                    AddSubview (arrowImageView);
                    AddSubview (stopDateTimeButton);

                    trackedConstraints.AddRange (new [] {
                        startDateTimeButton.WithSameCenterX (this),
                        startDateTimeButton.WithSameCenterY (this),
                        startDateTimeButton.AtTopOf (this, 10f),
                        startDateTimeButton.AtBottomOf (this, 10f),

                        arrowImageView.WithSameCenterY (startDateTimeButton),
                        arrowImageView.ToRightOf (startDateTimeButton, 10f),
                        arrowImageView.ToLeftOf (stopDateTimeButton, 10f),

                        stopDateTimeButton.AtTopOf (this, 10f),
                        stopDateTimeButton.AtBottomOf (this, 10f)
                    } .ToLayoutConstraints());
                    break;
                case LayoutVariant.BothCenterAll:
                default:
                    AddSubview (arrowImageView);
                    AddSubview (stopDateTimeButton);

                    trackedConstraints.AddRange (new [] {
                        startDateTimeButton.AtTopOf (this, 10f),
                        startDateTimeButton.AtBottomOf (this, 10f),

                        arrowImageView.WithSameCenterX (this),
                        arrowImageView.WithSameCenterY (this),
                        arrowImageView.ToRightOf (startDateTimeButton, 10f),
                        arrowImageView.ToLeftOf (stopDateTimeButton, 10f),

                        stopDateTimeButton.AtTopOf (this, 10f),
                        stopDateTimeButton.AtBottomOf (this, 10f)
                    } .ToLayoutConstraints());
                    break;
                }

                AddConstraints (trackedConstraints.ToArray ());

                base.UpdateConstraints ();
            }

            private LayoutVariant ViewLayout
            {
                get { return viewLayout; }
                set {
                    if (viewLayout == value) {
                        return;
                    }
                    viewLayout = value;
                    SetNeedsUpdateConstraints ();
                    SetNeedsLayout ();
                }
            }

            private enum LayoutVariant {
                StartOnly,
                BothCenterStart,
                BothCenterAll
            }

            [Export ("requiresConstraintBasedLayout")]
            public static new bool RequiresConstraintBasedLayout ()
            {
                return true;
            }
        }

        private enum TimeKind {
            None,
            Start,
            Stop
        }

        private class ProjectClientTaskButton : UIButton
        {
            private readonly List<NSLayoutConstraint> trackedConstraints = new List<NSLayoutConstraint>();
            private readonly UIView container;
            private readonly UILabel projectLabel;
            private readonly UILabel clientLabel;
            private readonly UILabel taskLabel;

            public ProjectClientTaskButton ()
            {
                Add (container = new UIView () {
                    TranslatesAutoresizingMaskIntoConstraints = false,
                    UserInteractionEnabled = false,
                });

                var maskLayer = new CAGradientLayer () {
                    AnchorPoint = CGPoint.Empty,
                    StartPoint = new CGPoint (0.0f, 0.0f),
                    EndPoint = new CGPoint (1.0f, 0.0f),
                    Colors = new [] {
                        UIColor.FromWhiteAlpha (1, 1).CGColor,
                        UIColor.FromWhiteAlpha (1, 1).CGColor,
                        UIColor.FromWhiteAlpha (1, 0).CGColor,
                    },
                    Locations = new [] {
                        NSNumber.FromFloat (0f),
                        NSNumber.FromFloat (0.9f),
                        NSNumber.FromFloat (1f),
                    },
                };
                container.Layer.Mask = maskLayer;

                container.Add (projectLabel = new UILabel () {
                    TranslatesAutoresizingMaskIntoConstraints = false,
                } .Apply (Style.EditTimeEntry.ProjectLabel));
                container.Add (clientLabel = new UILabel () {
                    TranslatesAutoresizingMaskIntoConstraints = false,
                } .Apply (Style.EditTimeEntry.ClientLabel));
                container.Add (taskLabel = new UILabel () {
                    TranslatesAutoresizingMaskIntoConstraints = false,
                } .Apply (Style.EditTimeEntry.TaskLabel));
            }

            public override void UpdateConstraints ()
            {
                if (trackedConstraints.Count > 0) {
                    RemoveConstraints (trackedConstraints.ToArray());
                    trackedConstraints.Clear ();
                }

                trackedConstraints.AddRange (new FluentLayout[] {
                    container.AtLeftOf (this, 15f),
                    container.WithSameCenterY (this),

                    projectLabel.AtTopOf (container),
                    projectLabel.AtLeftOf (container),
                    null
                } .ToLayoutConstraints());

                if (!clientLabel.Hidden) {
                    var baselineOffset = (float)Math.Floor (projectLabel.Font.Descender - clientLabel.Font.Descender);
                    trackedConstraints.AddRange (new FluentLayout[] {
                        clientLabel.AtTopOf (container, -baselineOffset),
                        clientLabel.AtRightOf (container),
                        clientLabel.ToRightOf (projectLabel, 5f),
                        clientLabel.AtBottomOf (projectLabel, baselineOffset),
                        null
                    } .ToLayoutConstraints());
                } else {
                    trackedConstraints.AddRange (new FluentLayout[] {
                        projectLabel.AtRightOf (container),
                        null
                    } .ToLayoutConstraints());
                }

                if (!taskLabel.Hidden) {
                    trackedConstraints.AddRange (new FluentLayout[] {
                        taskLabel.Below (projectLabel, 3f),
                        taskLabel.AtLeftOf (container),
                        taskLabel.AtRightOf (container),
                        taskLabel.AtBottomOf (container),
                        null
                    } .ToLayoutConstraints());
                } else {
                    trackedConstraints.AddRange (new FluentLayout[] {
                        projectLabel.AtBottomOf (container),
                        null
                    } .ToLayoutConstraints());
                }

                AddConstraints (trackedConstraints.ToArray ());

                base.UpdateConstraints ();
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();

                // Update fade mask position:
                var bounds = container.Frame;
                bounds.Width = Frame.Width - bounds.X;
                container.Layer.Mask.Bounds = bounds;
            }

            public string ProjectName
            {
                get { return projectLabel.Text; }
                set { projectLabel.Text = value; }
            }

            public string ClientName
            {
                get { return clientLabel.Text; }
                set {
                    if (clientLabel.Text == value) {
                        return;
                    }

                    var visibilityChanged = String.IsNullOrWhiteSpace (clientLabel.Text) != String.IsNullOrWhiteSpace (value);
                    clientLabel.Text = value;

                    if (visibilityChanged) {
                        SetNeedsUpdateConstraints ();
                        clientLabel.Hidden = String.IsNullOrWhiteSpace (value);
                    }
                }
            }

            public string TaskName
            {
                get { return taskLabel.Text; }
                set {
                    if (taskLabel.Text == value) {
                        return;
                    }

                    var visibilityChanged = String.IsNullOrWhiteSpace (taskLabel.Text) != String.IsNullOrWhiteSpace (value);
                    taskLabel.Text = value;

                    if (visibilityChanged) {
                        SetNeedsUpdateConstraints ();
                        taskLabel.Hidden = String.IsNullOrWhiteSpace (value);
                    }
                }
            }

            public UIColor ProjectColor
            {
                set {
                    if (value == Color.White) {
                        projectLabel.Apply (Style.EditTimeEntry.ProjectHintLabel);
                        SetBackgroundImage (Color.White.ToImage (), UIControlState.Normal);
                        SetBackgroundImage (Color.LightestGray.ToImage (), UIControlState.Highlighted);
                    } else {
                        projectLabel.Apply (Style.EditTimeEntry.ProjectLabel);
                        SetBackgroundImage (value.ToImage (), UIControlState.Normal);
                        SetBackgroundImage (null, UIControlState.Highlighted);
                    }
                }
            }

            [Export ("requiresConstraintBasedLayout")]
            public static new bool RequiresConstraintBasedLayout ()
            {
                return true;
            }
        }
    }
}
