using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PropertyChanged;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Data.ViewModels;
using Toggl.Phoebe.Data.Views;
using XPlatUtils;

namespace Toggl.Phoebe.Data.ViewModels
{
    [ImplementPropertyChanged]
    public class ProjectListViewModel : IVModel<ITimeEntryModel>
    {
        private IList<TimeEntryData> timeEntryList;
        private WorkspaceProjectsView projectList;
        private IList<string> timeEntryIds;

        public ProjectListViewModel (IList<TimeEntryData> timeEntryList)
        {
            this.timeEntryList = timeEntryList;
            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "Select Project";
        }

        public ProjectListViewModel (IList<string> timeEntryIds)
        {
            this.timeEntryIds = timeEntryIds;
            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "Select Project";
        }

        public bool IsLoading { get; set; }

        public ITimeEntryModel Model { get; set; }

        public WorkspaceProjectsView ProjectList
        {
            get {
                if (projectList == null) {
                    projectList = new WorkspaceProjectsView ();
                }

                return projectList;
            } set {
                //Bind library needs set method
            }
        }

        public IList<TimeEntryData> TimeEntryList
        {
            get {
                return timeEntryList;
            }
        }

        public async Task Init ()
        {
            IsLoading = true;

            if (timeEntryList == null) {
                timeEntryList = await TimeEntryGroup.GetTimeEntryDataList (timeEntryIds);
            }

            // Create model.
            if (timeEntryList.Count > 1) {
                Model = new TimeEntryGroup (timeEntryList);
            } else if (timeEntryList.Count == 1) {
                Model = new TimeEntryModel (timeEntryList [0]);
            }

            await Model.LoadAsync ();

            if (Model.Workspace == null || Model.Workspace.Id == Guid.Empty) {
                Model = null;
            }

            IsLoading = false;
        }

        public void Dispose ()
        {
            projectList.Dispose ();
            Model = null;
        }

        public async void Finish (TaskModel task = null, ProjectModel project = null, WorkspaceModel workspace = null)
        {
            project = task != null ? task.Project : project;
            if (project != null) {
                await project.LoadAsync ();
                workspace = project.Workspace;
            }

            if (project != null || task != null || workspace != null) {
                Model.Workspace = workspace;
                Model.Project = project;
                Model.Task = task;
                await Model.SaveAsync ();
            }
        }
    }
}