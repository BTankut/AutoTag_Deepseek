using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.Exceptions;
using System.Linq;

namespace TagsOrderingPlugin
{
    /// <summary>
    /// Etiketleri otomatik düzenleyen komut sınıfı
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class TagsOrderingCommand : IExternalCommand
    {
        /// <summary>
        /// Komut çalıştırıldığında işletilen metod
        /// </summary>
        /// <param name="commandData">Revit komut verileri</param>
        /// <param name="message">Hata mesajı</param>
        /// <param name="elements">Etkilenen elementler</param>
        /// <returns>Komut çalıştırma sonucu</returns>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Logger.LogInfo("Komut başlatılıyor...");
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            Logger.LogDebug($"Aktif döküman ID: {doc.Title}");

            // TransactionGroup başlat
            using (TransactionGroup transGroup = new TransactionGroup(doc, "Etiketleri Düzenle"))
            {
                try
                {
                    transGroup.Start();

                    // Element geçerliliğini kontrol et
                    if (!doc.IsValidObject)
                    {
                        message = "Geçerli bir Revit dökümanı bulunamadı.";
                        return Result.Failed;
                    }

                    // Tag seçim işlemini başlat
                    Logger.LogInfo("Etiket seçimi başlatılıyor...");
                    var tagSelector = new TagSelection(uidoc);
                    var selectedTags = tagSelector.GetSelectedTags();

                    // Seçim iptal edildiyse veya başarısız olduysa
                    if (selectedTags == null || !selectedTags.Any())
                    {
                        Logger.LogInfo("Etiket seçimi iptal edildi veya hiç etiket seçilmedi.");
                        transGroup.RollBack();
                        return Result.Cancelled;
                    }

                    Logger.LogDebug($"Seçilen etiket sayısı: {selectedTags.Count}");
                    Logger.LogDebug($"Seçilen etiket ID'leri: {string.Join(", ", selectedTags.Select(id => id.IntegerValue))}");

                    try
                    {
                        // Etiketleri otomatik sırala
                        Logger.LogInfo("Etiketler sıralanıyor...");
                        var tagSorter = new TagSorter(doc);
                        var sortedTags = tagSorter.SortByCoordinates(selectedTags);

                        if (!sortedTags.Any())
                        {
                            Logger.LogError("Etiketler sıralanamadı.");
                            transGroup.RollBack();
                            message = "Etiketler sıralanamadı.";
                            return Result.Failed;
                        }

                        Logger.LogDebug($"Sıralanan etiket sayısı: {sortedTags.Count}");
                        Logger.LogDebug($"Sıralanan etiket ID'leri: {string.Join(", ", sortedTags.Select(tag => tag.Id.IntegerValue))}");
                        Logger.LogDebug($"Sıralanan etiket konumları: {string.Join(", ", sortedTags.Select(tag => $"({tag.TagHeadPosition.X:F2}, {tag.TagHeadPosition.Y:F2})"))}");

                        // Manuel konumlandırma için başlangıç noktası al
                        var placer = new AutoSortWithManualPlacement(doc, uidoc);
                        XYZ startPoint = placer.GetStartPoint();

                        if (startPoint == null)
                        {
                            Logger.LogInfo("Başlangıç noktası seçilmedi, işlem iptal edildi.");
                            transGroup.RollBack();
                            return Result.Cancelled;
                        }

                        // Etiketleri konumlandır
                        if (!placer.PlaceSortedTags(sortedTags, startPoint))
                        {
                            Logger.LogError("Etiketler konumlandırılamadı.");
                            transGroup.RollBack();
                            message = "Etiketler konumlandırılamadı.";
                            return Result.Failed;
                        }

                        Logger.LogDebug("TransactionGroup assimilate ediliyor...");
                        transGroup.Assimilate();
                        Logger.LogInfo("İşlem başarıyla tamamlandı.");
                        TaskDialog.Show("Bilgi", "Etiketler başarıyla düzenlendi.");
                        return Result.Succeeded;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("İşlem sırasında hata", ex);
                        transGroup.RollBack();
                        message = $"İşlem sırasında hata: {ex.Message}";
                        return Result.Failed;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Beklenmeyen hata", ex);
                    transGroup.RollBack();
                    message = $"Beklenmeyen hata: {ex.Message}";
                    return Result.Failed;
                }
                finally
                {
                    Logger.LogInfo("Komut sonlandırıldı.");
                }
            }
        }
    }
}
