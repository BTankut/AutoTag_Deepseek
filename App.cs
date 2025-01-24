using System;
using System.Reflection;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace TagsOrderingPlugin
{
    /// <summary>
    /// Revit etiketlerini düzenlemek için ana uygulama sınıfı
    /// </summary>
    public class App : IExternalApplication
    {
        public static UIControlledApplication UIApp { get; private set; }

        /// <summary>
        /// Revit başlatıldığında çağrılan metod
        /// </summary>
        /// <param name="application">Revit UI uygulama nesnesi</param>
        /// <returns>Başarı durumunu gösteren sonuç</returns>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                UIApp = application;

                // Uygulama bilgilerini al
                string assemblyPath = Assembly.GetExecutingAssembly().Location;

                // Temel kontrolleri yap
                if (application == null)
                {
                    TaskDialog.Show("Hata", "Uygulama başlatılamadı: UIControlledApplication null.");
                    return Result.Failed;
                }

                // Ribbon UI'ı oluştur
                RibbonHandler.CreateRibbon(application);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Hata", $"Uygulama başlatılırken hata oluştu: {ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Revit kapatıldığında çağrılan metod
        /// </summary>
        /// <param name="application">Revit UI uygulama nesnesi</param>
        /// <returns>Başarı durumunu gösteren sonuç</returns>
        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                UIApp = null;
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Hata", $"Uygulama kapatılırken hata oluştu: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}
