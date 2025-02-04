using System;
using System.Collections.Generic;
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
        // UI metinleri
        private const string DIALOG_TITLE = "Sıralama Yönü";
        private const string DIALOG_INSTRUCTION = "Etiketler nasıl sıralansın?";
        private const string DIALOG_CONTENT = "Yatay sıralama: Soldan sağa\nDikey sıralama: Yukarıdan aşağıya";
        private const string HORIZONTAL_OPTION = "Yatay Sıralama";
        private const string VERTICAL_OPTION = "Dikey Sıralama";
        private const string SUCCESS_MESSAGE = "Etiketler başarıyla düzenlendi.";
        private const string INVALID_DOC_MESSAGE = "Geçerli bir Revit dökümanı bulunamadı.";

        // Leader stil seçimi için UI metinleri
        private const string LEADER_STYLE_DIALOG_TITLE = "Leader Stil Seçimi";
        private const string LEADER_STYLE_INSTRUCTION = "Leader çizgi stili nasıl olsun?";
        private const string LEADER_STYLE_CONTENT = "Düz: Element'ten etikete direkt çizgi\nL Şekli: 90 derece kırılımlı çizgi";
        private const string STRAIGHT_LEADER_OPTION = "Düz Leader";
        private const string L_SHAPE_LEADER_OPTION = "L Şekli Leader";

        /// <summary>
        /// Komut çalıştırıldığında işletilen metod
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Logger.LogInfo("Komut başlatılıyor...");

            if (commandData?.Application == null)
            {
                message = "Geçersiz komut verisi.";
                Logger.LogError(message);
                return Result.Failed;
            }

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            Logger.LogDebug($"Aktif döküman: {doc.Title}");

            using (TransactionGroup transGroup = new TransactionGroup(doc, "Etiketleri Düzenle"))
            {
                try
                {
                    Logger.LogInfo("Execute metodu başlatıldı");
                    // Element geçerliliğini kontrol et
                    if (!doc.IsValidObject)
                    {
                        message = INVALID_DOC_MESSAGE;
                        Logger.LogError(message);
                        return Result.Failed;
                    }

                    transGroup.Start();
                    Logger.LogInfo("TransactionGroup başlatıldı");

                    // Etiketleri seç
                    Logger.LogInfo("Etiket seçimi başlatılıyor...");
                    var selectedTags = GetSelectedTags(uidoc);
                    Logger.LogInfo($"Seçilen etiket sayısı: {(selectedTags?.Count ?? 0)}");
                    
                    if (selectedTags == null)
                    {
                        Logger.LogInfo("Etiket seçimi başarısız, işlem iptal ediliyor");
                        transGroup.RollBack();
                        return Result.Cancelled;
                    }

                    // Sıralama yönünü al
                    Logger.LogInfo("Sıralama yönü seçimi başlatılıyor...");
                    var direction = GetSortingDirection();
                    Logger.LogInfo($"Seçilen sıralama yönü: {direction}");
                    
                    if (direction == null)
                    {
                        Logger.LogInfo("Sıralama yönü seçimi başarısız, işlem iptal ediliyor");
                        transGroup.RollBack();
                        return Result.Cancelled;
                    }

                    // Leader stilini al
                    Logger.LogInfo("Leader stil seçimi başlatılıyor...");
                    var leaderStyle = GetLeaderStyle();
                    Logger.LogInfo($"Seçilen leader stili: {leaderStyle}");
                    
                    if (leaderStyle == null)
                    {
                        Logger.LogInfo("Leader stil seçimi başarısız, işlem iptal ediliyor");
                        transGroup.RollBack();
                        return Result.Cancelled;
                    }

                    // Başlangıç noktasını al ve etiketleri yerleştir
                    if (!PlaceTagsAtStartPoint(doc, uidoc, selectedTags, direction, leaderStyle))
                    {
                        Logger.LogInfo("Etiket yerleştirme başarısız, işlem geri alınıyor");
                        transGroup.RollBack();
                        return Result.Failed;
                    }

                    transGroup.Assimilate();
                    TaskDialog.Show("Bilgi", SUCCESS_MESSAGE);
                    Logger.LogSuccess("İşlem başarıyla tamamlandı.");
                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Execute metodunda beklenmeyen hata", ex);
                    message = $"Beklenmeyen hata: {ex.Message}";
                    if (transGroup.HasStarted()) 
                        transGroup.RollBack();
                    return Result.Failed;
                }
            }
        }

        /// <summary>
        /// Kullanıcının seçtiği etiketleri alır
        /// </summary>
        private List<ElementId> GetSelectedTags(UIDocument uidoc)
        {
            try
            {
                Logger.LogInfo("Etiket seçimi başlatılıyor...");
                var tagSelector = new TagSelection(uidoc);
                var selectedTags = tagSelector.GetSelectedTags();

                if (selectedTags == null || !selectedTags.Any())
                {
                    Logger.LogInfo("Etiket seçimi iptal edildi veya hiç etiket seçilmedi.");
                    return null;
                }

                Logger.LogDebug($"Seçilen etiket sayısı: {selectedTags.Count}");
                Logger.LogDebug($"Seçilen etiket ID'leri: {string.Join(", ", selectedTags.Select(id => id.IntegerValue))}");

                return selectedTags;
            }
            catch (Exception ex)
            {
                Logger.LogError("Etiket seçimi sırasında hata", ex);
                return null;
            }
        }

        /// <summary>
        /// Kullanıcıdan sıralama yönünü alır
        /// </summary>
        private string GetSortingDirection()
        {
            try
            {
                Logger.LogInfo("Sıralama yönü seçimi bekleniyor...");
                var dialog = new TaskDialog(DIALOG_TITLE)
                {
                    MainInstruction = DIALOG_INSTRUCTION,
                    MainContent = DIALOG_CONTENT,
                    CommonButtons = TaskDialogCommonButtons.None
                };

                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, HORIZONTAL_OPTION);
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, VERTICAL_OPTION);

                var result = dialog.Show();
                Logger.LogInfo($"Dialog sonucu: {result}");

                if (result == TaskDialogResult.CommandLink1)
                {
                    Logger.LogInfo("Yatay sıralama seçildi");
                    return "Horizontal";
                }
                else if (result == TaskDialogResult.CommandLink2)
                {
                    Logger.LogInfo("Dikey sıralama seçildi");
                    return "Vertical";
                }
                else
                {
                    Logger.LogInfo("Sıralama seçimi iptal edildi");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Sıralama yönü seçimi sırasında hata", ex);
                return null;
            }
        }

        /// <summary>
        /// Kullanıcıdan leader stilini alır
        /// </summary>
        private string GetLeaderStyle()
        {
            try
            {
                Logger.LogInfo("Leader stil seçimi bekleniyor...");
                var dialog = new TaskDialog(LEADER_STYLE_DIALOG_TITLE)
                {
                    MainInstruction = LEADER_STYLE_INSTRUCTION,
                    MainContent = LEADER_STYLE_CONTENT,
                    CommonButtons = TaskDialogCommonButtons.None
                };

                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, STRAIGHT_LEADER_OPTION);
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, L_SHAPE_LEADER_OPTION);

                var result = dialog.Show();
                Logger.LogInfo($"Dialog sonucu: {result}");

                if (result == TaskDialogResult.CommandLink1)
                {
                    Logger.LogInfo("Düz leader seçildi");
                    return "Straight";
                }
                else if (result == TaskDialogResult.CommandLink2)
                {
                    Logger.LogInfo("L şekilli leader seçildi");
                    return "LShape";
                }
                else
                {
                    Logger.LogInfo("Leader stil seçimi iptal edildi");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Leader stil seçimi sırasında hata", ex);
                return null;
            }
        }

        /// <summary>
        /// Etiketleri başlangıç noktasına göre yerleştirir
        /// </summary>
        private bool PlaceTagsAtStartPoint(Document doc, UIDocument uidoc, List<ElementId> selectedTags, string direction, string leaderStyle)
        {
            try
            {
                var placer = new AutoSortWithManualPlacement(doc, uidoc);
                var startPoint = placer.GetStartPoint();

                if (startPoint == null)
                {
                    Logger.LogInfo("Başlangıç noktası seçilmedi, işlem iptal edildi.");
                    return false;
                }

                Logger.LogInfo($"Etiketler {direction} yönünde sıralanıyor...");
                var tagSorter = new TagSorter(doc);
                var sortedTags = tagSorter.SortByCoordinates(selectedTags, direction, startPoint);

                if (!sortedTags.Any())
                {
                    Logger.LogInfo("Sıralanacak etiket bulunamadı.");
                    return false;
                }

                Logger.LogInfo($"Etiketler {direction} yönünde yerleştiriliyor...");
                if (!placer.PlaceSortedTags(sortedTags, startPoint, direction, leaderStyle))
                {
                    Logger.LogError("Etiketler yerleştirilemedi.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Etiket yerleştirme sırasında hata", ex);
                return false;
            }
        }
    }
}
