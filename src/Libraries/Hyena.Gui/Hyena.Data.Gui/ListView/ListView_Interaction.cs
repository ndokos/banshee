//
// ListView_Interaction.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2007-2008 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using Gtk;

using Hyena.Collections;
using Selection = Hyena.Collections.Selection;

namespace Hyena.Data.Gui
{
    public partial class ListView<T> : Widget
    {
        private Adjustment vadjustment;
        public Adjustment Vadjustment {
            get { return vadjustment; }
        }
        
        private Adjustment hadjustment;
        public Adjustment Hadjustment {
            get { return hadjustment; }
        }
        
        private SelectionProxy selection_proxy = new SelectionProxy ();
        public SelectionProxy SelectionProxy {
            get { return selection_proxy; }
        }

        public Selection Selection {
            get { return model.Selection; }
        }
        
        public event RowActivatedHandler<T> RowActivated;
        
#region Row/Selection, Keyboard/Mouse Interaction

        private bool KeyboardScroll (Gdk.ModifierType modifier, int relative_row, bool align_y)
        {
            int row_limit;
            if (relative_row < 0) {
                if (Selection.FocusedIndex == -1) {
                    return false;
                }
                
                row_limit = 0;
            } else {
                row_limit = Model.Count - 1;
            }

            if (Selection.FocusedIndex == row_limit) {
                return true;
            }
            
            int row_index = Math.Min (Model.Count - 1, Math.Max (0, Selection.FocusedIndex + relative_row));

            if (Selection != null) {
                if ((modifier & Gdk.ModifierType.ControlMask) != 0) {
                    // Don't change the selection
                } else if ((modifier & Gdk.ModifierType.ShiftMask) != 0) {
                    // Behave like nautilus: if and arrow key + shift is pressed and the currently focused item
                    // is not selected, select it and don't move the focus or vadjustment.
                    // Otherwise, select the new row and scroll etc as necessary.
                    if (relative_row * relative_row != 1) {
                        Selection.SelectFromFirst (row_index, true);
                    } else if (Selection.Contains (Selection.FocusedIndex)) {
                        Selection.SelectFromFirst (row_index, true);
                    } else {
                        Selection.Select (Selection.FocusedIndex);
                        return true;
                    }
                } else {
                    Selection.Clear (false);
                    Selection.Select (row_index);
                }
            }
            
            // Scroll if needed
            double y_at_row = GetYAtRow (row_index);
            if (align_y) {
                if (y_at_row < vadjustment.Value) {
                    ScrollTo (y_at_row);
                } else if ((y_at_row + RowHeight) > (vadjustment.Value + vadjustment.PageSize)) {
                    ScrollTo (y_at_row + RowHeight - (vadjustment.PageSize));
                }
            } else {
                ScrollTo (vadjustment.Value + ((row_index - Selection.FocusedIndex) * RowHeight));
            }

            Selection.FocusedIndex = row_index;
            InvalidateList ();
            return true;
        }
        
        protected override bool OnKeyPressEvent (Gdk.EventKey press)
        {
            bool handled = false;

            switch (press.Key) {
                case Gdk.Key.a:
                    if ((press.State & Gdk.ModifierType.ControlMask) != 0) {
                        SelectionProxy.Selection.SelectAll ();
                        handled = true;
                    }
                    break;

                case Gdk.Key.A:
                    if ((press.State & Gdk.ModifierType.ControlMask) != 0) {
                        SelectionProxy.Selection.Clear ();
                        handled = true;
                    }
                    break;

                case Gdk.Key.k:
                case Gdk.Key.K:
                case Gdk.Key.Up:
                case Gdk.Key.KP_Up:
                    handled = KeyboardScroll (press.State, -1, true);
                    break;

                case Gdk.Key.j:
                case Gdk.Key.J:
                case Gdk.Key.Down:
                case Gdk.Key.KP_Down:
                    handled = KeyboardScroll (press.State, 1, true);
                    break;

                case Gdk.Key.Page_Up:
                case Gdk.Key.KP_Page_Up:
                    handled = KeyboardScroll (press.State, 
                        (int)(-vadjustment.PageIncrement / (double)RowHeight), false);
                    break;

                case Gdk.Key.Page_Down:
                case Gdk.Key.KP_Page_Down:
                    handled = KeyboardScroll (press.State, 
                        (int)(vadjustment.PageIncrement / (double)RowHeight), false);
                    break;

                case Gdk.Key.Home:
                case Gdk.Key.KP_Home:
                    handled = KeyboardScroll (press.State, -10000000, false);
                    break;

                case Gdk.Key.End:
                case Gdk.Key.KP_End:
                    handled = KeyboardScroll (press.State, 10000000, false);
                    break;

                case Gdk.Key.Return:
                case Gdk.Key.KP_Enter:
                    if (Selection != null && Selection.FocusedIndex != -1) {
                        Selection.Clear (false);
                        Selection.Select (Selection.FocusedIndex);
                        OnRowActivated ();
                        handled = true;
                    }
                    break;
                
                case Gdk.Key.space:
                    if (Selection != null && Selection.FocusedIndex != 1) {
                        Selection.ToggleSelect (Selection.FocusedIndex);
                        handled = true;
                    }
                    break;
            }

            if (handled) {
                return true;
            }
            
            return base.OnKeyPressEvent (press);
        }
        
#region Cell Event Proxy        
        
        private IInteractiveCell last_icell;
        private Gdk.Rectangle last_icell_area;
        
        private void ProxyEventToCell (Gdk.Event evnt, bool press)
        {
            IInteractiveCell icell;
            Gdk.Rectangle [] damage = new Gdk.Rectangle[2];
            damage[0] = ProxyEventToCellNoDraw (evnt, press, out icell);
            
            if (last_icell != null && last_icell != icell) {
                if (last_icell.PointerLeaveEvent ()) {
                    damage[1] = last_icell_area;
                }
                
                last_icell = null;
                last_icell_area = Gdk.Rectangle.Zero;
            }
            
            if (damage[0].Equals (damage[1])) {
                if (damage[0].Equals (Gdk.Rectangle.Zero)) {
                    return;
                }
                
                // FIXME: Should be QueueDrawArea (damage[0]...)
                QueueDraw ();
            } else {
                // FIXME: When QueueDrawArea works, use this
                // foreach (Gdk.Rectangle area in damage) {
                //     if (!area.Equals (Gdk.Rectangle.Zero)) {
                //         QueueDrawArea (area.X, area.Y, area.Width, area.Height);
                //     }
                // }
                
                QueueDraw (); 
            }
        }
        
        private Gdk.Rectangle ProxyEventToCellNoDraw (Gdk.Event evnt, bool press, out IInteractiveCell icell)
        {
            int evnt_x, evnt_y;
            Gdk.Rectangle damage = Gdk.Rectangle.Zero;
            
            Gdk.EventButton evnt_button = evnt as Gdk.EventButton;
            Gdk.EventMotion evnt_motion = evnt as Gdk.EventMotion;
            
            bool redraw = false;
            icell = null;
            
            if (evnt_motion != null) {
                evnt_x = (int)evnt_motion.X;
                evnt_y = (int)evnt_motion.Y;
            } else if (evnt_button != null) {
                evnt_x = (int)evnt_button.X;
                evnt_y = (int)evnt_button.Y;
            } else {
                // Possibly EventCrossing, for the leave event
                return damage;
            }
            
            int y = evnt_y - list_interaction_alloc.Y;
            int x = evnt_x - list_interaction_alloc.X;
            
            int row_index = GetRowAtY (y);
            if (row_index < 0 || row_index >= Model.Count) {
                return damage;
            }
            
            Column column = GetColumnAt (x);
            if (column == null) {
                return damage;
            }
            
            CachedColumn cached_column = GetCachedColumnForColumn (column);
            
            ColumnCell cell = column.GetCell (0);
            icell = cell as IInteractiveCell;
            if (icell == null) {
                return damage;
            }
            
            last_icell = icell;
            
            // Turn the view-absolute coordinates into cell-relative coordinates
            x -= cached_column.X1;
            int page_offset = (int)vadjustment.Value % RowHeight;
            y = (y + page_offset) % RowHeight;
            
            // Bind the row to the cell and then send it a synthesized input event
            cell.BindListItem (model[row_index]);
            
            if (evnt_motion != null) {
                redraw |= icell.MotionEvent (x, y, evnt_motion);
            } else if (evnt_button != null) {
                redraw |= icell.ButtonEvent (x, y, press, evnt_button);
            }
            
            // FIXME: This rectangle might not be correct, but I don't
            // think QueueDrawArea works at all... maybe Scott has
            // some insight? I smell issues with the blit canvases
            // --Aaron
            if (redraw) {
                damage.X = cached_column.X1;
                damage.Y = (int)GetYAtRow (row_index);
                damage.Width = cached_column.Width;
                damage.Height = RowHeight;
                
                last_icell_area = damage;
            }
            
            return damage;
        }   
        
#endregion
        
#region OnButtonPress

        protected override bool OnButtonPressEvent (Gdk.EventButton evnt)
        {
            HasFocus = true;
            if (header_visible && header_interaction_alloc.Contains ((int)evnt.X, (int)evnt.Y)) {
                return OnHeaderButtonPressEvent (evnt);
            } else if (list_interaction_alloc.Contains ((int)evnt.X, (int)evnt.Y) && model != null) {
                return OnListButtonPressEvent (evnt);
            }
            return true;
        }
        
        private bool OnHeaderButtonPressEvent (Gdk.EventButton evnt)
        {
            int x = (int)evnt.X - header_interaction_alloc.X;
            int y = (int)evnt.Y - header_interaction_alloc.Y;
            
            if (evnt.Button == 3 && ColumnController.EnableColumnMenu) {
                Column menu_column = GetColumnAt (x);
                if (menu_column != null) {
                    OnColumnRightClicked (menu_column, x + Allocation.X, y + Allocation.Y);
                }
                return true;
            } else if (evnt.Button != 1) {
                return true;
            }
            
            Gtk.Drag.SourceUnset (this);
            
            Column column = GetColumnForResizeHandle (x);
            if (column != null) {
                resizing_column_index = GetCachedColumnForColumn (column).Index;
            } else {
                column = GetColumnAt (x);
                if (column != null) {
                    CachedColumn column_c = GetCachedColumnForColumn (column);
                    pressed_column_index = column_c.Index;
                    pressed_column_x_start = x;
                    pressed_column_x_offset = pressed_column_x_start - column_c.X1;
                    pressed_column_x_start_hadjustment = (int)hadjustment.Value;
                }
            }
            
            return true;
        }
        
        private bool OnListButtonPressEvent (Gdk.EventButton evnt)
        {
            int y = (int)evnt.Y - list_interaction_alloc.Y;
            
            GrabFocus ();
            
            int row_index = GetRowAtY (y);

            if (row_index < 0 || row_index >= Model.Count) {
                return true;
            }
            
            ProxyEventToCell (evnt, true);
            
            if (Selection != null && evnt.Button == 1 && evnt.Type != Gdk.EventType.TwoButtonPress && 
                (evnt.State & Gdk.ModifierType.ControlMask) == 0 && Selection.Contains (row_index)) {
                return true;
            }

            object item = model[row_index];
            if (item == null) {
                return true;
            }

            if (evnt.Button == 1 && evnt.Type == Gdk.EventType.TwoButtonPress) {
                OnRowActivated ();
            } else if (Selection != null) {
                if ((evnt.State & Gdk.ModifierType.ControlMask) != 0) {
                    if (evnt.Button == 3) {
                        if (!Selection.Contains (row_index)) {
                            Selection.Select (row_index);
                        }
                    } else {
                        Selection.ToggleSelect (row_index);
                    }
                } else if ((evnt.State & Gdk.ModifierType.ShiftMask) != 0) {
                    Selection.SelectFromFirst (row_index, true);
                } else {
                    if (evnt.Button == 3) {
                        if (!Selection.Contains (row_index)) {
                            Selection.Clear (false);
                            Selection.Select (row_index);
                        }
                    } else {
                        Selection.Clear (false);
                        Selection.Select (row_index);
                    }
                }

                FocusRow (row_index);

                if (evnt.Button == 3) {
                    OnPopupMenu ();
                }
            }
            
            InvalidateList ();
            return true;
        }
        
#endregion

#region OnButtonRelease
        
        protected override bool OnButtonReleaseEvent (Gdk.EventButton evnt)
        {
            OnDragSourceSet ();
            StopDragScroll ();
            
            if (resizing_column_index >= 0) {
                pressed_column_index = -1;
                resizing_column_index = -1;
                GdkWindow.Cursor = null;
                return true;
            }
            
            if (pressed_column_index >= 0 && pressed_column_is_dragging) {
                pressed_column_is_dragging = false;
                pressed_column_index = -1;
                GdkWindow.Cursor = null;
                InvalidateHeader ();
                InvalidateList ();
                return true;
            }
            
            if (header_visible && header_interaction_alloc.Contains ((int)evnt.X, (int)evnt.Y)) {
                return OnHeaderButtonRelease (evnt);
            } else if (list_interaction_alloc.Contains ((int)evnt.X, (int)evnt.Y) && model != null &&
                (evnt.State & (Gdk.ModifierType.ShiftMask | Gdk.ModifierType.ControlMask)) == 0) {
                ProxyEventToCell (evnt, false);
                return OnListButtonRelease (evnt);
            }

            return true;
        }
        
        private bool OnHeaderButtonRelease (Gdk.EventButton evnt)
        {
            if (pressed_column_index >= 0 && pressed_column_index < column_cache.Length) {
                Column column = column_cache[pressed_column_index].Column;
                if (column != null && Model is ISortable && column is ISortableColumn) {
                    ((ISortable)Model).Sort ((ISortableColumn)column);
                    Model.Reload ();
                    RecalculateColumnSizes ();
                    RegenerateColumnCache ();
                    InvalidateHeader ();
                }
                
                pressed_column_index = -1;
                return true;
            } else {
                return false;
            }
        }
        
        private bool OnListButtonRelease (Gdk.EventButton evnt)
        {
            int y = (int)evnt.Y - list_interaction_alloc.Y;
            
            GrabFocus ();
            
            int row_index = GetRowAtY (y);

            if (row_index >= Model.Count) {
                return true;
            }

            object item = model[row_index];
            if (item == null) {
                return true;
            }
            
            if (Selection != null && Selection.Contains (row_index) && Selection.Count > 1) {
                Selection.Clear (false);
                Selection.Select (row_index);
                FocusRow (row_index);
            }
            
            return true;
        }
        
#endregion
        
        protected override bool OnMotionNotifyEvent (Gdk.EventMotion evnt)
        {
            int x = (int)evnt.X - header_interaction_alloc.X;
            
            if (pressed_column_index >= 0 && !pressed_column_is_dragging && 
                Gtk.Drag.CheckThreshold (this, pressed_column_x_start, 0, x, 0)) {
                pressed_column_is_dragging = true;
                InvalidateHeader ();
                InvalidateList ();
            }
            
            pressed_column_x = x;
            
            if (OnMotionNotifyEvent (x)) {
                return true;
            }
            
            GdkWindow.Cursor = (resizing_column_index >= 0 || GetColumnForResizeHandle (x) != null) &&
                header_interaction_alloc.Contains ((int)evnt.X, (int)evnt.Y) 
                ? resize_x_cursor 
                : null;
            
            if (resizing_column_index >= 0) {
                ResizeColumn (x);
            }
            
            ProxyEventToCell (evnt, false);
            
            return true;
        }
        
        private bool OnMotionNotifyEvent (int x)
        {
            if (!pressed_column_is_dragging) {
                return false;
            }
            
            OnDragScroll (OnDragHScrollTimeout, header_interaction_alloc.Width * 0.1, header_interaction_alloc.Width, x);
            
            GdkWindow.Cursor = drag_cursor;
            
            Column swap_column = GetColumnAt (x);
            
            if (swap_column != null) {
                CachedColumn swap_column_c = GetCachedColumnForColumn (swap_column);
                bool reorder = false;
                
                if (swap_column_c.Index < pressed_column_index) {
                    // Moving from right to left
                    reorder = pressed_column_x_drag <= swap_column_c.X1 + swap_column_c.Width / 2;
                } else if (swap_column_c.Index > pressed_column_index) {
                    // Moving from left to right
                    reorder = pressed_column_x_drag + column_cache[pressed_column_index].Width >= 
                        swap_column_c.X1 + swap_column_c.Width / 2;
                }
                
                if (reorder) {
                    int actual_pressed_index = ColumnController.IndexOf (column_cache[pressed_column_index].Column);
                    int actual_swap_index = ColumnController.IndexOf (swap_column_c.Column);
                    ColumnController.Reorder (actual_pressed_index, actual_swap_index);
                    pressed_column_index = swap_column_c.Index;
                    RegenerateColumnCache ();
                }
            }
            
            pressed_column_x_drag = x - pressed_column_x_offset - (pressed_column_x_start_hadjustment - (int)hadjustment.Value);
            
            QueueDraw ();
            return true;
        }
        
        private bool OnDragHScrollTimeout ()
        {
            ScrollTo (hadjustment, hadjustment.Value + (drag_scroll_velocity * drag_scroll_velocity_max));
            OnMotionNotifyEvent (pressed_column_x);
            return true;
        }
        
        protected override bool OnLeaveNotifyEvent (Gdk.EventCrossing evnt)
        {
            GdkWindow.Cursor = null;
            if (evnt.Mode == Gdk.CrossingMode.Normal) {
                ProxyEventToCell (evnt, false);
            }
            return base.OnLeaveNotifyEvent (evnt);
        }
        
        protected override bool OnFocusInEvent (Gdk.EventFocus evnt)
        {
            return base.OnFocusInEvent (evnt);
        }
        
        protected override bool OnFocusOutEvent (Gdk.EventFocus evnt)
        {
            return base.OnFocusOutEvent (evnt);
        }
        
        protected virtual void OnRowActivated ()
        {
            if (Selection.FocusedIndex != -1) {
                RowActivatedHandler<T> handler = RowActivated;
                if (handler != null) {
                    handler (this, new RowActivatedArgs<T> (Selection.FocusedIndex, model[Selection.FocusedIndex]));
                }
            }
        }
        
        protected int GetRowAtY (int y)
        {
            if (y < 0) {
                return -1;
            }
            
            int page_offset = (int)vadjustment.Value % RowHeight;
            int first_row = (int)vadjustment.Value / RowHeight;
            int row_offset = (y + page_offset) / RowHeight;
            
            return first_row + row_offset;
        }

        protected double GetYAtRow (int row)
        {
            double y = (double)RowHeight * row;
            return y;
        }
          
        private void FocusRow (int index)
        {
            Selection.FocusedIndex = index;
        }

#endregion

#region Adjustments & Scrolling
        
        private void UpdateAdjustments ()
        {
            UpdateAdjustments (null, null);
        }
        
        private void UpdateAdjustments (Adjustment hadj, Adjustment vadj)
        {
            if (hadj != null) {
                hadjustment = hadj;
            }
            
            if (vadj != null) {
                vadjustment = vadj;
            }
            
            if (hadjustment != null) {
                hadjustment.Upper = header_width;
                hadjustment.StepIncrement = 10.0;
                if (hadjustment.Value + hadjustment.PageSize > hadjustment.Upper) {
                    hadjustment.Value = hadjustment.Upper - hadjustment.PageSize;
                }
            }
            
            if (vadjustment != null && model != null) {
                vadjustment.Upper = (RowHeight * (model.Count));
                vadjustment.StepIncrement = RowHeight;
                if (vadjustment.Value + vadjustment.PageSize > vadjustment.Upper) {
                    vadjustment.Value = vadjustment.Upper - vadjustment.PageSize;
                }
            }
            
            if (hadjustment != null) {
                hadjustment.Change ();
            }

            if (vadjustment != null) {
                vadjustment.Change ();
            }
        }
        
        private void OnHadjustmentChanged (object o, EventArgs args)
        {
            InvalidateHeader ();
            InvalidateList (false);
        }
        
        private void OnVadjustmentChanged (object o, EventArgs args)
        {
            InvalidateList (false);
        }
        
        public void ScrollTo (double val)
        {
            ScrollTo (vadjustment, val);
        }
        
        private void ScrollTo (Adjustment adjustment, double val)
        {
            if (adjustment != null) {
                adjustment.Value = Math.Max (0.0, Math.Min (val, adjustment.Upper - adjustment.PageSize));
            }
        }

        public void ScrollTo (int index)
        {
            ScrollTo (GetYAtRow (index));
        }

        public void CenterOn (int index)
        {
            ScrollTo (index - RowsInView/2 + 1);
        }
                
        protected override void OnSetScrollAdjustments (Adjustment hadj, Adjustment vadj)
        {
            if (hadj == null || vadj == null) {
                return;
            }
            
            hadj.ValueChanged += OnHadjustmentChanged;
            vadj.ValueChanged += OnVadjustmentChanged;
            
            UpdateAdjustments (hadj, vadj);
        }

#endregion
        
    }
}
