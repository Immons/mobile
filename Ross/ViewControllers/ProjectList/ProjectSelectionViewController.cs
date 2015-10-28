using System;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.Models;
using Toggl.Ross.Theme;
using UIKit;
using XPlatUtils;
using Toggl.Phoebe.Data.ViewModels;
using System.Collections.Generic;
using Toggl.Phoebe.Contrib.Bind;
using System.Linq;

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

            ProjectListTableViewSource = new ProjectListTableViewSource (this.TableView);
            this.ProjectListTableViewSource.TaskSelected += (sender, e) => ViewModel.Finish (e);
            this.ProjectListTableViewSource.ProjectSelected += (sender, e) => ViewModel.Finish (project: e);
            this.ProjectListTableViewSource.WorkspaceSelected += (sender, e) => ViewModel.Finish (workspace: e);

            this.TableView.Source = ProjectListTableViewSource;

            this.ViewModel.ShowNewProjectEvent += (sender, e) => this.NavigateToNewProject (e);

            CreateBindingSet();
            CreateNavBarButtonNewProject();
        }

        private void CreateNavBarButtonNewProject()
        {
            NavigationItem.RightBarButtonItem = new UIBarButtonItem (
                "ClientNewClient".Tr(), UIBarButtonItemStyle.Plain, OnNavigationBarAddClicked)
            .Apply (Style.NavLabelButton);
        }

        private void OnNavigationBarAddClicked (object sender, EventArgs e)
        {
            //TODO this is for sure not right method to get WorkspaceId
            var test = ViewModel.ProjectList.Workspaces.Last().Clients.Last().WorkspaceId;

            var newProjectViewController = new NewProjectViewController (new WorkspaceModel (test), 0) {
                ProjectCreated = (p) => ViewModel.Finish (project: p),
            };

            this.NavigateToNewProject (newProjectViewController);
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