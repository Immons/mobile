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
using System.Windows.Input;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Toggl.Phoebe.Contrib.Bind;
using System.Reflection;

namespace Toggl.Phoebe.Data.ViewModels
{
    [ImplementPropertyChanged]
    public class ProjectListViewModel : IVModel<ITimeEntryModel>, INotifyPropertyChanged
    {
        private IList<TimeEntryData> timeEntryList;
        private WorkspaceProjectsView projectList;
        private IList<string> timeEntryIds;
        private Action navigateBackAction;

        public ProjectListViewModel (IList<TimeEntryData> timeEntryList)
        {
            this.timeEntryList = timeEntryList;
            ServiceContainer.Resolve<ITracker>().CurrentScreen = "Select Project";
        }

        public ProjectListViewModel (IList<string> timeEntryIds)
        {
            this.timeEntryIds = timeEntryIds;
            ServiceContainer.Resolve<ITracker>().CurrentScreen = "Select Project";

            ProjectList.CollectionChanged += (sender, e) => {
                //needed because in current logic, there is no INotifyPropertyChanged for Data object
                OnPropertyChanged ("ProjectList");
            };
        }

        public event EventHandler<object> ShowNewProjectEvent;

        public bool IsLoading { get; set; }

        public ITimeEntryModel Model { get; set; }

        public WorkspaceProjectsView ProjectList
        {
            get {
                if (projectList == null) {
                    projectList = new WorkspaceProjectsView();
                }

                return projectList;
            } set {

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
                Model = new TimeEntryGroup (timeEntryList);
            } else if (timeEntryList.Count == 1) {
                Model = new TimeEntryModel (timeEntryList[0]);
            }

            await Model.LoadAsync();

            if (Model.Workspace == null || Model.Workspace.Id == Guid.Empty) {
                Model = null;
            }

            IsLoading = false;
        }

        public async Task SaveModelAsync (ProjectModel project, WorkspaceModel workspace, TaskData task = null)
        {
            Model.Project = project;
            Model.Workspace = workspace;
            if (task != null) {
                Model.Task = new TaskModel (task);
            }

            await Model.SaveAsync();
        }

        public void Dispose()
        {
            projectList.Dispose();
            Model = null;
        }

        public async void Finish (TaskModel task = null, ProjectModel project = null, WorkspaceModel workspace = null)
        {
            project = task != null ? task.Project : project;
            if (project != null) {
                await project.LoadAsync();
                workspace = project.Workspace;
            }

            if (project != null || task != null || workspace != null) {
                Model.Workspace = workspace;
                Model.Project = project;
                Model.Task = task;
                await Model.SaveAsync();
            }

            if (this.navigateBackAction != null) {
                this.navigateBackAction();
            }
        }

        public void SetNavigateBack (Action action)
        {
            this.navigateBackAction = action;
        }

        public void ShowNewProject (object view)
        {
            if (this.ShowNewProjectEvent != null) {
                this.ShowNewProjectEvent (this, view);
            }
        }

        #region INotifyPropertyChanged implementation

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged ([CallerMemberName] string name = "")
        {
            if (PropertyChanged != null) {
                PropertyChanged (this, new PropertyChangedEventArgs (name));
            }
        }

        #endregion
    }
}