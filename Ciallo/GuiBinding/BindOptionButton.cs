using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Linq;
using Godot;
using ObservableCollections;
using R3;

namespace Ciallo;

public static class BindOptionButton
{
    extension(OptionButton button)
    {
        /// <summary>
        /// Take enum members as OptionButton items and two-way bind the ReactiveProperty. Will clean existing option items.
        /// </summary>
        /// <param name="property"></param>
        /// <typeparam name="T">Must be enum type.</typeparam>
        public OptionButton BindEnum<T>(ReactiveProperty<T> property, Func<T, string> toName = null) where T : Enum
        {
            toName ??= value => value.ToString();
            var values = (T[])Enum.GetValues(typeof(T));
            button.BindValue(values, property, toName);
            return button;
        }
        /// <summary>
        /// Take list items as the OptionButton items and two-way bind the selection to a ReactiveProperty. Will clean existing option items.
        /// If the current property value is not in the list, the option button will be unselected.
        /// </summary>
        /// <param name="items">The list options.</param>
        /// <param name="property"></param>
        /// <param name="toString"></param>
        /// <param name="subs"></param>
        /// <typeparam name="T">Use `ToString()` as item string.</typeparam>
        public OptionButton BindValue<T>(IReadOnlyList<T> items,
            ReactiveProperty<T> property, Func<T, string> toString, CompositeDisposable subs)
        {
            if (button.AllowReselect) throw new ArgumentException("AllowReselect must be false.");
            button.Clear();
            foreach (var item in items)
                button.AddItem(toString(item));

            property.Subscribe(value => button.Selected = items.IndexOf(value)).AddTo(subs);
            button.OnItemSelectedAsObservable().Subscribe(index =>
            {
                if (index != -1) property.Value = items[(int)index];
                if (index == -1) property.Value = default;
            }).AddTo(subs);
            return button;
        }
        public OptionButton BindValue<T>(IReadOnlyList<T> items,
            ReactiveProperty<T> property, Func<T, string> toString = null)
        {
            toString ??= v => v.ToString();
            var subs = new CompositeDisposable();
            BindValue(button, items, property, toString, subs);
            subs.AddTo(button);
            return button;
        }
        ///---------------------------------------------------------------
        /// Pitfall: OptionButton lacks MoveItem, so we need to rebuild items on Move
        public OptionButton MoveItem(int from, int to)
        {
            var count = button.GetItemCount();
            var texts = new List<string>(count);
            for (int i = 0; i < count; i++)
                texts.Add(button.GetItemText(i));
            var selected = button.Selected;
            var movedText = texts[from];
            texts.RemoveAt(from);
            texts.Insert(to, movedText);
            button.Clear();
            foreach (var t in texts)
                button.AddItem(t);
            // Restore selection
            if (selected == from)
                button.Selected = to;
            else if (from < to && selected > from && selected <= to)
                button.Selected = selected - 1;
            else if (from > to && selected >= to && selected < from)
                button.Selected = selected + 1;
            else
                button.Selected = selected;
            return button;
        }
        public OptionButton ObserveObservableList<T>(ObservableList<T> list,
            Func<T, ReactiveProperty<string>> toName)
        {
            button.Clear();
            var subs = new CompositeDisposable();
            var subList = new List<IDisposable>();

            // Initialize items
            foreach (var item in list)
            {
                var name = toName(item);
                button.AddItem(name.Value);
                var sub = name.Subscribe(s =>
                {
                    var idx = list.Select(toName).ToImmutableArray().IndexOf(name);
                    if (idx != -1) button.SetItemText(idx, s);
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
                        var addName = toName(e.NewItem);
                        button.AddItem(addName.Value);
                        button.MoveItem(button.GetItemCount() - 1, e.NewStartingIndex);
                        var subAdd = addName.Subscribe(s =>
                        {
                            var idx = list.Select(toName).ToImmutableArray().IndexOf(addName);
                            if (idx != -1) button.SetItemText(idx, s);
                        });
                        subAdd.AddTo(subs);
                        subList.Insert(e.NewStartingIndex, subAdd);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        button.RemoveItem(e.OldStartingIndex);
                        subList[e.OldStartingIndex].Dispose();
                        subList.RemoveAt(e.OldStartingIndex);
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        var replaceName = toName(e.NewItem);
                        button.SetItemText(e.NewStartingIndex, replaceName.Value);
                        subList[e.OldStartingIndex].Dispose();
                        var subReplace = replaceName.Subscribe(s =>
                        {
                            var idx = list.Select(toName).ToImmutableArray().IndexOf(replaceName);
                            if (idx != -1) button.SetItemText(idx, s);
                        });
                        subReplace.AddTo(subs);
                        subList[e.NewStartingIndex] = subReplace;
                        break;
                    case NotifyCollectionChangedAction.Move:
                        button.MoveItem(e.OldStartingIndex, e.NewStartingIndex);
                        var moving = subList[e.OldStartingIndex];
                        subList.RemoveAt(e.OldStartingIndex);
                        subList.Insert(e.NewStartingIndex, moving);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        button.Clear();
                        foreach (var old in subList) old.Dispose();
                        subList.Clear();
                        break;
                    default: throw new ArgumentOutOfRangeException();
                }
            }).AddTo(subs);

            subs.AddTo(button);
            return button;
        }
        public OptionButton BindSelectionIndex(ReactiveProperty<int> property)
        {
            var subs = new CompositeDisposable();
            property.Subscribe(value =>
            {
                if (value >= 0 && value < button.GetItemCount())
                    button.Selected = value;
                else
                    button.Selected = -1;
            }).AddTo(subs);

            button.OnItemSelectedAsObservable()
                .Subscribe(index => property.Value = (int)index)
                .AddTo(subs);
            subs.AddTo(button);
            return button;
        }
    }
}
