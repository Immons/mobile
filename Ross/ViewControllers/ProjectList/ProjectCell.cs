using System;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Views;
using Toggl.Ross.Theme;
using Toggl.Ross.Views;
using UIKit;

namespace Toggl.Ross.ViewControllers.ProjectList
{
    public class ProjectCell : UITableViewCell
    {
        private const float CellSpacing = 4f;
        private UIView textContentView;
        private UILabel projectLabel;
        private UILabel clientLabel;
        private UIButton tasksButton;
        //        private ProjectModel model;

        public ProjectCell (IntPtr handle)
        : base (handle)
        {
            this.Apply (Style.Screen);
            BackgroundView = new UIView();

            ContentView.Add (textContentView = new UIView());
            ContentView.Add (tasksButton = new UIButton().Apply (Style.ProjectList.TasksButtons));
            textContentView.Add (projectLabel = new UILabel().Apply (Style.ProjectList.ProjectLabel));
            textContentView.Add (clientLabel = new UILabel().Apply (Style.ProjectList.ClientLabel));

            var maskLayer = new CAGradientLayer() {
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
            textContentView.Layer.Mask = maskLayer;

            tasksButton.TouchUpInside += OnTasksButtonTouchUpInside;
        }

        private void OnTasksButtonTouchUpInside (object sender, EventArgs e)
        {
            var cb = ToggleTasks;
            if (cb != null) {
                cb();
            }
        }

        public Action ToggleTasks { get; set; }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();

            var contentFrame = new CGRect (0, CellSpacing / 2, Frame.Width, Frame.Height - CellSpacing);
            SelectedBackgroundView.Frame = BackgroundView.Frame = ContentView.Frame = contentFrame;

            if (!tasksButton.Hidden) {
                var virtualWidth = contentFrame.Height;
                var buttonWidth = tasksButton.CurrentBackgroundImage.Size.Width;
                var extraPadding = (virtualWidth - buttonWidth) / 2f;
                tasksButton.Frame = new CGRect (
                    contentFrame.Width - virtualWidth + extraPadding, extraPadding,
                    buttonWidth, buttonWidth);
                contentFrame.Width -= virtualWidth;
            }

            contentFrame.X += 13f;
            contentFrame.Width -= 13f;
            textContentView.Frame = contentFrame;
            textContentView.Layer.Mask.Bounds = contentFrame;

            contentFrame = new CGRect (CGPoint.Empty, contentFrame.Size);

            if (clientLabel.Hidden) {
                // Only display single item, so make it fill the whole text frame
                var bounds = GetBoundingRect (projectLabel);
                projectLabel.Frame = new CGRect (
                    x: 0,
                    y: (contentFrame.Height - bounds.Height + projectLabel.Font.Descender) / 2f,
                    width: contentFrame.Width,
                    height: bounds.Height
                );
            } else {
                // Carefully craft the layout
                var bounds = GetBoundingRect (projectLabel);
                projectLabel.Frame = new CGRect (
                    x: 0,
                    y: (contentFrame.Height - bounds.Height + projectLabel.Font.Descender) / 2f,
                    width: bounds.Width,
                    height: bounds.Height
                );

                const float clientLeftMargin = 7.5f;
                bounds = GetBoundingRect (clientLabel);
                clientLabel.Frame = new CGRect (
                    x: projectLabel.Frame.X + projectLabel.Frame.Width + clientLeftMargin,
                    y: (float)Math.Floor (projectLabel.Frame.Y + projectLabel.Font.Ascender - clientLabel.Font.Ascender),
                    width: bounds.Width,
                    height: bounds.Height
                );
            }
        }

        private static CGRect GetBoundingRect (UILabel view)
        {
            var attrs = new UIStringAttributes() {
                Font = view.Font,
            };
            var rect = ((NSString) (view.Text ?? String.Empty)).GetBoundingRect (
                           new CGSize (Single.MaxValue, Single.MaxValue),
                           NSStringDrawingOptions.UsesLineFragmentOrigin,
                           attrs, null);
            rect.Height = (float)Math.Ceiling (rect.Height);
            return rect;
        }

        private void HandleProjectPropertyChanged (string prop)
        {
            if (prop == ProjectModel.PropertyClient
                    || prop == ProjectModel.PropertyName
                    || prop == ProjectModel.PropertyColor) {
                PopulateCell();
            }
        }

        private void HandleClientPropertyChanged (string prop)
        {
            if (prop == ClientModel.PropertyName) {
                PopulateCell();
            }
        }

        public void PopulateCell()
        {
            UIColor projectColor;
            string projectName;
            string clientName = String.Empty;
            int taskCount = 0;

            if (DataSource.IsNoProject) {
                projectColor = Color.Gray;
                projectName = "ProjectNoProject".Tr();
                projectLabel.Apply (Style.ProjectList.NoProjectLabel);
            } else if (DataSource.IsNewProject) {
                projectColor = Color.LightestGray;
                projectName = "ProjectNewProject".Tr();
                projectLabel.Apply (Style.ProjectList.NewProjectLabel);
            } else if (model != null) {
                projectColor = UIColor.Clear.FromHex (model.GetHexColor());

                projectName = model.Name;
                clientName = model.Client != null ? model.Client.Name : String.Empty;
                taskCount = DataSource.Tasks.Count;
                projectLabel.Apply (Style.ProjectList.ProjectLabel);
            } else {
                return;
            }

            if (String.IsNullOrWhiteSpace (projectName)) {
                projectName = "ProjectNoNameProject".Tr();
                clientName = String.Empty;
            }

            if (!String.IsNullOrWhiteSpace (projectName)) {
                projectLabel.Text = projectName;
                projectLabel.Hidden = false;

                if (!String.IsNullOrEmpty (clientName)) {
                    clientLabel.Text = clientName;
                    clientLabel.Hidden = false;
                } else {
                    clientLabel.Hidden = true;
                }
            } else {
                projectLabel.Hidden = true;
                clientLabel.Hidden = true;
            }

            tasksButton.Hidden = taskCount < 1;
            if (!tasksButton.Hidden) {
                tasksButton.SetTitle (taskCount.ToString(), UIControlState.Normal);
                tasksButton.SetTitleColor (projectColor, UIControlState.Normal);
            }

            BackgroundView.BackgroundColor = projectColor;
        }
    }
}