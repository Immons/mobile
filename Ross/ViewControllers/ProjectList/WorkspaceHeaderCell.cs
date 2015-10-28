using System;
using CoreGraphics;
using Toggl.Ross.Theme;
using UIKit;

namespace Toggl.Ross.ViewControllers.ProjectList
{
    public class WorkspaceHeaderCell : UITableViewCell
    {
        private const float HorizSpacing = 15f;
        private readonly UILabel nameLabel;

        public WorkspaceHeaderCell (IntPtr handle)
        : base (handle)
        {
            this.Apply (Style.Screen);
            nameLabel = new UILabel().Apply (Style.ProjectList.HeaderLabel);
            ContentView.AddSubview (nameLabel);

            BackgroundView = new UIView().Apply (Style.ProjectList.HeaderBackgroundView);
            UserInteractionEnabled = false;
        }

        public UILabel NameLabel
        {
            get { return nameLabel; }
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
    }
}