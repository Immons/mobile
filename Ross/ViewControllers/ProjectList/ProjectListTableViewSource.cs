using System;
using UIKit;
using Toggl.Phoebe.Data.Views;
using Foundation;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Ross.Theme;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Toggl.Ross.DataSources;

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
                var cell = (ProjectCell)tableView.DequeueReusableCell (ProjectCellId, indexPath);
                cell.Bind (project);
                if (project.Data != null && project.Data.Id != Guid.Empty) {
                    var projectId = project.Data.Id;
//                    cell.ToggleTasks = () => ToggleTasksExpanded(projectId);
                } else {
                    cell.ToggleTasks = null;
                }
                return cell;
            }

            var taskData = row as TaskData;
            if (taskData != null) {
                var cell = (TaskCell)tableView.DequeueReusableCell (TaskCellId, indexPath);
                cell.Bind ((TaskModel)taskData);

//                var rows = GetCachedRows(GetSection(indexPath.Section));
//                cell.IsFirst = indexPath.Row < 1 || !(rows[indexPath.Row - 1] is TaskModel);
//                cell.IsLast = indexPath.Row >= rows.Count || !(rows[indexPath.Row + 1] is TaskModel);
                return cell;
            }

            var workspace = row as ProjectAndTaskView.Workspace;
            if (workspace != null) {
                var cell = (WorkspaceHeaderCell)tableView.DequeueReusableCell (WorkspaceHeaderId, indexPath);
                cell.Bind (workspace);
                return cell;
            }

            throw new InvalidOperationException (String.Format ("Unknown row type {0}", row.GetType()));
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
//                viewModel.Finish((TaskModel)data);
            } else if (m is ProjectAndTaskView.Project) {
                var wrap = (ProjectAndTaskView.Project)m;
                if (wrap.IsNoProject) {
//                    viewModel.Finish(workspace: new WorkspaceModel(wrap.WorkspaceId));
                } else if (wrap.IsNewProject) {
                    var proj = (ProjectModel)wrap.Data;
                    // Show create project dialog instead

                    var newProjectViewController = new NewProjectViewController (proj.Workspace, proj.Color) {
//                        ProjectCreated = (p) => viewModel.Finish(project: p),
                    };

//                    this.viewModel.ShowNewProject(newProjectViewController);
                } else {
//                    viewModel.Finish(project: (ProjectModel)wrap.Data);
                }
            } else if (m is ProjectAndTaskView.Workspace) {
                var wrap = (ProjectAndTaskView.Workspace)m;
//                viewModel.Finish(workspace: (WorkspaceModel)wrap.Data);
            }

            tableView.DeselectRow (indexPath, true);
        }
    }
}