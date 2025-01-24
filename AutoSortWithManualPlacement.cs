using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace TagsOrderingPlugin
{
    /// <summary>
    /// Etiketleri otomatik sıralayıp manuel konumlandıran sınıf
    /// </summary>
    public class AutoSortWithManualPlacement
    {
        private readonly Document _doc;
        private readonly UIDocument _uiDoc;
        private const double SPACING_MM = 500.0; // 500mm sabit aralık

        /// <summary>
        /// AutoSortWithManualPlacement sınıfının yapıcı metodu
        /// </summary>
        /// <param name="doc">Aktif Revit dökümanı</param>
        /// <param name="uiDoc">Aktif UI dökümanı</param>
        public AutoSortWithManualPlacement(Document doc, UIDocument uiDoc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _uiDoc = uiDoc ?? throw new ArgumentNullException(nameof(uiDoc));
        }

        /// <summary>
        /// Kullanıcıdan başlangıç noktası seçmesini ister
        /// </summary>
        /// <returns>Seçilen nokta veya null</returns>
        public XYZ GetStartPoint()
        {
            try
            {
                Logger.LogInfo("Başlangıç noktası seçimi bekleniyor...");
                XYZ point = _uiDoc.Selection.PickPoint("Etiketlerin başlayacağı noktayı seçin");
                Logger.LogDebug($"Seçilen nokta: X={point.X}, Y={point.Y}, Z={point.Z}");
                return point;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                Logger.LogInfo("Nokta seçimi iptal edildi.");
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError("Nokta seçimi sırasında hata", ex);
                return null;
            }
        }

        /// <summary>
        /// Sıralanmış etiketleri belirtilen noktadan başlayarak konumlandırır
        /// </summary>
        /// <param name="sortedTags">Sıralanmış etiketler</param>
        /// <param name="startPoint">Başlangıç noktası</param>
        /// <returns>Başarı durumu</returns>
        public bool PlaceSortedTags(List<IndependentTag> sortedTags, XYZ startPoint, TagSortDirection direction)
        {
            try
            {
                if (sortedTags == null || !sortedTags.Any() || startPoint == null)
                    return false;

                using (Transaction trans = new Transaction(_doc, "Etiketleri Konumlandır"))
                {
                    trans.Start();

                    try
                    {
                        // Spacing değerlerini hesapla
                        double spacingFeet = UnitUtils.ConvertToInternalUnits(SPACING_MM, UnitTypeId.Millimeters);
                        Logger.LogInfo($"Aralık mesafesi: {SPACING_MM}mm = {spacingFeet:F6} feet");

                        // Başlangıç noktasını belirle
                        XYZ currentPosition = startPoint;

                        // Etiketleri konumlandır
                        foreach (var tag in sortedTags)
                        {
                            try
                            {
                                // 1. Etiket konumunu güncelle
                                tag.TagHeadPosition = currentPosition;
                                Logger.LogSuccess($"ID {tag.Id}: Yeni konum X={currentPosition.X:F3}, Y={currentPosition.Y:F3}");

                                // 2. Lider ayarlarını güncelle
                                if (tag.HasLeader && tag.GetTaggedLocalElementIds().Any())
                                {
                                    var element = _doc.GetElement(tag.GetTaggedLocalElementIds().First());
                                    if (element != null && element.IsValidObject)
                                    {
                                        tag.LeaderEndCondition = LeaderEndCondition.Attached;
                                        var reference = tag.GetTaggedReferences().First();
                                        
                                        if (element.Location is LocationPoint locPoint)
                                        {
                                            tag.SetLeaderEnd(reference, locPoint.Point);
                                            Logger.LogInfo($"ID {tag.Id}: Lider element konumuna bağlandı");
                                        }
                                    }
                                }

                                // 3. Bir sonraki pozisyonu hesapla
                                currentPosition = direction == TagSortDirection.Horizontal
                                    ? new XYZ(
                                        currentPosition.X + spacingFeet,  // X ekseninde sağa doğru
                                        currentPosition.Y,
                                        currentPosition.Z
                                    )
                                    : new XYZ(
                                        currentPosition.X,
                                        currentPosition.Y - spacingFeet,  // Y ekseninde aşağı doğru
                                        currentPosition.Z
                                    );
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"ID {tag.Id}: İşlem başarısız - {ex.Message}");
                                continue;
                            }
                        }

                        trans.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Taşıma hatası", ex);
                        trans.RollBack();
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Kritik hata", ex);
                return false;
            }
        }
    }
}
