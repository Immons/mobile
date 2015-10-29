using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Toggl.Phoebe.Data.Views
{
    public interface ICollectionDataView<T> : INotifyCollectionChanged, INotifyPropertyChanged, IDisposable
    {
        IEnumerable<T> Data { get; }

        int Count { get; }

        bool HasMore { get; }

        void Reload();

        void LoadMore();

        bool IsLoading { get; }

        event EventHandler OnIsLoadingChanged;

        event EventHandler OnHasMoreChanged;
    }
}