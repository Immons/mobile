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
        private ProjectListViewModel viewModel;

        public ProjectSelectionViewController (TimeEntryModel model)
        : base (UITableViewStyle.Plain)
        {
            this.model = model;

            Title = "ProjectTitle".Tr();
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            var array = new [] { model.Id.ToString() };
            var timeEntryIds = new List<string> (array);

            viewModel = new ProjectListViewModel (timeEntryIds);

            View.Apply (Style.Screen);
            EdgesForExtendedLayout = UIRectEdge.None;
            new Source (this).Attach();
        }

        private void CreateBindingSet()
        {
//            Binding.Create(() => );
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);

            ServiceContainer.Resolve<ITracker>().CurrentScreen = "Select Project";
        }

        public Action ProjectSelected { get; set; }

        public async void Finish (TaskModel task = null, ProjectModel project = null, WorkspaceModel workspace = null)
        {
            project = task != null ? task.Project : project;
            if (project != null) {
                await project.LoadAsync();
                workspace = project.Workspace;
            }

            if (project != null || task != null || workspace != null) {
                model.Workspace = workspace;
                model.Project = project;
                model.Task = task;
                await model.SaveAsync();
            }

            var cb = ProjectSelected;
            if (cb != null) {
                cb();
            } else {
                // Pop to previous view controller
                var vc = NavigationController.ViewControllers;
                var i = Array.IndexOf (vc, this) - 1;
                if (i >= 0) {
                    NavigationController.PopToViewController (vc[i], true);
                }
            }
        }
    }
}