using System;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.Models;
using Toggl.Ross.Theme;
using UIKit;
using XPlatUtils;
using Toggl.Phoebe.Data.ViewModels;
using System.Collections.Generic;
using Toggl.Phoebe.Contrib.Bind;

namespace Toggl.Ross.ViewControllers.ProjectList
{
    public class ProjectSelectionViewController : UITableViewController
    {
        private readonly TimeEntryModel model;
        //        private Source source;

        public ProjectSelectionViewController (TimeEntryModel model)
        : base (UITableViewStyle.Plain)
        {
            this.model = model;

            Title = "ProjectTitle".Tr();
        }

        public ProjectListViewModel ViewModel { get; set; }

        private ProjectListTableViewSource ProjectListTableViewSource { get; set; }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            var array = new [] { model.Id.ToString() };
            var timeEntryIds = new List<string> (array);
            this.ViewModel = new ProjectListViewModel (timeEntryIds);

            this.ViewModel.Model = this.model;
            this.ViewModel.SetNavigateBack (NavigateBack);

            View.Apply (Style.Screen);
            EdgesForExtendedLayout = UIRectEdge.None;
//            source = new Source(this.TableView, this.ViewModel);
//            source.Attach();

            ProjectListTableViewSource = new ProjectListTableViewSource (this.TableView);
            this.TableView.Source = ProjectListTableViewSource;

            this.ViewModel.ShowNewProjectEvent += (sender, e) => this.NavigateToNewProject (e);

            CreateBindingSet();
        }

        private void CreateBindingSet()
        {
            Binding.Create (() => this.ViewModel.ProjectList == ProjectListTableViewSource.ProjectList);
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);

            ServiceContainer.Resolve<ITracker>().CurrentScreen = "Select Project";
        }

        private void NavigateBack()
        {
            // Pop to previous view controller
            var vc = NavigationController.ViewControllers;
            var i = Array.IndexOf (vc, this) - 1;
            if (i >= 0) {
                NavigationController.PopToViewController (vc[i], true);
            }
        }

        private void NavigateToNewProject (object view)
        {
            this.NavigationController.PushViewController (view as UIViewController, true);
        }
    }
}