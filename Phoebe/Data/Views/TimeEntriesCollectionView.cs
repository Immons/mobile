﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Json.Converters;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Logging;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Data.Views
{
    /// <summary>
    /// This view combines ICollectionDataView data and data from ITogglClient for time views. It tries to load data from
    /// web, but always falls back to data from the local store.
    /// </summary>
    public class TimeEntriesCollectionView : ICollectionDataView<object>, IDisposable
    {
        protected string Tag = "TimeEntriesCollectionView";
        protected TimeEntryHolder LastRemovedItem;
        protected readonly List<object> ItemCollection = new List<object> ();

        private readonly List<IDateGroup> dateGroups = new List<IDateGroup> ();
        private UpdateMode updateMode = UpdateMode.Batch;
        private DateTime startFrom;
        private Subscription<DataChangeMessage> subscriptionDataChange;
        private Subscription<SyncFinishedMessage> subscriptionSyncFinished;
        private bool reloadScheduled;
        private bool isLoading;
        private bool hasMore;
        private int lastNumberOfItems;
        private bool isUpdatingCollection;
        private Queue<DataChangeMessage> updateMessageQueue = new Queue<DataChangeMessage> ();

        public TimeEntriesCollectionView ()
        {
            var bus = ServiceContainer.Resolve<MessageBus> ();
            subscriptionDataChange = bus.Subscribe<DataChangeMessage> (OnDataChange);
            HasMore = true;
            Reload ();
        }

        public void Dispose ()
        {
            // Clean lists
            updateMessageQueue.Clear ();
            ItemCollection.Clear ();
            foreach (var dateGroup in dateGroups) {
                dateGroup.Dispose ();
            }
            dateGroups.Clear ();

            var bus = ServiceContainer.Resolve<MessageBus> ();
            if (subscriptionDataChange != null) {
                bus.Unsubscribe (subscriptionDataChange);
                subscriptionDataChange = null;
            }
            if (subscriptionSyncFinished != null) {
                bus.Unsubscribe (subscriptionSyncFinished);
                subscriptionSyncFinished = null;
            }
        }

        #region Update List
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        private void OnDataChange (DataChangeMessage msg)
        {
            var entry = msg.Data as TimeEntryData;
            if (entry == null) {
                return;
            }

            if (isUpdatingCollection || IsLoading) {
                updateMessageQueue.Enqueue (msg);
            } else {
                ProcessUpdateMessage (msg);
            }
        }

        private async void ProcessUpdateMessage (DataChangeMessage msg)
        {
            isUpdatingCollection = true;

            var entry = msg.Data as TimeEntryData;
            var isExcluded = entry.DeletedAt != null
                             || msg.Action == DataAction.Delete
                             || entry.State == TimeEntryState.New;

            if (isExcluded) {
                await RemoveEntryAsync (entry);
            } else {
                await AddOrUpdateEntryAsync (new TimeEntryData (entry));
            }

            if (updateMessageQueue.Count > 0) {
                ProcessUpdateMessage (updateMessageQueue.Dequeue ());
            }

            isUpdatingCollection = false;
        }

        protected virtual Task AddOrUpdateEntryAsync (TimeEntryData entry)
        {
            return null;
        }

        protected virtual Task RemoveEntryAsync (TimeEntryData entry)
        {
            return null;
        }

        protected virtual async Task UpdateCollectionAsync (object data, NotifyCollectionChangedAction action, int newIndex, int oldIndex = -1, bool isRange = false)
        {
            if (updateMode != UpdateMode.Immediate) {
                return;
            }

            NotifyCollectionChangedEventArgs args;
            if (isRange) {
                args = CollectionEventBuilder.GetRangeEvent (action, newIndex, oldIndex);
            } else {
                args = CollectionEventBuilder.GetEvent (action, newIndex, oldIndex);
            }


            // Update collection.
            if (args.Action == NotifyCollectionChangedAction.Add) {
                if (args.NewItems.Count == 1 && data != null) {
                    if (data is IDateGroup) {
                        ItemCollection.Insert (args.NewStartingIndex, data);
                    } else {
                        var timeEntryList = GetListOfTimeEntries (data);
                        var newHolder = new TimeEntryHolder (timeEntryList);
                        await newHolder.LoadAsync ();
                        ItemCollection.Insert (args.NewStartingIndex, newHolder);
                    }
                } else {
                    var holderTaskList = new List<Task> ();
                    var currentItems = new List<object> (UpdatedList);

                    if (args.NewStartingIndex == 0) {
                        ItemCollection.Clear ();
                    }

                    for (int i = args.NewStartingIndex; i < args.NewStartingIndex + args.NewItems.Count; i++) {
                        var item = currentItems [i];
                        if (item is IDateGroup) {
                            ItemCollection.Insert (i, item);
                        } else {
                            var timeEntryList = GetListOfTimeEntries (item);
                            var timeEntryHolder = new TimeEntryHolder (timeEntryList);
                            ItemCollection.Insert (i, timeEntryHolder);
                            holderTaskList.Add (timeEntryHolder.LoadAsync ());
                        }
                    }
                    await Task.WhenAll (holderTaskList);
                }
            }

            if (args.Action == NotifyCollectionChangedAction.Move) {
                var savedItem = ItemCollection [args.OldStartingIndex];
                ItemCollection.RemoveAt (args.OldStartingIndex);
                ItemCollection.Insert (args.NewStartingIndex, savedItem);
            }

            if (args.Action == NotifyCollectionChangedAction.Remove) {
                ItemCollection.RemoveAt (args.OldStartingIndex);
            }

            if (args.Action == NotifyCollectionChangedAction.Replace) {
                if (data is IDateGroup) {
                    ItemCollection [args.NewStartingIndex] = data;
                } else {
                    var oldHolder = (TimeEntryHolder)ItemCollection.ElementAt (args.NewStartingIndex);
                    var timeEntryList = GetListOfTimeEntries (data);
                    await oldHolder.UpdateAsync (timeEntryList);
                    // Weird case,
                    // For further investigation
                    if (args.NewStartingIndex > ItemCollection.Count) {
                        return;
                    }
                    ItemCollection [args.NewStartingIndex] = oldHolder;
                }
            }

            // Dispatch Observable collection event.
            var handler = CollectionChanged;
            if (handler != null) {
                handler (this, args);
            }
        }

        private List<TimeEntryData> GetListOfTimeEntries (object data)
        {
            var timeEntryList = new List<TimeEntryData> ();

            if (data is TimeEntryData) {
                timeEntryList.Add ((TimeEntryData)data);
            } else if (data is TimeEntryGroup) {
                timeEntryList = ((TimeEntryGroup)data).TimeEntryList;
            }

            return timeEntryList;
        }
        #endregion

        #region TimeEntry operations
        public async void ContinueTimeEntry (TimeEntryHolder timeEntryHolder)
        {
            await TimeEntryModel.ContinueTimeEntryDataAsync (timeEntryHolder.TimeEntryData);

            // Ping analytics
            ServiceContainer.Resolve<ITracker> ().SendTimerStartEvent (TimerStartSource.AppContinue);
        }

        public async void StopTimeEntry (TimeEntryHolder timeEntryHolder)
        {
            await TimeEntryModel.StopAsync (timeEntryHolder.TimeEntryData);

            // Ping analytics
            ServiceContainer.Resolve<ITracker> ().SendTimerStopEvent (TimerStopSource.App);
        }
        #endregion

        #region Undo
        public void RestoreItemFromUndo ()
        {
            if (LastRemovedItem != null) {
                AddTimeEntryHolder (LastRemovedItem);
                LastRemovedItem = null;
            }
        }

        public async void RemoveItemWithUndo (TimeEntryHolder holder)
        {
            // Remove previous if exists
            RemoveItemPermanently (LastRemovedItem);
            if (holder.State == TimeEntryState.Running) {
                await TimeEntryModel.StopAsync (holder.TimeEntryData);
            }
            LastRemovedItem = holder;
            RemoveTimeEntryHolder (holder);
        }

        public void ConfirmItemRemove ()
        {
            RemoveItemPermanently (LastRemovedItem);
        }

        private async void RemoveItemPermanently (TimeEntryHolder holder)
        {
            if (holder == null) {
                return;
            }

            if (holder.TimeEntryDataList.Count > 1) {
                var timeEntryGroup = new TimeEntryGroup (holder.TimeEntryDataList);
                await timeEntryGroup.DeleteAsync ();
            } else {
                await TimeEntryModel.DeleteTimeEntryDataAsync (holder.TimeEntryDataList.First ());
            }
        }

        protected virtual void AddTimeEntryHolder (TimeEntryHolder holder)
        {
        }

        protected virtual void RemoveTimeEntryHolder (TimeEntryHolder holder)
        {
        }
        #endregion

        #region Load
        private void OnSyncFinished (SyncFinishedMessage msg)
        {
            if (reloadScheduled) {
                reloadScheduled = false;
                IsLoading = false;
                Load (true);
            }

            if (subscriptionSyncFinished != null) {
                var bus = ServiceContainer.Resolve<MessageBus> ();
                bus.Unsubscribe (subscriptionSyncFinished);
                subscriptionSyncFinished = null;
            }
        }

        private void BeginUpdate ()
        {
            if (updateMode != UpdateMode.Immediate) {
                return;
            }
            lastNumberOfItems = Count;
            updateMode = UpdateMode.Batch;
        }

        private async void EndUpdate ()
        {
            updateMode = UpdateMode.Immediate;
            if (Count > lastNumberOfItems) {
                await UpdateCollectionAsync (null, NotifyCollectionChangedAction.Add, lastNumberOfItems, Count - lastNumberOfItems, true);
            }
        }

        public void Reload ()
        {
            if (IsLoading) {
                return;
            }

            startFrom = Time.UtcNow;
            DateGroups.Clear ();
            HasMore = true;

            var syncManager = ServiceContainer.Resolve<ISyncManager> ();
            if (syncManager.IsRunning) {
                // Fake loading until initial sync has finished
                IsLoading = true;

                reloadScheduled = true;
                if (subscriptionSyncFinished == null) {
                    var bus = ServiceContainer.Resolve<MessageBus> ();
                    subscriptionSyncFinished = bus.Subscribe<SyncFinishedMessage> (OnSyncFinished);
                }
            } else {
                Load (true);
            }
        }

        public void LoadMore ()
        {
            Load (false);
        }

        private async void Load (bool initialLoad)
        {
            if (IsLoading || !HasMore) {
                return;
            }

            IsLoading = true;
            var client = ServiceContainer.Resolve<ITogglClient> ();

            try {
                var dataStore = ServiceContainer.Resolve<IDataStore> ();
                var endTime = startFrom;
                var startTime = startFrom = endTime - TimeSpan.FromDays (4);

                bool useLocal = false;

                if (initialLoad) {
                    useLocal = true;
                    startTime = startFrom = endTime - TimeSpan.FromDays (9);
                }

                // Try with latest data from server first:
                if (!useLocal) {
                    const int numDays = 5;
                    try {
                        var minStart = endTime;
                        var jsonEntries = await client.ListTimeEntries (endTime, numDays);

                        BeginUpdate ();
                        var entries = await dataStore.ExecuteInTransactionAsync (ctx =>
                                      jsonEntries.Select (json => json.Import (ctx)).ToList ());

                        // Add entries to list:
                        foreach (var entry in entries) {
                            await AddOrUpdateEntryAsync (entry);

                            if (entry.StartTime < minStart) {
                                minStart = entry.StartTime;
                            }
                        }

                        startTime = minStart;
                        HasMore = (endTime.Date - minStart.Date).Days > 0;
                    } catch (Exception exc) {
                        var log = ServiceContainer.Resolve<ILogger> ();
                        if (exc.IsNetworkFailure () || exc is TaskCanceledException) {
                            log.Info (Tag, exc, "Failed to fetch time entries {1} days up to {0}", endTime, numDays);
                        } else {
                            log.Warning (Tag, exc, "Failed to fetch time entries {1} days up to {0}", endTime, numDays);
                        }

                        useLocal = true;
                    }
                }

                // Fall back to local data:
                if (useLocal) {
                    var store = ServiceContainer.Resolve<IDataStore> ();
                    var userId = ServiceContainer.Resolve<AuthManager> ().GetUserId ();

                    var baseQuery = store.Table<TimeEntryData> ()
                                    .OrderBy (r => r.StartTime, false)
                                    .Where (r => r.State != TimeEntryState.New
                                            && r.DeletedAt == null
                                            && r.UserId == userId);
                    var entries = await baseQuery
                                  .QueryAsync (r => r.StartTime <= endTime
                                               && r.StartTime > startTime);

                    BeginUpdate ();
                    foreach (var entry in entries) {
                        await AddOrUpdateEntryAsync (entry);
                    }

                    if (!initialLoad) {
                        var count = await baseQuery
                                    .CountAsync (r => r.StartTime <= startTime);
                        HasMore = count > 0;
                    }
                }
            } catch (Exception exc) {
                var log = ServiceContainer.Resolve<ILogger> ();
                log.Error (Tag, exc, "Failed to fetch time entries");
            } finally {
                IsLoading = false;
                EndUpdate ();
            }
        }

        public event EventHandler OnHasMoreChanged;

        public bool HasMore
        {
            get {
                return hasMore;
            }
            private set {

                if (hasMore == value) {
                    return;
                }

                hasMore = value;

                if (OnHasMoreChanged != null) {
                    OnHasMoreChanged (this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler OnIsLoadingChanged;

        public bool IsLoading
        {
            get {
                return isLoading;
            }
            private set {

                if (isLoading  == value) {
                    return;
                }

                isLoading = value;

                if (OnIsLoadingChanged != null) {
                    OnIsLoadingChanged (this, EventArgs.Empty);
                }
            }
        }

        public IEnumerable<object> Data
        {
            get {
                return ItemCollection;
            }
        }

        protected virtual IList<IDateGroup> DateGroups
        {
            get { return dateGroups; }
        }

        protected IEnumerable<object> UpdatedList
        {
            get {
                foreach (var grp in DateGroups) {
                    yield return grp;
                    foreach (var data in grp.DataObjects) {
                        yield return data;
                    }
                }
            }
        }

        public int Count
        {
            get {
                var itemsCount = DateGroups.Sum (g => g.DataObjects.Count ());
                return DateGroups.Count + itemsCount;
            }
        }

        #endregion

        public interface IDateGroup : IDisposable
        {
            DateTime Date {  get; }

            bool IsRunning { get; }

            TimeSpan TotalDuration { get; }

            IEnumerable<object> DataObjects { get; }
        }

        private enum UpdateMode {
            Immediate,
            Batch,
        }
    }
}

