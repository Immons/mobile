using System;
using System.Collections.Generic;
using Foundation;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Views;
using Toggl.Ross.DataSources;
using Toggl.Ross.Theme;
using UIKit;
using Toggl.Phoebe.Data.ViewModels;

namespace Toggl.Ross.ViewControllers.ProjectList
{
    public class Source : PlainDataViewSource<object>
    {
        private readonly static NSString WorkspaceHeaderId = new NSString ("SectionHeaderId");
        private readonly static NSString ProjectCellId = new NSString ("ProjectCellId");
        private readonly static NSString TaskCellId = new NSString ("TaskCellId");
        private readonly UITableView tableView;
        private readonly HashSet<Guid> expandedProjects = new HashSet<Guid>();
        private readonly ProjectListViewModel viewModel;

        public Source (UITableView tableView, ProjectListViewModel viewModel)
        : base (tableView, new ProjectAndTaskView())
        {
            this.viewModel = viewModel;
            this.tableView = tableView;
        }

        public override void Attach()
        {
            base.Attach();

            tableView.RegisterClassForCellReuse (typeof (WorkspaceHeaderCell), WorkspaceHeaderId);
            tableView.RegisterClassForCellReuse (typeof (ProjectCell), ProjectCellId);
            tableView.RegisterClassForCellReuse (typeof (TaskCell), TaskCellId);
            tableView.SeparatorStyle = UITableViewCellSeparatorStyle.None;
        }

        private void ToggleTasksExpanded (Guid projectId)
        {
            SetTasksExpanded (projectId, !expandedProjects.Contains (projectId));
        }

        private void SetTasksExpanded (Guid projectId, bool expand)
        {
            if (expand && expandedProjects.Add (projectId)) {
                Update();
            } else if (!expand && expandedProjects.Remove (projectId)) {
                Update();
            }
        }

        public override nfloat EstimatedHeight (UITableView tableView, NSIndexPath indexPath)
        {
            return 60f;
        }

        public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            var row = GetRow (indexPath);
            if (row is ProjectAndTaskView.Workspace) {
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
            var row = GetRow (indexPath);

            var project = row as WorkspaceProjectsView.Project;
            if (project != null) {
                var cell = (ProjectCell)tableView.DequeueReusableCell (ProjectCellId, indexPath);
                cell.Bind (project);
                if (project.Data != null && project.Data.Id != Guid.Empty) {
                    var projectId = project.Data.Id;
                    cell.ToggleTasks = () => ToggleTasksExpanded (projectId);
                } else {
                    cell.ToggleTasks = null;
                }
                return cell;
            }

            var taskData = row as TaskData;
            if (taskData != null) {
                var cell = (TaskCell)tableView.DequeueReusableCell (TaskCellId, indexPath);
                cell.Bind ((TaskModel)taskData);

                var rows = GetCachedRows (GetSection (indexPath.Section));
                cell.IsFirst = indexPath.Row < 1 || ! (rows[indexPath.Row - 1] is TaskModel);
                cell.IsLast = indexPath.Row >= rows.Count || ! (rows[indexPath.Row + 1] is TaskModel);
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
            var m = GetRow (indexPath);

            if (m is TaskData) {
                var data = (TaskData)m;
                viewModel.Finish ((TaskModel)data);
            } else if (m is ProjectAndTaskView.Project) {
                var wrap = (ProjectAndTaskView.Project)m;
                if (wrap.IsNoProject) {
                    viewModel.Finish (workspace: new WorkspaceModel (wrap.WorkspaceId));
                } else if (wrap.IsNewProject) {
                    var proj = (ProjectModel)wrap.Data;
                    // Show create project dialog instead

                    var newProjectViewController = new NewProjectViewController (proj.Workspace, proj.Color) {
                        ProjectCreated = (p) => viewModel.Finish (project: p),
                    };

                    this.viewModel.ShowNewProject (newProjectViewController);
                } else {
                    viewModel.Finish (project: (ProjectModel)wrap.Data);
                }
            } else if (m is ProjectAndTaskView.Workspace) {
                var wrap = (ProjectAndTaskView.Workspace)m;
                viewModel.Finish (workspace: (WorkspaceModel)wrap.Data);
            }

            tableView.DeselectRow (indexPath, true);
        }

        protected override IEnumerable<object> GetRows (string section)
        {
            foreach (var row in DataView.Data) {
                var task = row as TaskData;
                if (task != null && !expandedProjects.Contains (task.ProjectId)) {
                    continue;
                }

                yield return row;
            }
        }
    }
}