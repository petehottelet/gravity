using System;
using System.Drawing;
using System.Windows.Forms;

namespace Gravity
{
    internal sealed partial class OrganizerView
    {
        protected override AccessibleObject CreateAccessibilityInstance() { return new PagesAccessible(this); }

        private sealed class PagesAccessible : ControlAccessibleObject
        {
            private readonly OrganizerView view;
            public PagesAccessible(OrganizerView owner) : base(owner) { view = owner; }
            public override string Name { get { return "Document pages"; } set { } }
            public override AccessibleRole Role { get { return AccessibleRole.List; } }
            public override AccessibleStates State { get { return base.State | AccessibleStates.MultiSelectable | AccessibleStates.ExtSelectable; } }
            public override int GetChildCount() { return view.pages.Count; }
            public override AccessibleObject GetChild(int index) { return index >= 0 && index < view.pages.Count ? new PageAccessible(view, index) : null; }
            public override AccessibleObject GetFocused() { return view.Focused ? GetChild(view.focus) : null; }
            public override AccessibleObject GetSelected() { var indices = view.SelectedIndices(); return indices.Count > 0 ? GetChild(indices[0]) : null; }
            public override AccessibleObject HitTest(int x, int y)
            {
                for (int i = 0; i < view.pages.Count; i++) { var child = GetChild(i); if (child.Bounds.Contains(x, y)) return child; }
                return base.HitTest(x, y);
            }
        }

        private sealed class PageAccessible : AccessibleObject
        {
            private readonly OrganizerView view;
            private readonly int index;
            public PageAccessible(OrganizerView owner, int page) { view = owner; index = page; }
            public override string Name { get { return "Page " + (index + 1); } set { } }
            public override string Description { get { return "Select to read or organize this page. Ctrl and Shift extend the selection."; } }
            public override AccessibleRole Role { get { return AccessibleRole.ListItem; } }
            public override AccessibleObject Parent { get { return view.AccessibilityObject; } }
            public override Rectangle Bounds
            {
                get { var rect = view.CellRect(index); rect.Offset(0, -view.ScrollY); return view.RectangleToScreen(rect); }
            }
            public override AccessibleStates State
            {
                get
                {
                    if (index >= view.pages.Count) return AccessibleStates.Unavailable;
                    var state = AccessibleStates.Selectable | AccessibleStates.Focusable;
                    if (view.selected.Contains(view.pages[index])) state |= AccessibleStates.Selected;
                    if (view.Focused && view.focus == index) state |= AccessibleStates.Focused;
                    if (!view.Visible || !view.RectangleToScreen(view.ClientRectangle).IntersectsWith(Bounds)) state |= AccessibleStates.Offscreen;
                    return state;
                }
            }
            public override string DefaultAction { get { return "Select page"; } }
            public override void DoDefaultAction() { Select(AccessibleSelection.TakeFocus | AccessibleSelection.TakeSelection); }
            public override void Select(AccessibleSelection flags)
            {
                if (index >= view.pages.Count) return;
                if ((flags & AccessibleSelection.TakeFocus) != 0) view.Focus();
                if ((flags & AccessibleSelection.TakeSelection) != 0) view.SelectOnly(index);
                else
                {
                    if ((flags & AccessibleSelection.AddSelection) != 0) view.selected.Add(view.pages[index]);
                    if ((flags & AccessibleSelection.RemoveSelection) != 0) view.selected.Remove(view.pages[index]);
                    if ((flags & AccessibleSelection.ExtendSelection) != 0)
                        for (int i = Math.Min(Math.Max(0, view.anchor), index); i <= Math.Max(view.anchor, index); i++) view.selected.Add(view.pages[i]);
                    view.focus = index; view.RaiseSelection(); view.Invalidate();
                }
                view.EnsureVisible(index);
            }
            public override AccessibleObject Navigate(AccessibleNavigation direction)
            {
                int target = direction == AccessibleNavigation.Next || direction == AccessibleNavigation.Down ? index + 1 :
                    direction == AccessibleNavigation.Previous || direction == AccessibleNavigation.Up ? index - 1 : -1;
                return Parent.GetChild(target);
            }
        }
    }

    internal sealed partial class TabStrip
    {
        protected override AccessibleObject CreateAccessibilityInstance() { return new TabsAccessible(this); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); if (selected >= 0) AccessibilityNotifyClients(AccessibleEvents.Focus, selected); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        private sealed class TabsAccessible : ControlAccessibleObject
        {
            private readonly TabStrip view;
            public TabsAccessible(TabStrip owner) : base(owner) { view = owner; }
            public override AccessibleRole Role { get { return AccessibleRole.PageTabList; } }
            public override int GetChildCount() { return view.Tabs.Count + 1; }
            public override AccessibleObject GetChild(int index)
            {
                if (index < 0 || index > view.Tabs.Count) return null;
                return new TabAccessible(view, index, false);
            }
            public override AccessibleObject GetSelected() { return GetChild(view.selected); }
            public override AccessibleObject GetFocused() { return view.Focused ? GetSelected() : null; }
        }

        private sealed class TabAccessible : AccessibleObject
        {
            private readonly TabStrip view;
            private readonly int index;
            private readonly bool close;
            private bool IsTab { get { return index < view.Tabs.Count; } }
            public TabAccessible(TabStrip owner, int tab, bool closeButton) { view = owner; index = tab; close = closeButton; }
            public override string Name
            {
                get { return !IsTab ? "Open a PDF" : (close ? "Close " : "") + view.Tabs[index].Title + (view.Tabs[index].Modified ? ", unsaved changes" : ""); }
                set { }
            }
            public override AccessibleObject Parent { get { return close ? new TabAccessible(view, index, false) : view.AccessibilityObject; } }
            public override AccessibleRole Role { get { return IsTab && !close ? AccessibleRole.PageTab : AccessibleRole.PushButton; } }
            public override Rectangle Bounds { get { return view.RectangleToScreen(!IsTab ? view.AddRect : close ? view.CloseRect(view.TabRect(index)) : view.TabRect(index)); } }
            public override AccessibleStates State
            {
                get
                {
                    var state = AccessibleStates.Focusable;
                    if (IsTab && !close) state |= AccessibleStates.Selectable;
                    if (IsTab && !close && index == view.selected) state |= AccessibleStates.Selected | (view.Focused ? AccessibleStates.Focused : 0);
                    if (!view.Visible || !view.RectangleToScreen(view.ClientRectangle).IntersectsWith(Bounds)) state |= AccessibleStates.Offscreen;
                    return state;
                }
            }
            public override string DefaultAction { get { return !IsTab ? "Open PDF" : close ? "Close document" : "Activate document"; } }
            public override int GetChildCount() { return IsTab && !close ? 1 : 0; }
            public override AccessibleObject GetChild(int child) { return child == 0 && IsTab && !close ? new TabAccessible(view, index, true) : null; }
            public override void DoDefaultAction()
            {
                if (!IsTab) { if (view.AddRequested != null) view.AddRequested(view, EventArgs.Empty); }
                else if (close) { if (view.TabCloseRequested != null) view.TabCloseRequested(index); }
                else { if (view.TabSelected != null) view.TabSelected(index); view.Focus(); }
            }
            public override void Select(AccessibleSelection flags) { if (IsTab && !close) DoDefaultAction(); }
        }
    }
}
