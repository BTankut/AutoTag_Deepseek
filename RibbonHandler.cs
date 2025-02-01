using System;
using System.Reflection;
using Autodesk.Revit.UI;
using System.Windows.Media.Imaging;
using System.IO;

namespace TagsOrderingPlugin
{
    /// <summary>
    /// Revit Ribbon arayüzü yönetim sınıfı
    /// </summary>
    public class RibbonHandler
    {
        private const string BASE64_ICON = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAABHNCSVQICAgIfAhkiAAAAAlwSFlzAAAApgAAAKYB3X3/OAAAABl0RVh0U29mdHdhcmUAd3d3Lmlua3NjYXBlLm9yZ5vuPBoAAANCSURBVEiJtZZPbBtFFMZ/M7ubXdtdb1xSFyeilBapySVU8h8OoFaooFSqiihIVIpQBKci6KEg9Q6H9kovIHoCIVQJJCKE1ENFjnAgcaSGC6rEnxBwA04Tx43t2FnvDAfjkNibxgHxnWb2e/u992bee7tCa00YFsffekFY+nUzFtjW0LrvjRXrCDIAaPLlW0nHL0SsZtVoaF98mLrx3pdhOqLtYPHChahZcYYO7KvPFxvRl5XPp1sN3adWiD1ZAqD6XYK1b/dvE5IWryTt2udLFedwc1+9kLp+vbbpoDh+6TklxBeAi9TL0taeWpdmZzQDry0AcO+jQ12RyohqqoYoo8RDwJrU+qXkjWtfi8Xxt58BdQuwQs9qC/afLwCw8tnQbqYAPsgxE1S6F3EAIXux2oQFKm0ihMsOF71dHYx+f3NND68ghCu1YIoePPQN1pGRABkJ6Bus96CutRZMydTl+TvuiRW1m3n0eDl0vRPcEysqdXn+jsQPsrHMquGeXEaY4Yk4wxWcY5V/9scqOMOVUFthatyTy8QyqwZ+kDURKoMWxNKr2EeqVKcTNOajqKoBgOE28U4tdQl5p5bwCw7BWquaZSzAPlwjlithJtp3pTImSqQRrb2Z8PHGigD4RZuNX6JYj6wj7O4TFLbCO/Mn/m8R+h6rYSUb3ekokRY6f/YukArN979jcW+V/S8g0eT/N3VN3kTqWbQ428m9/8k0P/1aIhF36PccEl6EhOcAUCrXKZXXWS3XKd2vc/TRBG9O5ELC17MmWubD2nKhUKZa26Ba2+D3P+4/MNCFwg59oWVeYhkzgN/JDR8deKBoD7Y+ljEjGZ0sosXVTvbc6RHirr2reNy1OXd6pJsQ+gqjk8VWFYmHrwBzW/n+uMPFiRwHB2I7ih8ciHFxIkd/3Omk5tCDV1t+2nNu5sxxpDFNx+huNhVT3/zMDz8usXC3ddaHBj1GHj/As08fwTS7Kt1HBTmyN29vdwAw+/wbwLVOJ3uAD1wi/dUH7Qei66PfyuRj4Ik9is+hglfbkbfR3cnZm7chlUWLdwmprtCohX4HUtlOcQjLYCu+fzGJH2QRKvP3UNz8bWk1qMxjGTOMThZ3kvgLI5AzFfo379UAAAAASUVORK5CYII=";

        /// <summary>
        /// Ribbon arayüzünü oluşturur
        /// </summary>
        /// <param name="application">Revit UI uygulama nesnesi</param>
        public static void CreateRibbon(UIControlledApplication application)
        {
            try
            {
                // Ribbon sekmesi oluştur
                string tabName = "BIRD testTask";
                application.CreateRibbonTab(tabName);

                // Panel oluştur
                RibbonPanel panel = application.CreateRibbonPanel(tabName, "Tags Tools");

                // Assembly bilgilerini al
                string assemblyPath = Assembly.GetExecutingAssembly().Location;

                var version = Assembly.GetExecutingAssembly().GetName().Version;
                PushButtonData buttonData = new PushButtonData(
                    "AutoArrangeButton",
                    $"AutoTag v{version.Major}.{version.Minor}.{version.Build}",
                    assemblyPath,
                    "TagsOrderingPlugin.TagsOrderingCommand")
                {
                    ToolTip = "Etiketleri otomatik düzenle",
                    LargeImage = GetImageFromBase64(BASE64_ICON)
                };

                PushButton button = panel.AddItem(buttonData) as PushButton;
                button.ItemText = $"AutoTag v{version.Major}.{version.Minor}.{version.Build}";
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Hata", $"Ribbon oluşturulurken hata oluştu: {ex.Message}");
            }
        }

        /// <summary>
        /// Base64 formatındaki string'i BitmapImage'a dönüştürür
        /// </summary>
        private static BitmapImage GetImageFromBase64(string base64)
        {
            try
            {
                byte[] imageBytes = Convert.FromBase64String(base64);
                BitmapImage image = new BitmapImage();
                
                using (MemoryStream ms = new MemoryStream(imageBytes))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                }
                
                return image;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
