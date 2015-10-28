using System;
using CoreGraphics;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Views;
using Toggl.Ross.Theme;
using Toggl.Ross.Views;
using UIKit;

namespace Toggl.Ross.ViewControllers.ProjectList
{
    public class WorkspaceHeaderCell : UITableViewCell
    {
        private const float HorizSpacing = 15f;
        private readonly UILabel nameLabel;
        //        private WorkspaceModel model;

        public WorkspaceHeaderCell (IntPtr handle)
        : base (handle)
        {
            this.Apply (Style.Screen);
            nameLabel = new UILabel().Apply (Style.ProjectList.HeaderLabel);
            ContentView.AddSubview (nameLabel);

            BackgroundView = new UIView().Apply (Style.ProjectList.HeaderBackgroundView);
            UserInteractionEnabled = false;
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            var contentFrame = ContentView.Frame;

            nameLabel.Frame = new CGRect (
                x: HorizSpacing,
                y: 0,
                width: contentFrame.Width - 2 * HorizSpacing,
                height: contentFrame.Height
            );
        }

        private void HandleClientPropertyChanged (string prop)
        {
            if (prop == WorkspaceModel.PropertyName) {
                PopulateCell();
            }
        }

        public void PopulateCell()
        {
            if (model != null) {
                nameLabel.Text = model.Name;
            }
        }
    }
}