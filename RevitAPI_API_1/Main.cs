using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CopyGroup : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            List<Level> listLevel = new List<Level>();
            listLevel = GetLevels(doc);

            Level level1 = listLevel
                .Where(x => x.Name.Equals("Уровень 1"))
                .FirstOrDefault();

            Level level2 = listLevel
                .Where(x => x.Name.Equals("Уровень 2"))
                .FirstOrDefault();

            double width = 10000;
            double depth = 5000;

            double roofHeight = 1500;

            List<Wall> walls = new List<Wall>();

            Transaction transaction = new Transaction(doc, "Построение стен");
            transaction.Start();

            walls = CreateWall(doc, width, depth, level1, level2);

            AddDoor(doc, level1, walls[0]);

            AddWindow(doc, level1, walls[1]);
            AddWindow(doc, level1, walls[2]);
            AddWindow(doc, level1, walls[3]);

            AddRoof(doc, level2, walls, roofHeight);

            transaction.Commit();

            return Result.Succeeded;
        }

        private void AddRoof(Document doc, Level level2, List<Wall> walls, double roofHeight)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();

            double wallWidth = walls[0].Width;
            double dt = wallWidth/2;

            CurveArray curveArray = new CurveArray();

            LocationCurve curve = walls[3].Location as LocationCurve;
            XYZ p1 = curve.Curve.GetEndPoint(0);
            XYZ p2 = curve.Curve.GetEndPoint(1);

            double cat = walls[3].get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble()/2 + dt;

            double tg = UnitUtils.ConvertToInternalUnits(roofHeight, UnitTypeId.Millimeters) / cat;

            double rt = roofType.get_Parameter(BuiltInParameter.ROOF_ATTR_DEFAULT_THICKNESS_PARAM).AsDouble();

            double swing = Math.Sqrt(Math.Pow(rt, 2) + Math.Pow((rt * tg), 2));

            double wallHeight = walls[0].get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();

            //вариант 1 - крыша вровень
            double roofTop = walls[0].get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble() + swing
                + UnitUtils.ConvertToInternalUnits(roofHeight, UnitTypeId.Millimeters);

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dt, -dt, wallHeight + swing));
            points.Add(new XYZ(dt, -dt, wallHeight + swing));
            points.Add(new XYZ(dt, dt, wallHeight + swing));
            points.Add(new XYZ(-dt, dt, wallHeight + swing));
            points.Add(new XYZ(-dt, -dt, wallHeight + swing));

            XYZ zTop = new XYZ(
                                (p1 + points[3]).X,
                                (((p1 + points[3]) + (p2 + points[4])) / 2).Y,
                                roofTop);

            //вариант 2 - крыша выпирает
            //double roofTop = walls[0].get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble() + rt
            //    + UnitUtils.ConvertToInternalUnits(roofHeight, UnitTypeId.Millimeters);

            //List<XYZ> points = new List<XYZ>();
            //points.Add(new XYZ(-wallWidth, -wallWidth, wallHeight + rt));
            //points.Add(new XYZ(wallWidth, -wallWidth, wallHeight + rt));
            //points.Add(new XYZ(wallWidth, wallWidth, wallHeight + rt));
            //points.Add(new XYZ(-wallWidth, wallWidth, wallHeight + rt));
            //points.Add(new XYZ(-wallWidth, -wallWidth, wallHeight + rt));

            //XYZ zTop = new XYZ(
            //                    (p1 + points[3]).X,
            //                    (((p1 + points[3]) + (p2 + points[4])) / 2).Y,
            //                    roofTop);
            ///////////

            curveArray.Append(Line.CreateBound(p1 + points[3], zTop));
            curveArray.Append(Line.CreateBound(zTop, p2 + points[4]));

            LocationCurve length = walls[0].Location as LocationCurve;
            XYZ pl1 = length.Curve.GetEndPoint(0);
            XYZ pl2 = length.Curve.GetEndPoint(1);

            double start = (pl1 + points[0]).X;
            double end = (pl2 + points[1]).X;

            ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), doc.ActiveView);
            doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, start, end);

        }

        //private void AddRoof(Document doc, Level level2, List<Wall> walls)
        //{
        //    RoofType roofType = new FilteredElementCollector(doc)
        //        .OfClass(typeof(RoofType))
        //        .OfType<RoofType>()
        //        .Where(x => x.Name.Equals("Типовой - 400мм"))
        //        .Where(x => x.FamilyName.Equals("Базовая крыша"))
        //        .FirstOrDefault();

        //    double wallWidth = walls[0].Width;
        //    double dt = wallWidth / 2;

        //    List<XYZ> points = new List<XYZ>();
        //    points.Add(new XYZ(-dt, -dt, 0));
        //    points.Add(new XYZ(dt, -dt, 0));
        //    points.Add(new XYZ(dt, dt, 0));
        //    points.Add(new XYZ(-dt, dt, 0));
        //    points.Add(new XYZ(-dt, -dt, 0));

        //    Application application = doc.Application;

        //    CurveArray footprint = application.Create.NewCurveArray();

        //    for (int i = 0; i < 4; i++)
        //    {
        //        LocationCurve curve = walls[i].Location as LocationCurve;
        //        XYZ p1 = curve.Curve.GetEndPoint(0);
        //        XYZ p2 = curve.Curve.GetEndPoint(1);
        //        Line line = Line.CreateBound(p1 + points[i], p2 + points[i + 1]);
        //        footprint.Append(line);
        //    }

        //    ModelCurveArray footPrintToModelCurveMapping = new ModelCurveArray();
        //    FootPrintRoof footPrintRoof = doc.Create.NewFootPrintRoof(footprint, level2, roofType, out footPrintToModelCurveMapping);

        //    //ModelCurveArrayIterator iterator = footPrintToModelCurveMapping.ForwardIterator();
        //    //iterator.Reset();
        //    //while (iterator.MoveNext())
        //    //{
        //    //    ModelCurve modelCurve = iterator.Current as ModelCurve;
        //    //    footPrintRoof.set_DefinesSlope(modelCurve, true);
        //    //    footPrintRoof.set_SlopeAngle(modelCurve, 0.5);
        //    //}

        //    foreach (ModelCurve m in footPrintToModelCurveMapping)
        //    {
        //        footPrintRoof.set_DefinesSlope(m, true);
        //        footPrintRoof.set_SlopeAngle(m, 0.5);
        //    }
        //}

        private void AddWindow(Document doc, Level level1, Wall wall)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0610 x 1220 мм"))
                .Where(x => x.FamilyName.Equals("Фиксированные"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            double height = UnitUtils.ConvertToInternalUnits(1000, UnitTypeId.Millimeters);

            if (!windowType.IsActive)
                windowType.Activate();

            FamilyInstance window = doc.Create.NewFamilyInstance(point, windowType, wall, level1, StructuralType.NonStructural);
            window.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).Set(height);

        }

        private void AddDoor(Document doc, Level level1, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0915 x 2134 мм"))
                .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            if (!doorType.IsActive)
                doorType.Activate();

            doc.Create.NewFamilyInstance(point, doorType, wall, level1, StructuralType.NonStructural);
        }

        public static List<Level> GetLevels(Document doc)
        {
            List<Level> listLevel = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();

            return listLevel;
        }

        public static List<Wall> CreateWall(Document doc, double width, double depth, Level level1, Level level2)
        {
            double W = UnitUtils.ConvertToInternalUnits(width, UnitTypeId.Millimeters);
            double D = UnitUtils.ConvertToInternalUnits(depth, UnitTypeId.Millimeters);
            double dx = W / 2;
            double dy = D / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            List<Wall> walls = new List<Wall>();

            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, level1.Id, false);
                walls.Add(wall);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);
            }

            return walls;
        }

    }
}
