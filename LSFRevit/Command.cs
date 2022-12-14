using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;

namespace LSFRevit
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //Get UIDocument
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            //Get Document
            Document doc = uidoc.Document;

            List<FamilySymbol> lsfProfileType = new FilteredElementCollector(doc)
              .WhereElementIsElementType()
              .OfClass(typeof(FamilySymbol))
              .OfCategory(BuiltInCategory.OST_StructuralFraming)
              .Cast<FamilySymbol>()
              //.Where(a => a.FamilyName.Contains("M_Light"))
              //.Where(a => a.Name.Contains("C140"))
              .ToList();

            //Get all walls of Architecture
            List<Element> walls = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_Walls)
                .ToList();

            List<Element> windows = new FilteredElementCollector(doc)
           .WhereElementIsNotElementType()
           .OfCategory(BuiltInCategory.OST_Windows)
           .ToList();

            List<Element> doors = new FilteredElementCollector(doc)
              .WhereElementIsNotElementType()
              .OfCategory(BuiltInCategory.OST_Doors)
              .ToList();


            Form1 windowForm = new Form1(lsfProfileType);
            windowForm.ShowDialog();

            FamilySymbol exteriorProfile = windowForm.ExteriorFrame;
            FamilySymbol interiorProfile = windowForm.InteriorFrame;
            int spacing = windowForm.SpacingFrame;

            List<LSFPanel> lSFPanels = new List<LSFPanel>();
            List<FamilyInstance> allFrames = new List<FamilyInstance>();


            using (Transaction trans = new Transaction(doc, "Generate Frames"))
            {
                trans.Start();
                FailureHandler.FailureHandlerTrans(trans);
                foreach (var wall in walls)
                {
                    LSFPanel lSFPanel = new LSFPanel(wall as Wall, doc, exteriorProfile, interiorProfile,spacing);
                    lSFPanel.FrameInstances.AddRange(lSFPanel.GenerateSteelFramesByLines(lSFPanel.ExternalWallVerticalCurves, lSFPanel.ExternalFrameType));
                    lSFPanel.FrameInstances.AddRange(lSFPanel.GenerateSteelFramesByLines(lSFPanel.ExternalWallHorizontalCurves, lSFPanel.ExternalFrameType));
                    lSFPanel.FrameInstances.AddRange(lSFPanel.GenerateSteelFramesByLines(lSFPanel.VerticalCurves, lSFPanel.ExternalFrameType));
                    lSFPanel.FrameInstances.AddRange(lSFPanel.GenerateSteelFramesByLines(lSFPanel.OpeningCurves, lSFPanel.InternalFrameType));
                    lSFPanel.FrameInstances.AddRange(lSFPanel.GenerateSteelFramesByLines(lSFPanel.HorizontalCurves, lSFPanel.InternalFrameType));
                    lSFPanel.FrameInstances.AddRange(lSFPanel.GenerateSteelFramesByLines(lSFPanel.DiagonalCurves, lSFPanel.InternalFrameType));

                    lSFPanels.Add(lSFPanel);

                }
                trans.Commit();
            }
            {
                Transaction transParam = new Transaction(doc, "Parameters Frames");
                transParam.Start();
                foreach (var panel in lSFPanels)
                {
                    panel.FillParameters();
                    allFrames.AddRange(panel.FrameInstances);
                }
                transParam.Commit();

                var groupedFrames = allFrames.OrderBy(a=>a.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH).AsDouble())
                    .GroupBy(a => a.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH).AsDouble());
                int i = 1;

                Transaction transRot = new Transaction(doc, "Rotation Frames");
                transRot.Start();
                foreach (var group in groupedFrames)
                {
                    foreach (var frame in group)
                    {
                        frame.LookupParameter("FrameNumber").Set($"F{i}");
                        Curve curve = (frame.Location as LocationCurve).Curve;
                        double pt1Z = curve.GetEndPoint(0).Z;
                        double pt2Z = curve.GetEndPoint(1).Z;

                        if (Math.Abs(pt1Z-pt2Z)<1)
                        {
                            if (pt1Z>1 || pt2Z>1)
                            {
                                LSFPanel.RotateCrossSection(frame, -90 * Math.PI / 180);
                            }
                            else
                            {
                                LSFPanel.RotateCrossSection(frame, 90 * Math.PI / 180);
                            }
                        }
                        else
                        {
                            string id = frame.LookupParameter("HostWall").AsString();
                            ElementId wallId = new ElementId(Convert.ToInt32(id));
                            Wall wall = (doc.GetElement(wallId) as Wall);
                            XYZ orientation = wall.Orientation;
                            double rotation = orientation.AngleTo(XYZ.BasisX);
                            LSFPanel.RotateCrossSection(frame, rotation);
                        }

                    }
                    i++;
                }

                View3D view = new FilteredElementCollector(doc)
               .WhereElementIsNotElementType()
               .OfClass(typeof(View3D))
               .Cast<View3D>()
               .Where(a => a.Name.Contains("LSF"))
               .FirstOrDefault();

                List<string> panelNames = allFrames.GroupBy(a => a.LookupParameter("PanelName").AsString())
                    .Select(a=>a.FirstOrDefault())
                    .Select(a => a.LookupParameter("PanelName").AsString())
                    .ToList();
                List<BuiltInCategory> list = new List<BuiltInCategory>();
                list.Add(BuiltInCategory.OST_StructuralFraming);

                int interator = 0;
                ElementId elementId = LSFPanel.GetParamId(doc, "PanelName");
                List<Color> colors = new List<Color>();
                colors.Add(new Color(255, 0, 0));
                colors.Add(new Color(255, 128, 0));
                colors.Add(new Color(255, 255, 0));
                colors.Add(new Color(128, 255, 0));
                colors.Add(new Color(128, 255, 0));
                colors.Add(new Color(0, 255, 128));
                colors.Add(new Color(0, 255, 255));
                colors.Add(new Color(0, 128, 255));
                colors.Add(new Color(0, 0, 255));
                colors.Add(new Color(127, 0, 255));
                colors.Add(new Color(127, 0, 255));
                colors.Add(new Color(255, 0, 255));
                colors.Add(new Color(255, 0, 127));
                colors.Add(new Color(128, 128, 128));
                colors.Add(new Color(255, 20, 20));
                colors.Add(new Color(255, 128, 20));
                colors.Add(new Color(255, 255, 20));
                colors.Add(new Color(128, 255, 20));
                colors.Add(new Color(128, 255, 20));
                colors.Add(new Color(20, 255, 128));
                colors.Add(new Color(20, 255, 255));
                colors.Add(new Color(20, 128, 255));
                colors.Add(new Color(20, 20, 255));
                colors.Add(new Color(127, 20, 255));
                colors.Add(new Color(127, 20, 255));
                colors.Add(new Color(255, 20, 255));
                colors.Add(new Color(255, 20, 127));
                colors.Add(new Color(128, 128, 128));

                foreach (var name in panelNames)
                {
                    LSFPanel.CreateAndAddFilterThatDoesNotContainTypeNameToView(view, list, name, true, elementId, colors[interator]);
                    interator++;
                }

                transRot.Commit();
                TaskDialog.Show("Data", $"Total of panels:{lSFPanels.Count}\n Total of frames generated: {allFrames.Count}");
            }
            return Result.Succeeded;
        }
    }
}