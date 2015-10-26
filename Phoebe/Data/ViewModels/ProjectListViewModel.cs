using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Data.ViewModels;
using Toggl.Phoebe.Data.Views;
using XPlatUtils;
using PropertyChanged;

namespace Toggl.Phoebe.Data.ViewModels
{
    [ImplementPropertyChanged]
    public class ProjectListViewModel : IVModel<ITimeEntryModel>
    {
        private ITimeEntryModel model;
        private IList<TimeEntryData> timeEntryList;
        private WorkspaceProjectsView projectList;
        private IList<string> timeEntryIds;

        public ProjectListViewModel (IList<TimeEntryData> timeEntryList)
        {
            this.timeEntryList = timeEntryList;
            ServiceContainer.Resolve<ITracker>().CurrentScreen = "Select Project";
        }

        public ProjectListViewModel (IList<string> timeEntryIds)
        {
            this.timeEntryIds = timeEntryIds;
            ServiceContainer.Resolve<ITracker>().CurrentScreen = "Select Project";
        }

        public bool IsLoading { get; set; }

        public ITimeEntryModel Model { get; set; }

        public WorkspaceProjectsView ProjectList
        {
            get {
                if (projectList == null) {
                    projectList = new WorkspaceProjectsView();
                }

                return projectList;
            }
        }

        public IList<TimeEntryData> TimeEntryList
        {
            get {
                return timeEntryList;
            }
        }

        public async Task Init()
        {
            IsLoading = true;

            if (timeEntryList == null) {
                timeEntryList = await TimeEntryGroup.GetTimeEntryDataList (timeEntryIds);
            }

            // Create model.
            if (timeEntryList.Count > 1) {
                model = new TimeEntryGroup (timeEntryList);
            } else if (timeEntryList.Count == 1) {
                model = new TimeEntryModel (timeEntryList[0]);
            }

            await model.LoadAsync();

            if (model.Workspace == null || model.Workspace.Id == Guid.Empty) {
                model = null;
            }

            IsLoading = false;
        }

        public async Task SaveModelAsync (ProjectModel project, WorkspaceModel workspace, TaskData task = null)
        {
            model.Project = project;
            model.Workspace = workspace;
            if (task != null) {
                model.Task = new TaskModel (task);
            }

            await model.SaveAsync();
        }

        public void Dispose()
        {
            projectList.Dispose();
            model = null;
        }
    }
}