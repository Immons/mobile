using System;
using CoreGraphics;
using Toggl.Ross.Theme;
using UIKit;

namespace Toggl.Ross.ViewControllers.ProjectList
{
    public class TaskCell : UITableViewCell
    {
        private const float CellSpacing = 4f;
        private readonly UILabel nameLabel;
        private readonly UIView separatorView;
        private bool isFirst;
        private bool isLast;

        public TaskCell (IntPtr handle)
        : base (handle)
        {
            this.Apply (Style.Screen);
            ContentView.Add (nameLabel = new UILabel().Apply (Style.ProjectList.TaskLabel));
            ContentView.Add (separatorView = new UIView().Apply (Style.ProjectList.TaskSeparator));
            BackgroundView = new UIView().Apply (Style.ProjectList.TaskBackground);
        }

        public UILabel NameLabel
        {
            get { return nameLabel; }
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();

            var contentFrame = new CGRect (0, 0, Frame.Width, Frame.Height);

            if (isFirst) {
                contentFrame.Y += CellSpacing / 2;
                contentFrame.Height -= CellSpacing / 2;
            }

            if (isLast) {
                contentFrame.Height -= CellSpacing / 2;
            }

            SelectedBackgroundView.Frame = BackgroundView.Frame = ContentView.Frame = contentFrame;

            // Add padding
            contentFrame.X = 15f;
            contentFrame.Y = 0;
            contentFrame.Width -= 15f;

            nameLabel.Frame = contentFrame;
            separatorView.Frame = new CGRect (
                contentFrame.X, contentFrame.Y + contentFrame.Height - 1f,
                contentFrame.Width, 1f);
        }

        public bool IsFirst
        {
            get { return isFirst; }
            set {
                if (isFirst == value) {
                    return;
                }
                isFirst = value;
                SetNeedsLayout();
            }
        }

        public bool IsLast
        {
            get { return isLast; }
            set {
                if (isLast == value) {
                    return;
                }
                isLast = value;
                SetNeedsLayout();

                separatorView.Hidden = isLast;
            }
        }
    }
}