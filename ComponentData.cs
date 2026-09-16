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
    abstract record BaseGhData(IGH_DocumentObject _ghObject)
    {
        private IGH_DocumentObject _ghObject { get; init; }
        public string Name => _ghObject.Name;
        public string NickName => _ghObject.NickName;
        public string Groups => GhGroupData.CreateGhGroupData(_ghObject);
        public Image Icon => _ghObject.Icon_24x24.ToEto().WithSize(15, 15);
        public Guid InstanceGuid => _ghObject.InstanceGuid;
        public bool IsComponent => _ghObject is IGH_Component;
        public bool IsParam => _ghObject is IGH_Param;
        public GH_ComponentParamServer Params =>
            _ghObject is IGH_Component comp ? comp.Params : null;

    }

    record ComponentData(IGH_DocumentObject _ghObject) : BaseGhData(_ghObject)
    {
        public RectangleF Bounds => new RectangleF(
                         _ghObject.Attributes.Bounds.X,
                         _ghObject.Attributes.Bounds.Y,
                         _ghObject.Attributes.Bounds.Width,
                         _ghObject.Attributes.Bounds.Height);
    }

    record OutputParamData(IGH_Param _ghParam) : BaseGhData(_ghParam)
    {
        public new string NickName
        {
            get => _ghParam.NickName;
            set
            {
                if (_ghParam.NickName != value)
                {
                    _ghParam.NickName = value;
                    CanvasRedraw.Redraw();
                }
            }
        }
    }

    record InputParamData(IGH_Param _ghParam) : BaseGhData(_ghParam)
    {
        public new string NickName
        {
            get => _ghParam.NickName;
            set
            {
                if (_ghParam.NickName != value)
                {
                    _ghParam.NickName = value;
                    CanvasRedraw.Redraw();
                }
            }
        }

        public string DrawIcon
        {
            get => DrawIconOptions[(int)_ghParam.IconDisplayMode];
            set
            {
                _ghParam.IconDisplayMode = (GH_IconDisplayMode)Array.IndexOf(DrawIconOptions, value);
                CanvasRedraw.Redraw();
            }
        }

        public string WireDisplay
        {
            get => WireDisplayOptions[(int)_ghParam.WireDisplay];
            set
            {
                _ghParam.WireDisplay = (GH_ParamWireDisplay)Array.IndexOf(WireDisplayOptions, value);
                CanvasRedraw.Redraw();
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