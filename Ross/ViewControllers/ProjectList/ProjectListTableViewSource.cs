using System;
using System.Collections.Generic;
using System.Linq;
using Foundation;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Views;
using Toggl.Ross.Theme;
using UIKit;

namespace Toggl.Ross.ViewControllers.ProjectList
{
    public class ProjectListTableViewSource : UITableViewSource
    {
        private readonly static NSString WorkspaceHeaderId = new NSString ("SectionHeaderId");
        private readonly static NSString ProjectCellId = new NSString ("ProjectCellId");
        private readonly static NSString TaskCellId = new NSString ("TaskCellId");

        private WorkspaceProjectsView projectList;
        private UITableView tableView;
        private IList<object> data;

        public ProjectListTableViewSource (UITableView tableView)
        {
            this.data = new List<object>();
            this.tableView = tableView;
            this.Attach();
        }

        public event EventHandler<TaskModel> TaskSelected;
        public event EventHandler<ProjectModel> ProjectSelected;
        public event EventHandler<WorkspaceModel> WorkspaceSelected;

        public WorkspaceProjectsView ProjectList
        {
            get {
                return projectList;
            } set {
                if (value?.Data != null) {
                    projectList = value;
                    data = projectList.Data.Where (p => p is TaskData || p is WorkspaceProjectsView.Project || p is WorkspaceProjectsView.Workspace).ToList();
                    this.tableView.ReloadData();
                }
            }
        }

        public void Attach()
        {
            tableView.RegisterClassForCellReuse (typeof (WorkspaceHeaderCell), WorkspaceHeaderId);
            tableView.RegisterClassForCellReuse (typeof (ProjectCell), ProjectCellId);
            tableView.RegisterClassForCellReuse (typeof (TaskCell), TaskCellId);
            tableView.SeparatorStyle = UITableViewCellSeparatorStyle.None;
        }

        public override nint RowsInSection (UITableView tableview, nint section)
        {
            return data.Count;
        }

        public override nfloat EstimatedHeight (UITableView tableView, NSIndexPath indexPath)
        {
            return 60f;
        }

        public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            var row = ProjectList.Data.ToList()[indexPath.Row];
            if (row is WorkspaceProjectsView.Workspace) {
                return 42f;
            }
            if (row is TaskModel) {
                return 49f;
            }
            return EstimatedHeight (tableView, indexPath);
        }

        public override nfloat EstimatedHeightForHeader (UITableView tableView, nint section)
        {
            return -1f;
        }

        public override nfloat GetHeightForHeader (UITableView tableView, nint section)
        {
            return EstimatedHeightForHeader (tableView, section);
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            var row = data[indexPath.Row];

            var project = row as WorkspaceProjectsView.Project;
            if (project != null) {
                return CreateProjectCell (project, indexPath);
            }

            var taskData = row as TaskData;
            if (taskData != null) {
                return CreateTaskCell ((TaskModel)taskData, indexPath);
            }

            var workspace = row as WorkspaceProjectsView.Workspace;
            if (workspace != null) {
                return CreateWorkspaceCell (workspace, indexPath);
            }

            throw new InvalidOperationException (String.Format ("Unknown row type {0}", row.GetType()));
        }

        private UITableViewCell CreateTaskCell (TaskModel taskModel, NSIndexPath indexPath)
        {
            var cell = (TaskCell)tableView.DequeueReusableCell (TaskCellId, indexPath);
//            var rows = GetCachedRows(GetSection(indexPath.Section));
//            cell.IsFirst = indexPath.Row < 1 || !(rows[indexPath.Row - 1] is TaskModel);
//            cell.IsLast = indexPath.Row >= rows.Count || !(rows[indexPath.Row + 1] is TaskModel);

            var taskName = taskModel.Name;
            if (String.IsNullOrWhiteSpace (taskName)) {
                taskName = "ProjectNoNameTask".Tr();
            }

            cell.NameLabel.Text = taskName;

            return cell;
        }

        private UITableViewCell CreateWorkspaceCell (WorkspaceProjectsView.Workspace workspace, NSIndexPath indexPath)
        {
            var cell = (WorkspaceHeaderCell)tableView.DequeueReusableCell (WorkspaceHeaderId, indexPath);

            cell.NameLabel.Text = workspace.Data.Name;

            return cell;
        }

        private UITableViewCell CreateProjectCell (WorkspaceProjectsView.Project project, NSIndexPath indexPath)
        {
            var cell = (ProjectCell)tableView.DequeueReusableCell (ProjectCellId, indexPath);

            if (project.Data != null && project.Data.Id != Guid.Empty) {
                var projectId = project.Data.Id;
//                cell.ToggleTasks = () => ToggleTasksExpanded(projectId);
            } else {
                cell.ToggleTasks = null;
            }

            PopulateProjectCell (project, cell);

            return cell;
        }

        private void PopulateProjectCell (WorkspaceProjectsView.Project project, ProjectCell cell)
        {
            UIColor projectColor;
            string projectName;
            string clientName = String.Empty;
            int taskCount = 0;
            var model = (ProjectModel)project.Data;

            if (project.IsNoProject) {
                projectColor = Color.Gray;
                projectName = "ProjectNoProject".Tr();
                cell.ProjectLabel.Apply (Style.ProjectList.NoProjectLabel);
            } else if (project.IsNewProject) {
                projectColor = Color.LightestGray;
                projectName = "ProjectNewProject".Tr();
                cell.ProjectLabel.Apply (Style.ProjectList.NewProjectLabel);
            } else if (model != null) {
                projectColor = UIColor.Clear.FromHex (model.GetHexColor());

                projectName = project.Data.Name;
                clientName = model.Client != null ? model.Client.Name : String.Empty;
                taskCount = project.Tasks.Count;
                cell.ProjectLabel.Apply (Style.ProjectList.ProjectLabel);
            } else {
                return;
            }

            if (String.IsNullOrWhiteSpace (projectName)) {
                projectName = "ProjectNoNameProject".Tr();
                clientName = String.Empty;
            }

            if (!String.IsNullOrWhiteSpace (projectName)) {
                cell.ProjectLabel.Text = projectName;
                cell.ProjectLabel.Hidden = false;

                if (!String.IsNullOrEmpty (clientName)) {
                    cell.ClientLabel.Text = clientName;
                    cell.ClientLabel.Hidden = false;
                } else {
                    cell.ClientLabel.Hidden = true;
                }
            } else {
                cell.ProjectLabel.Hidden = true;
                cell.ClientLabel.Hidden = true;
            }

            cell.TasksButton.Hidden = taskCount < 1;
            if (!cell.TasksButton.Hidden) {
                cell.TasksButton.SetTitle (taskCount.ToString(), UIControlState.Normal);
                cell.TasksButton.SetTitleColor (projectColor, UIControlState.Normal);
            }

            cell.BackgroundView.BackgroundColor = projectColor;
        }

        public override UIView GetViewForHeader (UITableView tableView, nint section)
        {
            return new UIView().Apply (Style.ProjectList.HeaderBackgroundView);
        }

        public override bool CanEditRow (UITableView tableView, NSIndexPath indexPath)
        {
            return false;
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            var m = data[indexPath.Row];

            if (m is TaskData) {
                var data = (TaskData)m;
                TaskSelected (this, (TaskModel)data);
            } else if (m is WorkspaceProjectsView.Project) {
                var wrap = (WorkspaceProjectsView.Project)m;
                if (wrap.IsNoProject) {
                    WorkspaceSelected (this, new WorkspaceModel (wrap.WorkspaceId));
                } else if (wrap.IsNewProject) {
                    var proj = (ProjectModel)wrap.Data;
                    // Show create project dialog instead

                    var newProjectViewController = new NewProjectViewController (proj.Workspace, proj.Color) {
//                        ProjectCreated = (p) => viewModel.Finish(project: p),
                    };

//                    this.viewModel.ShowNewProject(newProjectViewController);
                } else {
                    ProjectSelected (this, (ProjectModel)wrap.Data);
                }
            } else if (m is WorkspaceProjectsView.Workspace) {
                var wrap = (ProjectAndTaskView.Workspace)m;
                WorkspaceSelected (this, (WorkspaceModel)wrap.Data);
            }

            tableView.DeselectRow (indexPath, true);
        }
    }
}