﻿//
// PlayerInterfaceLite.cs
//
// Author:
//   nicholas <>
//
// Copyright 2016 nicholas
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using Banshee.Collection;
using Banshee.Configuration;
using Banshee.Gui;
using Banshee.Gui.Widgets;
using Banshee.I18n;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Sources.Gui;
using Gtk;
using Hyena;
using Hyena.Data.Gui;

namespace Nereid
{
    public class PlayerInterfaceLite : BaseClientWindow, IClientWindow, IDBusObjectName, IService, IDisposable, IHasSourceView
    {
        const string CONFIG_NAMESPACE = "player_window_lite";

        static readonly SchemaEntry<int> WidthSchema = WindowConfiguration.NewWidthSchema (CONFIG_NAMESPACE, 1024);
        static readonly SchemaEntry<int> HeightSchema = WindowConfiguration.NewHeightSchema (CONFIG_NAMESPACE, 700);
        static readonly SchemaEntry<int> XPosSchema = WindowConfiguration.NewXPosSchema (CONFIG_NAMESPACE);
        static readonly SchemaEntry<int> YPosSchema = WindowConfiguration.NewYPosSchema (CONFIG_NAMESPACE);
        static readonly SchemaEntry<bool> MaximizedSchema = WindowConfiguration.NewMaximizedSchema (CONFIG_NAMESPACE);

        VBox _source_box;
        SourceView _source_view;
        ViewContainer _view_container;

        ISourceContents _composite;
        Label _status_label;

        List<System.Action> _ready = new List<System.Action> ();

        IDBusExportable IDBusExportable.Parent {
            get { return null; }
        }

        public string ServiceName {
            get { return "NereidPlayerInterfaceLite"; }
        }

        public string ExportObjectName {
            get { return "ClientWindow"; }
        }

        public Source HighlightedSource {
            get { return _source_view.HighlightedSource; }
        }

        public void ResetHighlight ()
        {
            _source_view.ResetHighlight ();
        }

        public void BeginRenameSource (Source source)
        {
            _source_view.BeginRenameSource (source);
        }


        protected PlayerInterfaceLite (IntPtr ptr) : base (ptr)
        {
        }

        public PlayerInterfaceLite () :
            base (Catalog.GetString ("Banshee Media Player"),
                  new WindowConfiguration (WidthSchema, HeightSchema, XPosSchema, YPosSchema, MaximizedSchema))
        {
        }

        protected override void Initialize ()
        {
            InitialShowPresent ();
        }

        protected override void OnShown ()
        {
            var vbox = new VBox ();

            var views_pane = new HPaned ();
            PersistentPaneController.Control (views_pane, SourceViewWidth);
            _view_container = new ViewContainer ();

            _source_view = new SourceView ();

            var source_scroll = new ScrolledWindow ();

            _view_container.Content = _composite = new SimpleListSourceContents ();

            var toolbar = _Toolbar ();
            var status = _Statusbar ();

            source_scroll.Add (_source_view);

            _source_box = new VBox ();
            _source_box.PackStart (source_scroll, true, true, 0);
            _source_box.PackEnd (_cover_art, false, false, 0);

            views_pane.Pack1 (_source_box, true, true);
            views_pane.Pack2 (_view_container, true, true);

            vbox.PackStart (status, false, false, 0);
            vbox.PackStart (views_pane, true, true, 0);
            vbox.PackStart (toolbar, false, false, 0);

            Add (vbox);

            foreach (var _ in _ready) {
                _ ();
            }

            _ready.Clear ();

            _composite.Widget.Show ();
            _view_container.Show ();
            _source_view.Show ();
            source_scroll.Show ();
            _source_box.Show ();

            CoverArt ();

            status.Show ();
            views_pane.Show ();
            toolbar.Show ();

            vbox.Show ();

            ConnectEvents ();

            base.OnShown ();
        }

        Grid _Toolbar ()
        {
            var grid = new Grid { ColumnHomogeneous = true, MarginLeft = 10, MarginRight = 10 };

            var task_status = new TaskStatusIcon { ShowOnlyBackgroundTasks = false };

            var toolbar = (Toolbar)ActionService.UIManager.GetWidget ("/ControlToolbar");

            toolbar.ShowArrow = false;
            toolbar.ToolbarStyle = ToolbarStyle.Icons;
            toolbar.IconSize = IconSize.SmallToolbar;
            toolbar.Margin = 0;

            var next_button = new NextButton (ActionService) { IconSize = IconSize.SmallToolbar };
            ActionService.PopulateToolbarPlaceholder (toolbar, "/ControlToolbar/NextArrowButton", next_button);

            var repeat_button = new RepeatButton { Relief = ReliefStyle.None };
            ActionService.PopulateToolbarPlaceholder (toolbar, "/ControlToolbar/RepeatButton", repeat_button);

            var seek_slider = new ConnectedSeekSlider ();

            var tools_align = new Alignment (0.5f, 0.5f, 0f, 0f);
            tools_align.Child = toolbar;

            var search_align = new Alignment (0.5f, 0.5f, 0f, 0f);
            search_align.Child = _view_container.SearchEntry;

            //var track_info_display = new ClassicTrackInfoDisplay ();
            //track_info_display.Show ();
            //ActionService.PopulateToolbarPlaceholder (toolbar, "/HeaderToolbar/TrackInfoDisplay", track_info_display, true);

            var lbox = new HBox ();
            lbox.PackStart (task_status, false, false, 0);
            lbox.PackStart (tools_align, true, true, 0);

            var rbox = new HBox ();
            rbox.PackEnd (search_align, true, true, 0);

            grid.Attach (lbox,        0, 0, 1, 1);
            grid.Attach (seek_slider, 1, 0, 1, 1);
            grid.Attach (rbox,        2, 0, 1, 1);

            _ready.Add (() => {
                task_status.Show ();
                next_button.Show ();
                repeat_button.Show ();
                tools_align.Show ();
                search_align.Show ();

                lbox.Show ();
                seek_slider.Show ();
                rbox.Show ();
            });

            return grid;
        }

        Grid _Statusbar ()
        {
            var grid = new Grid { ColumnHomogeneous = true };

            var main_bar = new Toolbar ();

            main_bar.ShowArrow = false;
            main_bar.ToolbarStyle = ToolbarStyle.Icons;
            main_bar.IconSize = IconSize.SmallToolbar;
            main_bar.Margin = 0;

            var menu_button = new MainMenuButton ();

            var toolbar = (Toolbar)ActionService.UIManager.GetWidget ("/SourceToolbar");

            toolbar.ShowArrow = false;
            toolbar.ToolbarStyle = ToolbarStyle.Icons;
            toolbar.IconSize = IconSize.SmallToolbar;
            toolbar.Margin = 0;

            var menu_item = new ToolItem { Child = menu_button };

            main_bar.Insert (menu_item, 0);

            var lbox = new HBox ();
            lbox.PackStart (main_bar, false, false, 0);

            _status_label = new Label ();
            Alignment status_align = new Alignment (0.5f, 0.5f, 1.0f, 1.0f);
            status_align.Add (_status_label);

            var rbox = new HBox ();
            rbox.PackEnd (toolbar, false, false, 0);

            grid.Attach (lbox,         0, 0, 1, 1);
            grid.Attach (status_align, 1, 0, 1, 1);
            grid.Attach (rbox,         2, 0, 1, 1);

            _ready.Add (() => {
                menu_button.Show ();
                menu_item.Show ();
                main_bar.Show ();
                _status_label.Show ();

                lbox.Show ();
                status_align.Show ();
                rbox.Show ();
            });

            return grid;
        }

        protected override void ConnectEvents ()
        {
            base.ConnectEvents ();

            ActionService.SourceActions.SourceView = this;

            ServiceManager.SourceManager.ActiveSourceChanged += OnActiveSourceChanged;
            ServiceManager.SourceManager.SourceUpdated += OnSourceUpdated;

            _view_container.SearchEntry.Changed += (o, x) => {
                var source = ServiceManager.SourceManager.ActiveSource;
                if (source == null)
                    return;

                source.FilterType = (TrackFilterType)_view_container.SearchEntry.ActiveFilterID;
                source.FilterQuery = _view_container.SearchEntry.Query;
            };

            _source_view.RowActivated += (o, x) => {
                Source source = ServiceManager.SourceManager.ActiveSource;
                var handler = source.Properties.Get<System.Action> ("ActivationAction");
                if (handler != null) {
                    handler ();
                } else if (source is ITrackModelSource) {
                    ServiceManager.PlaybackController.NextSource = (ITrackModelSource)source;
                    ServiceManager.PlaybackController.Next ();
                }
            };

            ((ToggleAction)ActionService.ViewActions ["ShowCoverArtAction"]).Active = ShowCoverArt.Get ();
            ActionService.ViewActions ["ShowCoverArtAction"].Activated += (o, a) => {
                ShowCoverArt.Set ((o as Gtk.ToggleAction).Active);

                CoverArt ();
            };

            _source_box.Parent.SizeAllocated += (o, x) => {
                _cover_art.HeightRequest = _source_box.Allocation.Width;
            };
        }

        void CoverArt ()
        {
            if (ShowCoverArt.Get ()) {
                _cover_art.Child = new CoverArtDisplay ();
                _cover_art.Child.Show ();
                _cover_art.Show ();
            } else if (null != _cover_art.Child) {
                var widget = _cover_art.Child;
                _cover_art.Remove (_cover_art.Child);
                _cover_art.Hide ();
                widget.Dispose ();
            }
        }

        readonly Alignment _cover_art = new Alignment (0.0f, 0.0f, 1f, 1f);

        bool _accel_disabled;

        protected bool AccelDisabled {
            get {
                return _accel_disabled;
            }
            set {
                if (value == _accel_disabled) {
                    return;
                }

                _accel_disabled = value;

                if (value) {
                    RemoveAccelGroup (ActionService.UIManager.AccelGroup);
                } else {
                    AddAccelGroup (ActionService.UIManager.AccelGroup);
                }
            }
        }

        protected override void OnSetFocus (Widget focus)
        {
            AccelDisabled = focus is Entry || focus is IDisableKeybindings;

            base.OnSetFocus (focus);
        }

        private bool _disposed;

        protected override void Dispose (bool disposing)
        {
            if (_disposed) return;

            _disposed = true;

            ServiceManager.SourceManager.ActiveSourceChanged -= OnActiveSourceChanged;
            ServiceManager.SourceManager.SourceUpdated -= OnSourceUpdated;

            base.Dispose (disposing);
        }

        void OnActiveSourceChanged (SourceEventArgs args)
        {
            Set (args.Source);
        }

        void OnSourceUpdated (SourceEventArgs args)
        {
            Update (args.Source);
        }

        void Update (Source source)
        {
            if (source != _view_container.Content.Source) {
                return;
            }

            _status_label.Text = source.GetStatusText ();

            var msg = source.Properties.Get<string> ("SearchEntryDescription") ?? Catalog.GetString ("Search");
            _view_container.SearchEntry.EmptyMessage = msg;
            _view_container.SearchEntry.TooltipText = msg;
        }

        void Set (Source source)
        {
            if (source == null) {
                return;
            }

            if (ServiceManager.SourceManager.ActiveSource != source) {
                return;
            }

            if (source == _view_container.Content.Source) {
                return;
            }

            _view_container.Content.ResetSource ();

            var contents = source.GetProperty<ISourceContents> ("Nereid.SourceContents", source.GetInheritedProperty<bool> ("Nereid.SourceContentsPropagate"));

            _view_container.ClearHeaderWidget ();
            _view_container.ClearFooter ();

            if (contents != null) {
                _view_container.Content = contents;
            } else if (source is ITrackModelSource) {
                _view_container.Content = _composite;
            } else {
                _view_container.Content = new _SourceContents ();
            }

            _view_container.Content.SetSource (source);

            if (_view_container.Visible && _view_container.Content is ITrackModelSourceContents) {
                var track_content = (ITrackModelSourceContents) _view_container.Content;
                source.Properties.Set ("Track.IListView", track_content.TrackView);
            }

            if (source.Properties.Contains ("Nereid.SourceContents.HeaderWidget")) {
                var widget = source.Properties.Get<Widget> ("Nereid.SourceContents.HeaderWidget");
                _view_container.SetHeaderWidget (widget);
            }

            if (source.Properties.Contains ("Nereid.SourceContents.FooterWidget")) {
                var widget = source.Properties.Get<Widget> ("Nereid.SourceContents.FooterWidget");
                _view_container.SetFooter (widget);
            }

            //if (source is ITrackModelSource) {
            //    previous_track_model = (source as ITrackModelSource).TrackModel;
            //    previous_track_model.Reloaded += OnTrackModelReloaded;
            //}

            _view_container.SearchEntry.Ready = false;
            _view_container.SearchEntry.CancelSearch ();

            Update (source);

            if (source.FilterQuery != null) {
                _view_container.SearchEntry.Query = source.FilterQuery;
                _view_container.SearchEntry.ActivateFilter ((int)source.FilterType);
            }

            _view_container.SearchSensitive = source.CanSearch;
            _view_container.SearchEntry.Ready = true;

            _view_container.Show ();
        }

        class _SourceContents : ScrolledWindow, ISourceContents
        {
            public ISource Source { get; private set; }

            public Widget Widget {
                get { return this; }
            }

            public void ResetSource ()
            {
                Source = null;
            }

            public bool SetSource (ISource source)
            {
                Source = source;

                return true;
            }
        }

        static readonly SchemaEntry<int> SourceViewWidth = new SchemaEntry<int> (
            CONFIG_NAMESPACE, "source_view_width",
            175,
            "Source View Width",
            "Width of Source View Column."
        );

        static readonly SchemaEntry<bool> ShowCoverArt = new SchemaEntry<bool> (
            CONFIG_NAMESPACE, "show_cover_art",
            false,
            "Show cover art",
            "Show cover art below source view if available"
        );
    }
}