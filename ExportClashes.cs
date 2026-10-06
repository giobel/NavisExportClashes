using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using Autodesk.Navisworks.Api.Plugins;
using Autodesk.Navisworks.Api.Interop;
using Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi.ComApiBridge;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

/*
"C:\Program Files\Autodesk\Navisworks Manage 2026\roamer.exe" -OpenFile "C:\APAM Models\Clash Detection Master.nwf" -ShowGui -ExecuteAddInPlugin "apam_clash.Export.ExportClashes.APAM" "C:\TEMP\LatestClashReport.csv" -Exit
*/


namespace apam_clash.Export{

   [PluginAttribute("apam_clash.Export.ExportClashes",                         //Plugin name
                        "APAM",                                 //4 character Developer ID or GUID
                        ToolTip = "Export clashes tool tip",//The tooltip for the item in the ribbon
                        DisplayName = "Export clashes")]          //Display name for the Plugin in the Ribbon

   public class ExportClashes : AddInPlugin                        //Derives from AddInPlugin
   {
      public override int Execute(params string[] parameters)
      {
           var sb = new StringBuilder();
            DateTime time = DateTime.Now;
            string headers = "Test Name,Last Run,Display Name,Created Time,Status,Description,X,Y,Z,Data Exported,Image";
            int width = 256;
            int height = 256;
            string defaultPath = @"C:\Temp";
            bool outputPathProvided = parameters != null &&
                                      parameters.Length > 0 &&
                                      !string.IsNullOrWhiteSpace(parameters[0]);
            string outputPath = outputPathProvided
                ? parameters[0]
                : Path.Combine(defaultPath, $"{time:yyyyMMdd_HHmmss}_export.csv");
            var logger = new ExportLogger(Path.ChangeExtension(outputPath, ".log"));

            sb.AppendLine(headers);

            try{
            logger.Info($"Starting clash export. Output CSV: {outputPath}");

            // current document (.NET)
            Document doc = Application.ActiveDocument;
            logger.Info($"Active document: {doc.FileName}");

            InwOpState10 cdoc = ComApiBridge.State;

            DocumentClash documentClash = doc.GetClash();
            DocumentClashTests clashTests = documentClash.TestsData;
            logger.Info($"Found {clashTests.Tests.Count} clash tests.");


            //clashTests.TestsRunAllTests();

            foreach (ClashTest ctest in clashTests.Tests)
                {
                    clashTests.TestsRunTest(ctest);
                }


            foreach (ClashTest ctest in clashTests.Tests)
                {
                        if (ctest is ClashTest clashTest)
                            {

                            DateTime? lastRun = ctest.LastRun;


                            foreach (ClashResult result in clashTest.Children)
                            {
                                if (result.Status == ClashResultStatus.Active)
                                {

                                    logger.Info($"Exporting active clash '{ctest.DisplayName} / {result.DisplayName}'.");
                                    string filePath = $@"C:\Apam\OneDrive - Australia Pacific Airports Corporation LTD\Pictures\ClashTest\{ctest.DisplayName}_{result.DisplayName}.png";
                                    logger.Info($"Saving clash image to '{filePath}'.");

                                    ModelItem itemA = result.CompositeItem1;
                                    ModelItem itemB = result.CompositeItem2;

                                        Point3D center = result.Center;
                                            double x = center.X;
                                            double y = center.Y;
                                            double z = center.Z;
                                    
                                    //CreateAndFillImage(doc, result, path, ctest.DisplayName, 800, 600);
                                var instance = LcClCurrentIssue.GetInstance((LcOpState)Autodesk.Navisworks.Api.Application.MainDocument.State);
                                logger.Info($"Setting current issue from saved item '{result.DisplayName}'.");
                                
                                instance.SetCurrentIssueFromSavedItem((SavedItem)result,0,false,true);
                                logger.Info($"Calling OnGotFocus for current issue.");
                                
                                instance.OnGotFocus();
                                logger.Info($"Generating clash image for '{ctest.DisplayName} / {result.DisplayName}' with dimensions {width}x{height}.");


                                using (Bitmap clashImage = doc.ActiveView.GenerateImage(ImageGenerationStyle.Scene,width, height,false))
                                {

                                    clashImage.Save(filePath);
                                    logger.Info($"Clash image saved to '{filePath}'.");

                                }
                                string imageBase64 = PngToBase64(filePath);
                                logger.Info($"Clash image converted to Base64 for '{ctest.DisplayName} / {result.DisplayName}'.");

                                // Process clash
                                string csvRow = string.Join(",",
                                    CsvQuote(ctest.DisplayName),
                                    CsvQuote(lastRun?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""),
                                    CsvQuote(result.DisplayName),
                                    CsvQuote(result.CreatedTime.ToString()),
                                    CsvQuote(result.Status.ToString()),
                                    CsvQuote($"{itemA.DisplayName} vs {itemB.DisplayName}"),
                                    CsvQuote(x.ToString()),
                                    CsvQuote(y.ToString()),
                                    CsvQuote(z.ToString()),
                                    CsvQuote(time.ToString()),
                                    CsvQuote(imageBase64));

                                sb.AppendLine(csvRow);
                                
                                    logger.Info($"Exported active clash '{ctest.DisplayName} / {result.DisplayName}'.");
                                }
                            }

                        }
                }
            logger.Info("Clash export completed successfully.");
            // Show the names in a message box
            //System.Windows.Forms.MessageBox.Show($"{selectedItems.Count} exported");

            return 0;
            }
            catch(Exception ex){
                logger.Error("Clash export failed.", ex);
                sb.AppendLine($"Error: {ex.Message}");
                return 1;
            }

            finally{
                string outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                Directory.CreateDirectory(outputDirectory);
                File.WriteAllText(outputPath, sb.ToString());
                logger.Info($"CSV written to '{outputPath}'.");
                if (!outputPathProvided)
                {
                    System.Diagnostics.Process.Start(outputPath);
                }
            }
        }

/*
private static void PrepareClashView(
    Document doc,
    ClashResult result,
    ModelItemCollection allItems,
    ModelItemCollection clashItems)
    {
    var currentIssue = LcClCurrentIssue.GetInstance(
        (LcOpState)Autodesk.Navisworks.Api.Application.MainDocument.State);

    currentIssue.SetCurrentIssueFromSavedItem(
        (SavedItem)result,
        0,
        false,
        true);

    currentIssue.OnGotFocus();

    doc.Models.ResetAllPermanentMaterials();

    doc.Models.OverridePermanentTransparency(
        allItems,
        0.9);

    doc.Models.OverridePermanentTransparency(
        clashItems,
        0.0);

    doc.ActiveView.RequestDelayedRedraw(
        ViewRedrawRequests.All);
}
*/


        private static string PngToBase64(string filePath)
{
    using (var original = System.Drawing.Image.FromFile(filePath))
    {
        const int maxDimension = 800;
        const int maxEncodedBytes = 500 * 1024;

        double scale = Math.Min(
            (double)maxDimension / original.Width,
            (double)maxDimension / original.Height
        );

        // Don't enlarge small images
        scale = Math.Min(scale, 1.0);

        int width = (int)(original.Width * scale);
        int height = (int)(original.Height * scale);

        using (var resized = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
        using (var graphics = System.Drawing.Graphics.FromImage(resized))
        using (var output = new MemoryStream())
        {
            graphics.DrawImage(
                original,
                new System.Drawing.Rectangle(0, 0, width, height)
            );

            resized.Save(output, ImageFormat.Jpeg);
            byte[] encodedImage = output.ToArray();

            if (encodedImage.Length > maxEncodedBytes)
            {
                throw new InvalidOperationException(
                    $"Encoded image is {encodedImage.Length} bytes; maximum is {maxEncodedBytes} bytes.");
            }

            return "data:image/jpeg;base64," + Convert.ToBase64String(encodedImage);
        }
    }
}



private static Bitmap GenerateClashImage(Document doc, ClashResult result, int width, int height)
{
    ModelItem itemA = result.CompositeItem1;
    ModelItem itemB = result.CompositeItem2;
    if (itemA == null || itemB == null)
    {
        throw new InvalidOperationException($"Clash result '{result.DisplayName}' does not have both clash items.");
    }

    Viewpoint originalViewpoint = doc.CurrentViewpoint.Value.CreateCopy();
    try
    {
        ModelItemCollection allItems = new ModelItemCollection();
        foreach (Model model in doc.Models)
        {
            if (model.RootItem != null)
            {
                GetAllItems(new[] { model.RootItem }, ref allItems);
            }
        }

        ModelItemCollection clashItems = new ModelItemCollection();
        clashItems.Add(itemA);
        clashItems.Add(itemB);

        var currentIssue = LcClCurrentIssue.GetInstance(
            (LcOpState)Autodesk.Navisworks.Api.Application.MainDocument.State);
        currentIssue.SetCurrentIssueFromSavedItem((SavedItem)result, 0, false, true);
        currentIssue.OnGotFocus();

        doc.Models.ResetAllPermanentMaterials();
        doc.Models.OverridePermanentTransparency(allItems, 0.9);
        doc.Models.OverridePermanentTransparency(clashItems, 0.0);

        doc.ActiveView.RequestDelayedRedraw((ViewRedrawRequests)3);

        return doc.ActiveView.GenerateImage(ImageGenerationStyle.Scene, width, height, false);
    }
    finally
    {
        doc.CurrentSelection.Clear();
        doc.Models.ResetAllPermanentMaterials();
        doc.CurrentViewpoint.CopyFrom(originalViewpoint);
        doc.ActiveView.RequestDelayedRedraw((ViewRedrawRequests)3);
    }
}

private static string ImageToBase64(System.Drawing.Image original)
{
        const int maxDimension = 800;
        const int maxEncodedBytes = 500 * 1024;

        double scale = Math.Min(
            (double)maxDimension / original.Width,
            (double)maxDimension / original.Height
        );

        // Don't enlarge small images
        scale = Math.Min(scale, 1.0);

        int width = (int)(original.Width * scale);
        int height = (int)(original.Height * scale);

        using (var resized = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
        using (var graphics = System.Drawing.Graphics.FromImage(resized))
        using (var output = new MemoryStream())
        {
            graphics.DrawImage(
                original,
                new System.Drawing.Rectangle(0, 0, width, height)
            );

            resized.Save(output, ImageFormat.Jpeg);
            byte[] encodedImage = output.ToArray();

            if (encodedImage.Length > maxEncodedBytes)
            {
                throw new InvalidOperationException(
                    $"Encoded image is {encodedImage.Length} bytes; maximum is {maxEncodedBytes} bytes.");
            }

            return "data:image/jpeg;base64," + Convert.ToBase64String(encodedImage);
        }
}


   private void CreateAndFillImage(Document doc, ClashResult clResult, string directoryPath, string testName,int width, int height)
        {
            if (clResult != null)
            {
                Viewpoint viewpoint = doc.CurrentViewpoint.Value;
                // Get the 2 clashing elements from the ClashResult
                ModelItem item1 = clResult.Item1;
                ModelItem item2 = clResult.Item2;
                if (item1 != null && item2 != null)
                {
                    ModelItemCollection items = new ModelItemCollection();
                    items.Add(item1);
                    items.Add(item2);
                    doc.CurrentSelection.Clear();

                    //Show clash items
                    ModelItemCollection modelItemsToShow = new ModelItemCollection();
                    foreach (ModelItem item in items)
                    {
                        if (item.AncestorsAndSelf != null)
                            modelItemsToShow.AddRange(item.AncestorsAndSelf);

                        if (item.Descendants != null)
                            modelItemsToShow.AddRange(item.Descendants);
                    }
                    doc.Models.SetHidden(modelItemsToShow, false);
                    // Select the 2 clashing elements
                    doc.CurrentSelection.Clear();
                    doc.CurrentSelection.CopyFrom(items);
                    doc.ActiveView.FocusOnCurrentSelection();
                    doc.CurrentSelection.Clear();

                    // Adjust the camera, lighting, and paint the clashing elements in Red and Green respectively
                    Viewpoint copy = viewpoint.CreateCopy();
                    copy.Lighting = 0;

                    doc.Models.ResetAllPermanentMaterials();
                    doc.CurrentViewpoint.CopyFrom(copy);

                    Autodesk.Navisworks.Api.Color RED = Autodesk.Navisworks.Api.Color.Red;
                    Autodesk.Navisworks.Api.Color GREEN = Autodesk.Navisworks.Api.Color.Green;

                    if (!NativeHandle.ReferenceEquals(items.ElementAtOrDefault(0), null))
                        doc.Models.OverridePermanentColor(new ModelItem[1] { items.ElementAtOrDefault(0) }, RED);

                    if (!NativeHandle.ReferenceEquals(items.ElementAtOrDefault(1), null))
                        doc.Models.OverridePermanentColor(new ModelItem[1] { items.ElementAtOrDefault(1) }, GREEN);

                    doc.ActiveView.LookFromFrontRightTop();
                    doc.ActiveView.RequestDelayedRedraw((ViewRedrawRequests)3);

                    // Save the Clash image
                    using (System.Drawing.Bitmap clashImage = doc.ActiveView.GenerateImage(ImageGenerationStyle.Scene, width, height, false))
                    {
                        string clashResultImageName = Path.Combine(directoryPath, $"{testName}_{clResult.DisplayName}.png");

                        clashImage.Save(clashResultImageName, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    //Hide clashelements
                    doc.Models.SetHidden(modelItemsToShow, true);
                }
            }
        }

        private void HideAllItems(Document doc)
        {
            ModelItemCollection rootItems = new ModelItemCollection();
            ModelItemCollection allitems = new ModelItemCollection();
            foreach (var m in doc.Models)
            {
                if (m.RootItem != null)
                {
                    rootItems.Add(m.RootItem);
                }
            }
            GetAllItems(rootItems, ref allitems);
            if (allitems != null && allitems.Count > 0)
            {
                doc.Models.SetHidden(allitems, true);
            }
        }

        private static void GetAllItems(IEnumerable<ModelItem> items, ref ModelItemCollection allItems)
        {
            if (items != null)
            {
                foreach (ModelItem item in items)
                {
                    allItems.Add(item);
                    if (item.Children != null)
                    {
                        GetAllItems(item.Children, ref allItems);
                    }

                }
            }
        }



        static List<ModelItem> GetItemsWithGeometry(Document doc)
        {
            var itemsWithGeom = new List<ModelItem>();

            void Traverse(ModelItem item)
            {
                // Has geometry?
                //if (!item.IsEmpty)
                    itemsWithGeom.Add(item);

                foreach (var child in item.Children)
                    Traverse(child);
            }

            foreach (var root in doc.Models.RootItems)
                Traverse(root);
            
            //swf.MessageBox.Show(itemsWithGeom.Count.ToString());

            return itemsWithGeom;
        }

        static string GetPropertyValue(DataProperty prop)
        {
            if (prop == null || prop.Value == null)
                return "";

            var v = prop.Value;


            if (v.IsDisplayString)
                return v.ToDisplayString();

            if (v.DataType.ToString() == "DoubleVolume")
                return UnitConvert.Ft3ToMeters3(v.ToAnyDouble()).ToString();

            if (v.DataType.ToString() == "DoubleLength")
                return UnitConvert.FtToMeters(v.ToAnyDouble()).ToString();

            if (v.DataType.ToString() == "DoubleArea")
                return UnitConvert.Ft2ToMeters2(v.ToAnyDouble()).ToString();

            if (v.IsDouble)
                return v.ToDouble().ToString();

            if (v.IsAnyDouble)
                return v.ToAnyDouble().ToString();

            if (v.IsInt32)
                return v.ToInt32().ToString();

            if (v.IsBoolean)
                return v.ToBoolean().ToString();

            if (v.IsNamedConstant)
            {
                string nc = ExtractInsideParentheses(v.ToString());
                return $"{CsvQuote(nc)}";
            }

            if (v.IsDateTime)
                return v.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss");

            // Fallback — safe catch-all
            return v.ToString();
        }

        public static class UnitConvert
        {
            private const double FtToM = 0.3048;
            private const double Ft2ToM2 = 0.09290304;
            private const double Ft3ToM3 = 0.028316846592;

            // === LENGTH ===
            public static double FtToMeters(double ft) => ft * FtToM;
            public static double MetersToFt(double m) => m / FtToM;

            // === AREA ===
            public static double Ft2ToMeters2(double ft2) => ft2 * Ft2ToM2;
            public static double Meters2ToFt2(double m2) => m2 / Ft2ToM2;

            // === VOLUME ===
            public static double Ft3ToMeters3(double ft3) => ft3 * Ft3ToM3;
            public static double Meters3ToFt3(double m3) => m3 / Ft3ToM3;
        }

        static string GetPropertyValueAndType(DataProperty prop)
                {
                    if (prop == null || prop.Value == null)
                        return "";

                    var v = prop.Value;

                    if (v.IsDisplayString)
                        return $"{v.ToDisplayString()},DisplayString";

                    if (v.IsDouble)
                        return $"{v.ToDouble()},Double";

                    if (v.IsAnyDouble)
                        return $"{v.ToAnyDouble()},AnyDouble";

                    if (v.IsInt32)
                        return $"{v.ToInt32()},Int32";

                    if (v.IsBoolean)
                        return $"{v.ToBoolean()},Boolean";

                    if (v.IsNamedConstant)
                    {
                        //var nc = v.ToNamedConstant();
                        string nc = ExtractInsideParentheses(v.ToString());
                    return $"{CsvQuote(nc)},NamedConstant";
                    }

                    if (v.IsDateTime)
                        return $"{v.ToDateTime():yyyy-MM-dd HH:mm:ss},DateTime";

                    // Fallback — safe catch-all
                    return $"{v.ToString()},Unknown";
                }

        static string ExtractInsideParentheses(string input)
            {
                if (string.IsNullOrEmpty(input))
                    return "";

                int start = input.IndexOf('(');
                int end = input.LastIndexOf(')');

                if (start < 0 || end < 0 || end <= start)
                    return input;

                return input.Substring(start + 1, end - start - 1);
            }
        static string CsvQuote(string value)
        {
            if (value == null)
                return string.Empty;

            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
                return value;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }



        private static  InwOaProperty AddProperty(InwOpState10 cdoc, string name, DateTime dt)
        {
            InwOaProperty prop = (InwOaProperty)cdoc.ObjectFactory(nwEObjectType.eObjectType_nwOaProperty, null, null);
            prop.name = name;
            prop.UserName = name;

            var v = RuntimeHelpers.GetObjectValue(ToUtc(dt));
            prop.value = v;
            return prop;
        }


        private static InwOaProperty AddProperty(InwOpState10 cdoc, string name, object dt)
        {
            InwOaProperty prop = (InwOaProperty)cdoc.ObjectFactory(nwEObjectType.eObjectType_nwOaProperty, null, null);
            prop.name = name;
            prop.UserName = name;

            var v = dt;
            prop.value = v;
            return prop;
        }

        private static DateTime ToUtc(DateTime dt)
        {

            DateTime time = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, DateTimeKind.Local);
            return TimeZone.CurrentTimeZone.ToUniversalTime(time);

        }





    }
    
    
}