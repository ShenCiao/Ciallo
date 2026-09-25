using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Godot;
using ObservableCollections;
using R3;

namespace Ciallo;

// Note: DeselectAll does not emit ItemSelected signal
// Shen: being lazy here, not implement output disposable version
public static class BindItemList
{
    // Fix item list
    extension(ItemList control)
    {
        public ItemList BindValue<T>([NotNull] IReadOnlyList<T> items,
            [NotNull] ReactiveProperty<T> property, Func<T, string> toName = null)
        {
            if (control.SelectMode != ItemList.SelectModeEnum.Single) throw new ArgumentException("List must be single selectable", nameof(control));
            toName ??= v => v.ToString();

            control.Clear();
            foreach (var item in items)
                control.AddItem(toName(item));

            var subs = new CompositeDisposable();
            property.Subscribe(value =>
            {
                var idx = items.IndexOf(value);
                if (idx >= 0)
                    control.Select(idx);
                else
                    control.DeselectAll();
            }).AddTo(subs);

            control.SignalAsObservable<long>(ItemList.SignalName.ItemSelected)
                .Subscribe(idx => property.Value = items[(int)idx])
                .AddTo(subs);
            subs.AddTo(control);
            return control;
        }
        /// Binds dynamic list
        public ItemList ObserveObservableList<T>(ObservableList<T> list,
            Func<T, ReactiveProperty<string>> toName)
        {
            if (control.SelectMode != ItemList.SelectModeEnum.Single) throw new ArgumentException("List must be single selectable", nameof(control));
            control.Clear();

            var subs = new CompositeDisposable();
            var subList = new List<IDisposable>();

            // Initialize items
            foreach (var item in list)
            {
                var name = toName(item);
                control.AddItem(name.Value);
                var sub = name.Subscribe(s =>
                {
                    var idx = list.Select(toName).ToImmutableArray().IndexOf(name);
                    if (idx != -1) control.SetItemText(idx, s);
                });
                sub.AddTo(subs);
                subList.Add(sub);
            }

            // Handle dynamic list changes
            list.ObserveChanged().Subscribe(e =>
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        var newName = toName(e.NewItem);
                        control.AddItem(newName.Value);
                        control.MoveItem(control.GetItemCount() - 1, e.NewStartingIndex);
                        var newSub = newName.Subscribe(s =>
                        {
                            var idx = list.Select(toName).ToImmutableArray().IndexOf(newName);
                            if (idx != -1) control.SetItemText(idx, s);
                        }).AddTo(subs);
                        subList.Insert(e.NewStartingIndex, newSub);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        control.RemoveItem(e.OldStartingIndex);
                        subList[e.OldStartingIndex].Dispose();
                        subList.RemoveAt(e.OldStartingIndex);
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        var replaceName = toName(e.NewItem);
                        control.SetItemText(e.NewStartingIndex, replaceName.Value);
                        subList[e.NewStartingIndex].Dispose();
                        var subReplace = replaceName.Subscribe(s =>
                        {
                            var idx = list.Select(toName).ToImmutableArray().IndexOf(replaceName);
                            if (idx != -1) control.SetItemText(idx, s);
                        }).AddTo(subs);
                        subList[e.NewStartingIndex] = subReplace;
                        break;
                    case NotifyCollectionChangedAction.Move:
                        control.MoveItem(e.OldStartingIndex, e.NewStartingIndex);
                        var moving = subList[e.OldStartingIndex];
                        subList.RemoveAt(e.OldStartingIndex);
                        subList.Insert(e.NewStartingIndex, moving);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        control.Clear();
                        foreach (var subOld in subList) subOld.Dispose();
                        subList.Clear();
                        break;
                    default: throw new ArgumentOutOfRangeException();
                }
            }).AddTo(subs);

            subs.AddTo(control);
            return control;
        }
        public ItemList ObserveObservableList(ObservableList<string> list)
        {
            if (control.SelectMode != ItemList.SelectModeEnum.Single) throw new ArgumentException("List must be single selectable", nameof(control));
            control.Clear();

            var subs = new CompositeDisposable();
            // Initialize items
            foreach (var item in list)
                control.AddItem(item);

            // Handle dynamic list changes
            list.ObserveChanged().Subscribe(e =>
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        control.AddItem(e.NewItem);
                        control.MoveItem(control.GetItemCount() - 1, e.NewStartingIndex);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        control.RemoveItem(e.OldStartingIndex);
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        control.SetItemText(e.NewStartingIndex, e.NewItem);
                        break;
                    case NotifyCollectionChangedAction.Move:
                        control.MoveItem(e.OldStartingIndex, e.NewStartingIndex);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        control.Clear();
                        foreach (var v in list)
                            control.AddItem(v);
                        break;
                    default: throw new ArgumentOutOfRangeException();
                }
            }).AddTo(subs);

            subs.AddTo(control);
            return control;
        }
        /// Two-way binding of selection index
        public ItemList BindSelectionIndex(ReactiveProperty<int> index)
        {
            if (control.SelectMode != ItemList.SelectModeEnum.Single) throw new ArgumentException("List must be single selectable", nameof(control));
            var subs = new CompositeDisposable();
            index.Subscribe(value =>
            {
                if (value >= 0 && value < control.GetItemCount())
                    control.Select(value);
                else
                    control.DeselectAll();
            }).AddTo(subs);

            control.SignalAsObservable<long>(ItemList.SignalName.ItemSelected)
                .Subscribe(idx => index.Value = (int)idx)
                .AddTo(subs);
            subs.AddTo(control);
            return control;
        }

        public ItemList BindEnum<T>(ReactiveProperty<T> property, Func<T, string> toName = null) where T : Enum
        {
            toName ??= value => value.ToString();
            var values = (T[])Enum.GetValues(typeof(T));
            control.BindValue(values, property, toName);
            return control;
        }
    }
}
