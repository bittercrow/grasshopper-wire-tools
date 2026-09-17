using Eto.Forms;
using Eto.Drawing;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Rhino.Geometry;
using Rhino.UI;
using System;
using System.Collections.Generic;
using System.Linq;


namespace WireTools
{
    record ComponentData(IGH_DocumentObject GhObject)
    {
        internal string Name => GhObject.Name;
        
        internal string NickName
        {
            get => GhObject.NickName;
            set
            {
                if (GhObject.NickName != value)
                {
                    GhObject.NickName = value;
                    CanvasRedraw.Redraw();
                }
            }
        }
        
        internal string Groups => GhGroupData.CreateGhGroupData(GhObject);
        
        internal Image Icon => GhObject.Icon_24x24.ToEto().WithSize(15, 15);
        
        internal Guid InstanceGuid => GhObject.InstanceGuid;
        
        internal bool IsComponent => GhObject is IGH_Component;
        
        internal bool IsParam => GhObject is IGH_Param;
        
        internal GH_ComponentParamServer Params =>
            GhObject is IGH_Component comp ? comp.Params : null;
        
        internal RectangleF Bounds => new RectangleF(
                         GhObject.Attributes.Bounds.X,
                         GhObject.Attributes.Bounds.Y,
                         GhObject.Attributes.Bounds.Width,
                         GhObject.Attributes.Bounds.Height);
        internal string DrawIcon
        {
            get => DrawIconOptions[(int)GhObject.IconDisplayMode];
            set
            {
                GhObject.IconDisplayMode = (GH_IconDisplayMode)Array.IndexOf(DrawIconOptions, value);
                CanvasRedraw.Redraw();
            }
        }

        internal string WireDisplay
        {
            get => WireDisplayOptions[GhObject is IGH_Param param ? (int)param.WireDisplay : 0];
            set
            {
                if (GhObject is IGH_Param param)
                {
                    param.WireDisplay = (GH_ParamWireDisplay)Array.IndexOf(WireDisplayOptions, value);
                    CanvasRedraw.Redraw();
                }
            }
        }

        internal static readonly string[] DrawIconOptions = { "Icon", "UseAppSetting", "Text" };

        internal static readonly string[] WireDisplayOptions = { "Default", "Faint", "Hidden" };

    }

    static class GhGroupData
    {
        internal static string CreateGhGroupData(IGH_DocumentObject obj)
        {
            var ghDoc = Instances.ActiveCanvas?.Document;

            if (ghDoc == null)
            {
                throw new ArgumentException("No grasshopper document");
            }

            var guid = obj.Attributes.IsTopLevel
                                ? obj.InstanceGuid
                                : obj.Attributes.GetTopLevel.InstanceGuid;

            return string.Join("-", ghDoc.Objects
                .OfType<GH_Group>()
                .Where(g => g.ObjectIDs.Contains(guid))
                .Select(h => h.NickName));

        }
    }

    static class CanvasRedraw
    {
        internal static void Redraw()
        {
            var canvas = Instances.ActiveCanvas;
            canvas.Invalidate();
        }
    }

}