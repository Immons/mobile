using System;
using Toggl.Phoebe.Data.Utils;

namespace Toggl.Ross.Views
{
    public abstract class ModelTableViewCell<T> : BindableTableViewCell<T>
    {
        private PropertyChangeTracker tracker = new PropertyChangeTracker();

        protected ModelTableViewCell()
        {
        }

        protected ModelTableViewCell (IntPtr handle)
        : base (handle)
        {
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                if (tracker != null) {
                    tracker.Dispose();
                    tracker = null;
                }
            }

            base.Dispose (disposing);
        }

        protected PropertyChangeTracker Tracker
        {
            get { return tracker; }
        }

        protected abstract void ResetTrackedObservables();
    }
}
