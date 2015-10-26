using System;
using UIKit;

namespace Toggl.Ross.Views
{
    public abstract class BindableTableViewCell<T> : UITableViewCell
    {
        private T dataSource;

        protected BindableTableViewCell()
        {
        }

        protected BindableTableViewCell (IntPtr handle)
        : base (handle)
        {
        }

        public void Bind (T dataSource)
        {
            this.dataSource = dataSource;
            OnDataSourceChanged();
        }

        protected virtual void OnDataSourceChanged()
        {
            Rebind();
        }

        protected abstract void Rebind();

        public T DataSource
        {
            get { return dataSource; }
        }
    }
}
