using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Autodesk.Revit.DB.Structure;

namespace LSFRevit
{
    public class LSFPanel
    {
        public Document WallDocument { get; private set; }
        public ElementId HostWall { get; set; }
        public Level FrameLevel { get; set; }
        public bool HasConnections { get; set; }
        public bool HasOpenings { get; set; }
        public List<Curve> ExternalWallVerticalCurves { get; set; }
        public List<Curve> ExternalWallHorizontalCurves { get; set; }
        public List<Curve> OpeningCurves { get; set; }
        public List<Curve> VerticalCurves { get; set; }
        public List<Curve> HorizontalCurves { get; set; }
        public List<Curve> DiagonalCurves { get; set; }
        public double InternalFramingSpacing { get; set; }
        public List<Curve> InternalFramings { get; set; }
        public string PanelName { get; set; }
        public FamilySymbol ExternalFrameType { get; set; }
        public FamilySymbol InternalFrameType { get; set; }

        public List<FamilyInstance> FrameInstances { get; set; }
        public double WallLength { get; set; }
        public LSFPanel(Wall wall, Document doc, FamilySymbol external, FamilySymbol interior, double spacing)
        {
            WallDocument = doc;
            HostWall = wall.Id;
            FrameLevel = doc.GetElement(wall.LevelId) as Level;
            ExternalWallVerticalCurves = new List<Curve>();
            ExternalWallHorizontalCurves = new List<Curve>();
            GetWallBoundaries(wall);
            OpeningCurves = GetOpenings(doc, wall);
            HasOpenings = OpeningCurves.Count > 0;
            HasConnections = ExporterIFCUtils.GetConnectedWalls(wall, IFCConnectedWallDataLocation.End).Count() > 0;
            VerticalCurves = new List<Curve>();
            HorizontalCurves = new List<Curve>();
            DiagonalCurves = new List<Curve>();
            FrameInstances = new List<FamilyInstance>();

            WallLength = Math.Round((wall.Location as LocationCurve).Curve.Length);

            ExternalFrameType = external;
            InternalFrameType = interior;

            InternalFramingSpacing = spacing;

            if (!HasOpenings)
            {
                GetInternalCurves(wall);
                GetHorizontalAndDiagonalFrames(wall);
            }

        }
        public void GetInternalCurves(Wall wall)
        {
            Curve wallCurve = (wall.Location as LocationCurve).Curve;
            double wallZ = wall.get_BoundingBox(null).Max.Z;
            double totalInternal = Math.Ceiling(wallCurve.Length / CmToFeet(InternalFramingSpacing));
            List<Curve> curves = new List<Curve>();
            for (int i = 1; i < totalInternal; i++)
            {
                XYZ basePt = wallCurve.Evaluate(i / totalInternal, true);
                XYZ topPt = basePt + new XYZ(0, 0, wallZ - CmToFeet(5));
                Line line = Line.CreateBound(topPt, basePt);
                curves.Add(line);
            }

            VerticalCurves = curves;
        }
        public void GetWallBoundaries(Wall wall)
        {
            List<Curve> externalShape = new List<Curve>();

            Curve locationCurve = (wall.Location as LocationCurve).Curve;

            //if (locationCurve.GetEndPoint(0).Y == locationCurve.GetEndPoint(1).Y)
            {
                double x0 = locationCurve.Evaluate(0, true).X;
                double y0 = locationCurve.Evaluate(0, true).Y;
                double z0 = locationCurve.GetEndPoint(0).Z;
                double x1 = locationCurve.Evaluate(1, true).X;
                double y1 = locationCurve.Evaluate(1, true).Y;

                double maxZ = wall.get_BoundingBox(null).Max.Z;
                Curve v1 = Line.CreateBound(new XYZ(x1, y1, z0), new XYZ(x1, y1, maxZ));
                Curve h1 = Line.CreateBound(new XYZ(x0, y0, maxZ), new XYZ(x1, y1, maxZ));
                Curve v2 = Line.CreateBound(new XYZ(x0, y0, maxZ), new XYZ(x0, y0, 0));

                ExternalWallHorizontalCurves.Add(locationCurve);
                ExternalWallVerticalCurves.Add(v1);
                ExternalWallHorizontalCurves.Add(h1);
                ExternalWallVerticalCurves.Add(v2);
            }
            //else
            //{
            //    double x0 = locationCurve.Evaluate(0.02, true).X;
            //    double y0 = locationCurve.Evaluate(0.02, true).Y;
            //    double z0 = locationCurve.GetEndPoint(0).Z;
            //    double x1 = locationCurve.Evaluate(0.98, true).X;
            //    double y1 = locationCurve.Evaluate(0.98, true).Y;

            //    double maxZ = wall.get_BoundingBox(null).Max.Z;
            //    Curve v1 = Line.CreateBound(new XYZ(x1, y1, z0), new XYZ(x1, y1, maxZ));
            //    Curve h1 = Line.CreateBound(new XYZ(x0, y0, maxZ), new XYZ(x1, y1, maxZ));
            //    Curve v2 = Line.CreateBound(new XYZ(x0, y0, 0), new XYZ(x0, y0, maxZ));

            //    ExternalWallHorizontalCurves.Add(locationCurve);
            //    ExternalWallVerticalCurves.Add(v1);
            //    ExternalWallHorizontalCurves.Add(h1);
            //    ExternalWallVerticalCurves.Add(v2);
            //}


        }
        public List<Curve> GetOpenings(Document doc, Wall wall)
        {
            List<Element> windows = new FilteredElementCollector(doc)
           .WhereElementIsNotElementType()
           .OfCategory(BuiltInCategory.OST_Windows)
           .ToList();

            List<Element> doors = new FilteredElementCollector(doc)
              .WhereElementIsNotElementType()
              .OfCategory(BuiltInCategory.OST_Doors)
              .ToList();

            XYZ cutDir = null;
            List<Curve> openingsCurves = new List<Curve>();
            foreach (var window in windows)
            {
                if ((window as FamilyInstance).Host.Id == HostWall)
                {
                    Curve wallCurve = (wall.Location as LocationCurve).Curve;
                    double wallZ = wall.get_BoundingBox(null).Max.Z;

                    CurveLoop cLoop = ExporterIFCUtils.GetInstanceCutoutFromWall(doc, wall, window as FamilyInstance, out cutDir);
                    double width = cLoop.GetRectangularWidth(cLoop.GetPlane());
                    double height = cLoop.GetRectangularHeight(cLoop.GetPlane());
                    XYZ lowerPoint = cLoop.OrderBy(a => a.GetEndPoint(0).Z).ThenBy(a => a.GetEndPoint(0).X).ThenBy(a => a.GetEndPoint(0).Y).Select(a => a.GetEndPoint(0)).First();
                    XYZ secondPoint = cLoop.OrderBy(a => a.GetEndPoint(0).Z).ThenBy(a => a.GetEndPoint(0).X).ThenBy(a => a.GetEndPoint(0).Y).Select(a => a.GetEndPoint(0)).Skip(1).First();


                    XYZ lowerPoint0 = new XYZ(lowerPoint.X, lowerPoint.Y, 0);
                    XYZ secondPoint0 = new XYZ(secondPoint.X, secondPoint.Y, 0);

                    XYZ intersectPt = wallCurve.Project(lowerPoint0).XYZPoint;
                    XYZ intersectSecondPt = wallCurve.Project(secondPoint0).XYZPoint;

                    XYZ topPoint = new XYZ(intersectPt.X, intersectPt.Y, wallZ - CmToFeet(5));
                    XYZ topSecondPoint = new XYZ(intersectSecondPt.X, intersectSecondPt.Y, wallZ - CmToFeet(5));

                    Line firstLine = Line.CreateBound(topPoint, intersectPt);
                    Line secondLine = Line.CreateBound(topSecondPoint, intersectSecondPt);

                    XYZ firstPt = new XYZ(intersectPt.X, intersectPt.Y, lowerPoint.Z - CmToFeet(5));
                    XYZ secondPt = new XYZ(intersectSecondPt.X, intersectSecondPt.Y, secondPoint.Z - CmToFeet(5));
                    Line thrdLine = Line.CreateBound(firstPt, secondPt);

                    XYZ firstPtTop = new XYZ(intersectPt.X, intersectPt.Y, lowerPoint.Z + height + CmToFeet(5));
                    XYZ secondPtTop = new XYZ(intersectSecondPt.X, intersectSecondPt.Y, secondPoint.Z + height + CmToFeet(5));
                    Line fourthLine = Line.CreateBound(firstPtTop, secondPtTop);

                    XYZ topMiddle = fourthLine.Evaluate(0.5, true);
                    Line middleLineTop = Line.CreateBound(topMiddle, topMiddle + new XYZ(0, 0, wallZ - topMiddle.Z - CmToFeet(5)));

                    XYZ bottomMiddle = thrdLine.Evaluate(0.5, true);
                    Line middleLineBottom = Line.CreateBound(bottomMiddle - new XYZ(0, 0, CmToFeet(5)), bottomMiddle - new XYZ(0, 0, bottomMiddle.Z - CmToFeet(5)));

                    openingsCurves.Add(firstLine);
                    openingsCurves.Add(secondLine);
                    openingsCurves.Add(thrdLine);
                    openingsCurves.Add(fourthLine);
                    openingsCurves.Add(middleLineTop);
                    openingsCurves.Add(middleLineBottom);
                }
            }
            foreach (var door in doors)
            {
                if ((door as FamilyInstance).Host.Id == HostWall)
                {
                    Curve wallCurve = (wall.Location as LocationCurve).Curve;
                    double wallZ = wall.get_BoundingBox(null).Max.Z;

                    CurveLoop cLoop = ExporterIFCUtils.GetInstanceCutoutFromWall(doc, wall, door as FamilyInstance, out cutDir);
                    double width = cLoop.GetRectangularWidth(cLoop.GetPlane());
                    double height = cLoop.GetRectangularHeight(cLoop.GetPlane());
                    XYZ lowerPoint = cLoop.OrderBy(a => a.GetEndPoint(0).Z).ThenBy(a => a.GetEndPoint(0).X).ThenBy(a => a.GetEndPoint(0).Y).Select(a => a.GetEndPoint(0)).First();
                    XYZ secondPoint = cLoop.OrderBy(a => a.GetEndPoint(0).Z).ThenBy(a => a.GetEndPoint(0).X).ThenBy(a => a.GetEndPoint(0).Y).Select(a => a.GetEndPoint(0)).Skip(1).First();

                    Curve higherCurve = cLoop.OrderBy(a => a.GetEndPoint(0).Z).ThenBy(a => a.GetEndPoint(0).X).ThenBy(a => a.GetEndPoint(0).Y).Last();
                    XYZ higherPoint = higherCurve.Evaluate(0.5, true);

                    XYZ lowerPoint0 = new XYZ(lowerPoint.X, lowerPoint.Y, 0);
                    XYZ secondPoint0 = new XYZ(secondPoint.X, secondPoint.Y, 0);

                    XYZ intersectPt = wallCurve.Project(lowerPoint0).XYZPoint;
                    XYZ intersectSecondPt = wallCurve.Project(secondPoint0).XYZPoint;

                    XYZ topPoint = new XYZ(intersectPt.X, intersectPt.Y, wallZ - CmToFeet(5));
                    XYZ topSecondPoint = new XYZ(intersectSecondPt.X, intersectSecondPt.Y, wallZ - CmToFeet(5));

                    Line firstLine = Line.CreateBound(topPoint, intersectPt);
                    Line secondLine = Line.CreateBound(topSecondPoint, intersectSecondPt);
                    Line horizontalLine = Line.CreateBound(firstLine.Project(higherPoint).XYZPoint, secondLine.Project(higherPoint).XYZPoint);
                    XYZ horizontalLineMiddle = horizontalLine.Evaluate(0.5, true);

                    Line middleLineTop = Line.CreateBound(horizontalLineMiddle, horizontalLineMiddle + new XYZ(0, 0, wallZ - horizontalLineMiddle.Z - CmToFeet(5)));

                    openingsCurves.Add(firstLine);
                    openingsCurves.Add(secondLine);
                    openingsCurves.Add(horizontalLine);
                    openingsCurves.Add(middleLineTop);

                }
            }
            return openingsCurves;
        }

        public List<FamilyInstance> GenerateSteelFramesByLines(List<Curve> curves, FamilySymbol frameType)
        {
            List<FamilyInstance> lines = new List<FamilyInstance>();
            frameType.Activate();
            foreach (var edge in curves)
            {
                FamilyInstance profile = WallDocument.Create.NewFamilyInstance(edge, frameType, FrameLevel, StructuralType.Brace);
                StructuralFramingUtils.DisallowJoinAtEnd(profile, 0);
                StructuralFramingUtils.DisallowJoinAtEnd(profile, 1);

                lines.Add(profile);
            }
            return lines;
        }

        public void GetHorizontalAndDiagonalFrames(Wall wall)
        {
            List<Curve> verticalCurves = new List<Curve>();
            verticalCurves.Add(ExternalWallVerticalCurves.Last());
            verticalCurves.AddRange(VerticalCurves);
            verticalCurves.Add(ExternalWallVerticalCurves.First());


            for (int i = 0; i < verticalCurves.Count - 1; i++)
            {
                Line line = Line.CreateBound(verticalCurves[i].Evaluate(0.5, true), verticalCurves[i + 1].Project(verticalCurves[i].Evaluate(0.5, true)).XYZPoint);
                HorizontalCurves.Add(line);
            }
            var resultEnd = ExporterIFCUtils.GetConnectedWalls(wall, IFCConnectedWallDataLocation.End);
            var resultStart = ExporterIFCUtils.GetConnectedWalls(wall, IFCConnectedWallDataLocation.Start);
            if (resultEnd.Count() > 0)
            {
                foreach (var item in resultEnd)
                {
                    Wall wallConnected = WallDocument.GetElement(item.ElementId) as Wall;
                    if (!wallConnected.Orientation.IsAlmostEqualTo(wall.Orientation))
                    {
                        XYZ p1 = verticalCurves.First().Evaluate(0.05, true);
                        XYZ p2 = verticalCurves[1].Evaluate(0.25, true);
                        XYZ p3 = verticalCurves.First().Evaluate(0.5, true);
                        XYZ p4 = verticalCurves[1].Evaluate(0.75, true);
                        XYZ p5 = verticalCurves.First().Evaluate(0.95, true);

                        DiagonalCurves.Add(Line.CreateBound(p1, p2));
                        DiagonalCurves.Add(Line.CreateBound(p2, p3));
                        DiagonalCurves.Add(Line.CreateBound(p3, p4));
                        DiagonalCurves.Add(Line.CreateBound(p4, p5));
                    }
                }
            }
            if (resultStart.Count() > 0)
            {
                int index = verticalCurves.Count - 2;
                foreach (var item in resultStart)
                {
                    Wall wallConnected = WallDocument.GetElement(item.ElementId) as Wall;
                    if (!wallConnected.Orientation.IsAlmostEqualTo(wall.Orientation))
                    {
                        XYZ p1 = verticalCurves.Last().Evaluate(0.95, true);
                        XYZ p2 = verticalCurves[index].Evaluate(0.25, true);
                        XYZ p3 = verticalCurves.Last().Evaluate(0.5, true);
                        XYZ p4 = verticalCurves[index].Evaluate(0.75, true);
                        XYZ p5 = verticalCurves.Last().Evaluate(0.05, true);

                        DiagonalCurves.Add(Line.CreateBound(p1, p2));
                        DiagonalCurves.Add(Line.CreateBound(p2, p3));
                        DiagonalCurves.Add(Line.CreateBound(p3, p4));
                        DiagonalCurves.Add(Line.CreateBound(p4, p5));
                    }
                }
            }
        }
        public void FillParameters()
        {
            foreach (var instance in FrameInstances)
            {
                instance.LookupParameter("HostWall").Set(HostWall.IntegerValue.ToString());
                if (HasOpenings)
                {
                    PanelName = "P." + $"{FrameInstances.Count}_{FeetToCm(WallLength)}_X";
                    instance.LookupParameter("PanelName").Set(PanelName);

                }
                else
                {
                    PanelName = "P." + $"{FrameInstances.Count}_{FeetToCm(WallLength)}";
                    instance.LookupParameter("PanelName").Set(PanelName);
                }
            }

        }
        public double CmToFeet(double cm)
        {
            return cm / 30.48;
        }
        public double FeetToCm(double feet)
        {
            return Math.Round(feet*30.48,0);
        }
        public static void RotateCrossSection(FamilyInstance frame, double degrees)
        {
            {
                frame.get_Parameter(BuiltInParameter.STRUCTURAL_BEND_DIR_ANGLE).Set(degrees);
            }
        }
        public static ElementId GetParamId(Document doc, string paramName)
        {
            var projParams = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfClass(typeof(SharedParameterElement))
                .Cast<SharedParameterElement>()
                .ToList();

            ElementId paramId = null;
            foreach (SharedParameterElement param in projParams)
            {
                if (param.Name.ToUpper().Contains(paramName.ToUpper()))
                {
                    paramId = param.Id;
                }

            }

            return paramId;
        }
        public static void CreateAndAddFilterThatDoesNotContainTypeNameToView(View view, List<BuiltInCategory> builtInCategory, string typeName, bool visibility, ElementId paramId, Color color)
        {
            const string patternNameFilter = "BIMA_";
            List<ParameterFilterElement> filters = new FilteredElementCollector(view.Document)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .Where(a => a.Name.EndsWith(typeName))
                .ToList();
            ParameterFilterElement parameterFilterElement = null;
            if (!view.GetFilters().Select(a => view.Document.GetElement(a)).Where(b => b.Name == typeName).Any())
            {
                if (filters.Any())
                {
                    parameterFilterElement = filters.FirstOrDefault();
                    parameterFilterElement.ClearRules();
                    List<FilterRule> filterRules = new List<FilterRule>();
                    ElementId exteriorParamId = paramId;
                    filterRules.Add(ParameterFilterRuleFactory.CreateNotContainsRule(exteriorParamId, typeName, false));
                    parameterFilterElement.SetElementFilter(new ElementParameterFilter(filterRules));
                }
                else
                {
                    List<ElementId> categories = new List<ElementId>();
                    categories.AddRange(builtInCategory.Select(a => new ElementId(a)).ToList());
                    parameterFilterElement = ParameterFilterElement.Create(view.Document, patternNameFilter + typeName, categories);
                    List<FilterRule> filterRules = new List<FilterRule>();
                    ElementId exteriorParamId = paramId;
                    filterRules.Add(ParameterFilterRuleFactory.CreateEqualsRule(exteriorParamId, typeName, false));
                    parameterFilterElement.SetElementFilter(new ElementParameterFilter(filterRules));
                }

                view.AddFilter(parameterFilterElement.Id);
                view.SetFilterVisibility(parameterFilterElement.Id, visibility);
                OverrideGraphicSettings overrideGraphicSettings = new OverrideGraphicSettings();
                overrideGraphicSettings.SetProjectionLineColor(color);
                view.SetFilterOverrides(parameterFilterElement.Id, overrideGraphicSettings);
            }
        }
    }
}
