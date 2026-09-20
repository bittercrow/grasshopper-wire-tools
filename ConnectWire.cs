using Eto.Drawing;
using Eto.Forms;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

using Rhino;
using Rhino.UI;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WireTools
{
    #region TODOs 
    // TODO: add 3 states to sorting by clicking on headers
    // todo: grey out rows which are not in the current document
    // todo: feat: add connect button 
    #endregion

    class ConnectWireForm : Form
    {
        readonly FilterCollection<ComponentData> _candidateRows = new FilterCollection<ComponentData>();
        readonly FilterCollection<ComponentData> _inputRows = new FilterCollection<ComponentData>();
        readonly FilterCollection<ComponentData> _outputRows = new FilterCollection<ComponentData>();
        readonly GridView _candidateGridView;
        readonly GridView _leftGridView;
        readonly GridView _rightGridView;
        readonly SearchBox _searchBox;
        GH_Document _ghDocument;
        GH_NamedView _ghNamedView = new GH_NamedView();
        int _namedViewLength = 200;
        float _zoomFactor = 2;

        /// <summary>
        /// Initializes a new instance of the ConnectWireForm class.
        /// </summary>
        internal ConnectWireForm()
        {
            //HACK: TryUpdateGhDocument();
            var editor = Instances.EtoDocumentEditor;

            // Layout
            var layout = new DynamicLayout
            {
                Padding = new Padding(5),
                Spacing = new Size(5, 10)
            };

            // Row 1: SearchBox and Candidate List
            layout.BeginCentered();
            _searchBox = CreateSearchBox();
            layout.Add(_searchBox);
            _candidateGridView = CreateCandidateGridView();
            layout.Add(_candidateGridView);
            layout.EndCentered();

            // Row 2: Update Button
            layout.AddSeparateRow(null, null, false, false, CreateAddButtons());

            // Row 3: Input Param List and Output Param List
            layout.BeginVertical(yscale: true);
            layout.BeginHorizontal();
            _leftGridView = CreateLeftGridView();
            layout.Add(_leftGridView, xscale: true, yscale: true);
            _rightGridView = CreateRightGridView();
            layout.Add(_rightGridView, xscale: true, yscale: true);
            layout.EndHorizontal();
            layout.EndVertical();

            // Row 4: Buttons
            layout.AddSeparateRow(null, null, false, false, CreateButtons());

            // Form
            Title = "Connect Wire";
            Padding = new Padding(5);
            Owner = editor ?? throw new InvalidOperationException("Grasshopper not found.");
            Content = layout;
            this.UseRhinoStyle();
        }

        #region SearchBox
        SearchBox CreateSearchBox()
        {
            var searchBox = new SearchBox
            {
                Width = 300,
                PlaceholderText = "Type Name, Nickname or Group Name..."
            };
            searchBox.TextChanging += OnSearchTextChanging;
            searchBox.TextChanged += OnSearchTextChanged;
            return searchBox;
        }

        void OnSearchTextChanging(object sender, TextChangingEventArgs e)
        {
            if (string.IsNullOrEmpty(e.OldText) && !string.IsNullOrEmpty(e.NewText))
            {
                //HACK: RefreshComponentData();
                SetCurrentView();
            }
        }

        void RefreshComponentData()
        {
            TryUpdateGhDocument();
            TryBuildRows();
        }

        bool TryUpdateGhDocument()
        {
            var doc = Instances.ActiveCanvas?.Document;
            if (doc == null)
            {
                return false;
            }

            _ghDocument = doc;
            return true;
        } 

        bool TryBuildRows()
        {
            if (_ghDocument == null)
            {
                return false;
            }

            var objs = _ghDocument.Objects;
            var filtered = objs.Where(obj => obj is IGH_Component or IGH_Param);

            _candidateRows.Clear();

            foreach (var obj in filtered)
            {
                _candidateRows.Add(new ComponentData(obj));
            }

            _candidateRows.Refresh();  // This refresh method is required after the canvas changes.

            return true;
        }

        void SetCurrentView()
        {
            var canvas = Instances.ActiveCanvas;
            if (canvas != null)
            {
                _ghNamedView.Zoom = _zoomFactor;
                _ghNamedView.LoadFromViewport(canvas.Viewport, GH_NamedViewType.center);

            }
        }

        void OnSearchTextChanged(object sender, EventArgs e) => Search(_searchBox.Text);

        void Search(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                _candidateGridView.Visible = false;
                _candidateGridView.UnselectAll();
                return;
            }

            var pattern = "*" + text + "*";
            _candidateRows.Filter = row =>
            {
                return MatchesWildcard(row.Name, pattern) || MatchesWildcard(row.NickName, pattern) || MatchesWildcard(row.Groups, pattern);
            };

            if (_candidateRows.Count > 0)
            {
                _candidateGridView.Height = ChangeGridHeight(_candidateRows.Count);
                _candidateGridView.Visible = true;
            }
            else
            {
                _candidateGridView.Visible = false;
            }
        }

        static bool MatchesWildcard(string value, string pattern)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase);
        }

        int ChangeGridHeight(int rowCount)
        {
            int maxRowsToShow = 5; // Cap the display so it acts like a dropdown overlay
            int rowsToCalculate = Math.Min(rowCount, maxRowsToShow);

            int rowHeight = 25; // Eto rows are roughly 22-26 pixels tall
            int headerHeight = 28; // Approximate height for the column headers
            int paddingSafety = 4; // Top/bottom border padding thickness

            return (rowsToCalculate * rowHeight) + headerHeight + paddingSafety;
        }
        #endregion

        #region Candidate GridView
        GridView CreateCandidateGridView()
        {
            var gridView = new GridView
            {
                ShowHeader = true,
                AllowMultipleSelection = true,
                DataStore = _candidateRows,
                ContextMenu = CreateContextMenu()
            };

            gridView.ColumnHeaderClick += OnCandidateGridViewColumnHeaderClicked;
            gridView.SelectedItemsChanged += OnSelectedItemsChanged;
            gridView.KeyDown += OnCandidateGridViewKeyDown;

            gridView.Columns.Add(new GridColumn
            {
                HeaderText = ColumnHeaders.Icon.Text,
                Editable = false,
                Resizable = false,
                DataCell = new ImageViewCell { Binding = Binding.Property<ComponentData, Image>(r => r.Icon) }
            });

            gridView.Columns.Add(new GridColumn
            {
                Sortable = true,
                HeaderText = ColumnHeaders.Name.Text,
                Editable = false,
                DataCell = new TextBoxCell { Binding = Binding.Property<ComponentData, string>(r => r.Name) }
            });

            gridView.Columns.Add(new GridColumn
            {
                Sortable = true,
                HeaderText = ColumnHeaders.NickName.Text,
                Editable = false,
                DataCell = new TextBoxCell { Binding = Binding.Property<ComponentData, string>(r => r.NickName) }
            });

            gridView.Columns.Add(new GridColumn
            {
                Sortable = true,
                HeaderText = ColumnHeaders.Groups.Text,
                Editable = false,
                DataCell = new TextBoxCell { Binding = Binding.Property<ComponentData, string>(r => r.Groups) }
            });

            return gridView;
        }

        void OnCandidateGridViewColumnHeaderClicked(object sender, GridColumnEventArgs e) => SortByColumn(e.Column);

        void SortByColumn(GridColumn column)
        {
            switch (column.HeaderText)
            {
                case var h when h == ColumnHeaders.Name.Text:
                    _candidateRows.Sort = (r1, r2) => String.Compare(r1.Name, r2.Name);
                break;

                case var h when h == ColumnHeaders.NickName.Text:
                    _candidateRows.Sort = (r1, r2) => String.Compare(r1.NickName, r2.NickName);
                break;
                
                case var h when h == ColumnHeaders.Groups.Text:
                    _candidateRows.Sort = (r1, r2) => String.Compare(r1.Groups, r2.Groups);
                break;
            }
        }

        void OnSelectedItemsChanged(object sender, EventArgs e)
        {
            if (sender is GridView gridView)
            {
                if (!gridView.SelectedItems.Any())
                {
                    CurrentView();
                    return;
                }

                var allBounds = gridView.SelectedItems.OfType<ComponentData>()
                    .Select(data => data.Bounds);

                CandidateView(allBounds);
            }
        }

        void OnCandidateGridViewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Keys.Escape:
                if (sender is GridView gridView)
                {
                    gridView.UnselectAll();
                }
                e.Handled = true;
                break;

                case Keys.Left:
                AddOutputParamData();
                e.Handled = true;
                break;

                case Keys.Right:
                AddInputParamData();
                e.Handled = true;
                break;
            }

        }

        void CurrentView()
        {
            var canvas = Instances.ActiveCanvas;
            if (canvas != null)
            {
                _ghNamedView.SetToViewport(canvas, _namedViewLength);
            }
        }

        void CandidateView(IEnumerable<RectangleF> rectangleFs)
        {
            var allBounds = rectangleFs
                    .Aggregate(RectangleF.Union);

            var canvas = Instances.ActiveCanvas;
            if (canvas != null)
            {
                var view = CreateNamedView(allBounds);
                view.SetToViewport(canvas, _namedViewLength);
            }

        }

        GH_NamedView CreateNamedView(RectangleF bound)
        {
            var canvas = Instances.ActiveCanvas;
            if (canvas != null)
            {
                var view = new GH_NamedView
                {
                    Point = new PointF(
                            bound.X + bound.Width * 0.5f,
                            bound.Y + bound.Height * 0.5f
                        ).ToSD(),
                    Type = GH_NamedViewType.center,
                    Zoom = new[] {
                        _ghNamedView.Zoom,
                        Convert.ToSingle(canvas.Viewport.Width / bound.Width),
                        Convert.ToSingle(canvas.Viewport.Height / bound.Height)
                    }.Min()
                };

                return view;
            }

            return null;
        }
        #endregion

        #region Add Buttons for Output and Input
        IEnumerable<Control> CreateAddButtons()
        {
            var addLeftButton = new Button { Text = "Add" };
            addLeftButton.Click += OnAddLeftButtonClicked;
            var addRightButton = new Button { Text = "Add" };
            addRightButton.Click += OnAddRightButtonClicked;

            return new Control[] { null, addLeftButton, null, addRightButton, null };
        }

        void OnAddLeftButtonClicked(object sender, EventArgs e)
        {
            AddOutputParamData();
        }

        void OnAddRightButtonClicked(object sender, EventArgs e)
        {
            AddInputParamData();
        }

        void AddOutputParamData()
        {
            var selectedItems = (!_candidateGridView.SelectedItems.Any() && _ghDocument != null)
                ? _ghDocument.SelectedObjects().Select(obj => new ComponentData(obj))
                : _candidateGridView.SelectedItems.OfType<ComponentData>();

            foreach (var item in selectedItems)
            {
                var guids = _outputRows.Select(r => r.InstanceGuid);

                if (item.GhObject is IGH_Component component)
                {
                    component.Params.Output
                        .Where(param => !guids.Contains(param.InstanceGuid)).ToList()
                        .ForEach(param => { _outputRows.Add(new ComponentData(param)); });
                }
                else if (item.GhObject is IGH_Param param)
                {
                    if (!guids.Contains(param.InstanceGuid))
                    {
                        _outputRows.Add(new ComponentData(param));
                    }
                }
            }
        }

        void AddInputParamData()
        {
            var selectedItems = (!_candidateGridView.SelectedItems.Any() && _ghDocument != null)
                ? _ghDocument.SelectedObjects().Select(obj => new ComponentData(obj))
                : _candidateGridView.SelectedItems.OfType<ComponentData>();

            var guids = _inputRows.Select(r => r.InstanceGuid);

            foreach (var item in selectedItems)
            {
                if (item.IsComponent)
                {
                    item.Params.Input
                        .Where(param => !guids.Contains(param.InstanceGuid)).ToList()
                        .ForEach(param => { _inputRows.Add(new ComponentData(param)); });
                }
                else if (item.IsParam)
                {
                    if (!guids.Contains(item.InstanceGuid))
                    {
                        _inputRows.Add(item);
                    }
                }
            }
        }
        #endregion

        #region Left and Right GridViews
        GridView CreateLeftGridView()
        {
            var gridView = new GridView
            {
                ShowHeader = true,
                AllowMultipleSelection = true,
                DataStore = _outputRows,
                ContextMenu = CreateContextMenu()
            };

            gridView.ColumnHeaderClick += OnLeftGridViewColumnHeaderClicked;
            gridView.KeyDown += OnKeyDownLeftGridView;

            gridView.Columns.Add(new GridColumn
            {
                HeaderText = ColumnHeaders.Icon.Text,
                Editable = false,
                Resizable = false,
                DataCell = new ImageViewCell { Binding = Binding.Property<ComponentData, Image>(r => r.Icon) }
            });

            gridView.Columns.Add(new GridColumn
            {
                Sortable = true,
                HeaderText = ColumnHeaders.Name.Text,
                Editable = false,
                DataCell = new TextBoxCell { Binding = Binding.Property<ComponentData, string>(r => r.Name) }
            });

            gridView.Columns.Add(new GridColumn
            {
                Sortable = true,
                HeaderText = ColumnHeaders.NickName.Text,
                Editable = true,
                DataCell = new TextBoxCell { Binding = Binding.Property<ComponentData, string>(r => r.NickName) }
            });

            gridView.Columns.Add(new GridColumn
            {
                Sortable = true,
                HeaderText = ColumnHeaders.Groups.Text,
                Editable = false,
                DataCell = new TextBoxCell { Binding = Binding.Property<ComponentData, string>(r => r.Groups) }
            });

            return gridView;
        }

        private void OnLeftGridViewColumnHeaderClicked(object sender, GridColumnEventArgs e)
        {
            throw new NotImplementedException();
        }

        void OnKeyDownLeftGridView(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Keys.Delete:
                {
                    if (sender is GridView gridView)
                    {
                        foreach (ComponentData item in gridView.SelectedItems.ToArray()) { _outputRows.Remove(item); }
                    }
                    e.Handled = true;
                    break;
                }

                case Keys.Escape:
                {
                    if (sender is GridView gridView)
                    {
                        gridView.UnselectAll();
                    }
                    e.Handled = true;
                    break;
                }
            }
        }

        GridView CreateRightGridView()
        {
            var gridView = new GridView
            {
                ShowHeader = true,
                AllowMultipleSelection = true,
                DataStore = _inputRows,
                ContextMenu = CreateContextMenu()
            };

            gridView.ColumnHeaderClick += OnRightGridViewColumnHeaderClicked;
            gridView.KeyDown += OnKeyDownRightGridView;

            gridView.Columns.Add(new GridColumn
            {
                HeaderText = ColumnHeaders.Icon.Text,
                Editable = false,
                Resizable = false,
                DataCell = new ImageViewCell { Binding = Binding.Property<ComponentData, Image>(r => r.Icon) }
            });

            gridView.Columns.Add(new GridColumn
            {
                Sortable = true,
                HeaderText = ColumnHeaders.Name.Text,
                Editable = false,
                DataCell = new TextBoxCell { Binding = Binding.Property<ComponentData, string>(r => r.Name) }
            });

            gridView.Columns.Add(new GridColumn
            {
                Sortable = true,
                HeaderText = ColumnHeaders.NickName.Text,
                Editable = true,
                DataCell = new TextBoxCell { Binding = Binding.Property<ComponentData, string>(r => r.NickName) }
            });

            gridView.Columns.Add(new GridColumn
            {
                Sortable = true,
                HeaderText = ColumnHeaders.Groups.Text,
                Editable = false,
                DataCell = new TextBoxCell { Binding = Binding.Property<ComponentData, string>(r => r.Groups) }
            });

            gridView.Columns.Add(new GridColumn
            {
                HeaderText = ColumnHeaders.DrawIcon.Text,
                Editable = true,
                DataCell = new ComboBoxCell
                {
                    DataStore = ComponentData.DrawIconOptions,
                    Binding = Binding.Property<ComponentData, object>(r => r.DrawIcon)
                }
            });

            gridView.Columns.Add(new GridColumn
            {
                HeaderText = ColumnHeaders.WireDisplay.Text,
                Editable = true,
                DataCell = new ComboBoxCell
                {
                    DataStore = ComponentData.WireDisplayOptions,
                    Binding = Binding.Property<ComponentData, object>(r => r.WireDisplay)
                }
            });

            return gridView;
        }

        private void OnRightGridViewColumnHeaderClicked(object sender, GridColumnEventArgs e)
        {
            throw new NotImplementedException();
        }

        void OnKeyDownRightGridView(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Keys.Delete:
                {
                    if (sender is GridView gridView)
                    {
                        foreach (ComponentData item in gridView.SelectedItems.ToArray()) { _inputRows.Remove(item); }
                    }
                    e.Handled = true;
                    break;
                }

                case Keys.Escape:
                {
                    if (sender is GridView gridView)
                    {
                        gridView.UnselectAll();
                    }
                    e.Handled = true;
                    break;
                }
            }
        }
        #endregion

        #region Connect Button
        IEnumerable<Control> CreateButtons()
        {
            // TODO: Change to Connect Button
            //var removeButton = new Button { Text = "Create" };
            //removeButton.Click += OnRemoveButtonClicked;

            var connectButton = new Button { Text = "Connect" };
            connectButton.Click += OnConnectButtonClicked;

            return new Control[] { null, connectButton, null };
        }
        #endregion

        #region Create Context Menu
        ContextMenu CreateContextMenu()
        {
            var drawIconItem = new ButtonMenuItem { Text = "Draw Icon" };
            drawIconItem.Click += OnDrawIconMenuItemClicked;

            var wireDisplayItem = new ButtonMenuItem { Text = "Wire Display" };

            var contextMenu = new ContextMenu();
            contextMenu.Items.AddRange(new MenuItem[]{
                drawIconItem,
                new SeparatorMenuItem(),
                wireDisplayItem
            });

            return contextMenu;
        }

        void OnDrawIconMenuItemClicked(object sender, EventArgs e)
        {
            throw new NotImplementedException();

            //if (sender is not ButtonMenuItem menuItem)
            //{
            //    return;
            //}
            //foreach (var row in outputGridView.SelectedItems.Cast<ComponentData>())
            //{
            //    var _ = row.DrawIcon;
            //}
        }

        void OnConnectButtonClicked(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }
        #endregion

        // UNDONE: Button Events
        void OnUpdateButtonClicked(object sender, EventArgs e) => RefreshComponentData();

        void OnCreateButtonClicked(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        #region Overrides
        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            RefreshComponentData();
        }
        #endregion
    }

    static class ConnectWire
    {
        internal static void ShowForm()
        {
            var doc = Instances.ActiveCanvas?.Document;
            if (doc == null)
            {
                MessageBox.Show(
                    "No active document found. Please open or create a new document to proceed.",
                    "Wire Tools",
                    MessageBoxButtons.OK,
                    MessageBoxType.Warning);

                return;
            }

            var form = new ConnectWireForm();
            form.Show();
        }
    }
}
