using Grasshopper;
using Grasshopper.Documentation;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Rhino;
using Rhino.DocObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;


namespace WireTools
{
    public class WireToolsInfo : GH_AssemblyInfo
    {
        public override string Name => "WireTools";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "This plugin adds Wire Tools to Grasshopper.";

        public override Guid Id => new Guid("f0fa6d9a-5540-4b22-8a3a-ef8b04338108");

        //Return a string identifying you or your company.
        public override string AuthorName => "TeruS";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "https://github.com/bittercrow/grasshopper-wire-tools.git";

        //Return a string representing the version.  This returns the same version as the assembly.
        public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
    }

    public class AddCommandsPriority : GH_AssemblyPriority
    {
        private ToolStripMenuItem _menuItem = new ToolStripMenuItem
                                                    (
                                                       "WireTools",
                                                       null,
                                                       OnClick
                                                    );

        public override GH_LoadingInstruction PriorityLoad()
        {
            try
            {
                Instances.CanvasCreated += OnCanvasCreated;
            }
            catch { return GH_LoadingInstruction.Abort; }

            return GH_LoadingInstruction.Proceed;
        }

        private void OnCanvasCreated(GH_Canvas canvas)
        {
            Instances.CanvasCreated -= OnCanvasCreated; // Only once

            // Set a button in canvas toolbar  
            AddToolbarButton();

            // Set menu in menu bar
            var documentEditor = Instances.DocumentEditor;
            if (documentEditor != null)
            {
                AddMenuItem(documentEditor);
            }

            // Set Shortcuts event
            RegisterShortcut();

        }

        private void AddToolbarButton()
        {
            Type editorType = typeof(GH_DocumentEditor);
            BindingFlags binding = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField;

            FieldInfo field = editorType.GetField("_CanvasToolbar", binding);

            object fieldInstance = field.GetValue(Instances.DocumentEditor);
            ToolStrip toolstrip = fieldInstance as ToolStrip;
            //var toolstrip = Grasshopper.Instances.DocumentEditor.Controls[0].Controls[1] as ToolStrip;
            if (toolstrip == null)
                return;

            toolstrip.Items.Add("WT", null, OnClick);
        }

        private static void OnClick(object sender, EventArgs e)
        {
            ConnectWire.ShowForm();
        }

        private void AddMenuItem(GH_DocumentEditor editor)
        {
            var menuItem = new ToolStripMenuItem("WT");
            menuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                _menuItem,
                new ToolStripSeparator(),
            });

            var menuStrip = editor.MainMenuStrip;
            menuStrip.SuspendLayout();
            menuStrip.Items.AddRange(new ToolStripItem[] { menuItem });
            menuStrip.ResumeLayout(false);
            menuStrip.PerformLayout();
        }

        private void RegisterShortcut()
        {
            var shortcuts = new List<ToolStripMenuItem>();
            shortcuts.Add(_menuItem);

            GH_DocumentEditor.AggregateShortcutMenuItems += (sender, e) =>
            {
                shortcuts.ForEach(e.AppendItem);
            };
        }

        public GH_DocumentEditor.AggregateShortcutMenuItemsEventHandler handleThis { get; set; }
    }
}