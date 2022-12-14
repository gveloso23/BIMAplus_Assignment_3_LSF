#region Namespaces
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

#endregion

namespace LSFRevit
{
    class App : IExternalApplication
    {
        const string RIBBON_TAB = "BIM Aplus";
        const string RIBBON_PANEL = "LSF A+";
        public RibbonPanel panel = null;
        public Result OnStartup(UIControlledApplication a)
        {
            // GET THE RIBBON TAB
            try
            {
                a.CreateRibbonTab(RIBBON_TAB);
            }
            catch (Exception) { }

            // GET OR CREATE THE PANEL

            List<RibbonPanel> panels = a.GetRibbonPanels(RIBBON_TAB);

            foreach (RibbonPanel pnl in panels)
            {
                if (pnl.Name == RIBBON_PANEL)
                {
                    panel = pnl;
                    break;
                }
            }
            if (panel == null)
            {
                panel = a.CreateRibbonPanel(RIBBON_TAB, RIBBON_PANEL);
            }

            SplitButton group1 = null;
            SplitButtonData group1Data = new SplitButtonData("LSF", "LSF");
            group1 = panel.AddItem(group1Data) as SplitButton;

            // BUTTON IFCjs

            // GET THE IMAGE FOR THE BUTTON (ADD IN REFERENCES)
            Image img = LSFRevit.Properties.resources.lsficon;
            ImageSource imgSrc = GetImageSource(img);
            // CREATE BUTTON DATA
            PushButtonData btnData = new PushButtonData("Generate LSF", "Generate\nLSF", Assembly.GetExecutingAssembly().Location, typeof(Command).FullName)
            {
                //ToolTipImage = ttimgSrc,
                ToolTip = "Generate Frames based on Walls",
                LongDescription = "Generate Frames based on Walls",
                Image = imgSrc,
                LargeImage = imgSrc
            };


            // ADD THE BUTTON TO THE RIBBON

            PushButton button1 = group1.AddPushButton(btnData) as PushButton;
            button1.Enabled = true;

            return Result.Succeeded;
        }

        private BitmapSource GetImageSource(Image img)
        {
            BitmapImage bmp = new BitmapImage();
            using (MemoryStream ms = new MemoryStream())
            {
                img.Save(ms, ImageFormat.Png); ;
                ms.Position = 0;

                bmp.BeginInit();

                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = null;
                bmp.StreamSource = ms;

                bmp.EndInit();
            }

            return bmp;
        }
        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }
    }
}
