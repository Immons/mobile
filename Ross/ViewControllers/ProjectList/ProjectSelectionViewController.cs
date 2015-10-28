using System;
using System.Collections.Generic;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Contrib.Bind;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.ViewModels;
using Toggl.Ross.Theme;
using UIKit;
using XPlatUtils;

namespace Toggl.Ross.ViewControllers.ProjectList
{
    public class ProjectSelectionViewController : UITableViewController
    {
        private ProjectListTableViewSource tableViewSource;

        public ProjectSelectionViewController (TimeEntryModel model)
        : base (UITableViewStyle.Plain)
        {
            this.Model = model;

            Title = "ProjectTitle".Tr();
        }

        public ProjectListViewModel ViewModel { get; set; }

        public TimeEntryModel Model { get; set; }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            var array = new [] { Model.Id.ToString() };
            var timeEntryIds = new List<string> (array);
            this.ViewModel = new ProjectListViewModel (timeEntryIds);

            View.Apply (Style.Screen);
            EdgesForExtendedLayout = UIRectEdge.None;

            tableViewSource = new ProjectListTableViewSource (this.TableView);
            this.tableViewSource.TaskSelected += (sender, e) => {
                ViewModel.Finish (e);
                this.NavigateBack();
            };
            this.tableViewSource.ProjectSelected += (sender, e) => {
                ViewModel.Finish (project: e);
                this.NavigateBack();
            };
            this.tableViewSource.WorkspaceSelected += (sender, e) => {
                ViewModel.Finish (workspace: e);
                this.NavigateBack();
            };

            this.TableView.Source = tableViewSource;

            CreateBindingSet();
            CreateNavBarButtonNewProject();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);

            ServiceContainer.Resolve<ITracker>().CurrentScreen = "Select Project";
        }

        private void CreateNavBarButtonNewProject()
        {
            NavigationItem.RightBarButtonItem = new UIBarButtonItem (
                "ClientNewClient".Tr(), UIBarButtonItemStyle.Plain, OnNavigationBarAddClicked)
            .Apply (Style.NavLabelButton);
        }

        private void OnNavigationBarAddClicked (object sender, EventArgs e)
        {
            var data = this.Model.Workspace.Data;
            var color = new Random().Next (0, ProjectModel.HexColors.Length - 1);

            var newProjectViewController = new NewProjectViewController (new WorkspaceModel (data), color) {
                ProjectCreated = (p) => {
                    ViewModel.Finish (project: p);
                    this.NavigateBack();
                },
            };

            this.NavigationController.PushViewController (newProjectViewController, true);
        }

        private void CreateBindingSet()
        {
            Binding.Create (() => this.ViewModel.ProjectList == tableViewSource.ProjectList);
            Binding.Create (() => this.ViewModel.Model == Model);
        }

        private void NavigateBack()
        {
            this.NavigationController.PopViewController (true);
        }
    }
}