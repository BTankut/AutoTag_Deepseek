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
        public bool PlaceSortedTags(List<IndependentTag> sortedTags, XYZ startPoint)
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
                        // Metrik→Feet dönüşümünü logla
                        double spacingFeet = UnitUtils.ConvertToInternalUnits(SPACING_MM, UnitTypeId.Millimeters);
                        Logger.LogInfo($"500mm = {spacingFeet:F6} feet");

                        // Tag listesinin yönünü kontrol et
                        var firstTag = sortedTags.First();
                        var hostElement = _doc.GetElement(firstTag.GetTaggedLocalElementIds().First());
                        var hostLocation = (hostElement.Location as LocationPoint)?.Point;
                        
                        if (hostLocation != null)
                        {
                            bool isNegativeX = firstTag.TagHeadPosition.X < hostLocation.X;
                            Logger.LogInfo($"Tag listesi yönü: {(isNegativeX ? "-X" : "+X")}");

                            // -X yönünde ise yeni sıralı liste oluştur
                            if (isNegativeX)
                            {
                                sortedTags = sortedTags.OrderByDescending(t => t.TagHeadPosition.Y).ToList();
                                Logger.LogInfo("Liste -X yönünde, sıralama tersine çevrildi");
                            }
                            else
                            {
                                sortedTags = sortedTags.OrderBy(t => t.TagHeadPosition.Y).ToList();
                                Logger.LogInfo("Liste +X yönünde, normal sıralama");
                            }
                        }

                        // Tüm etiketlerin ORİJİNAL konumlarını logla
                        Logger.LogInfo("Etiketlerin Orijinal Konumları:");
                        foreach (var tag in sortedTags)
                        {
                            Logger.LogInfo($"ID: {tag.Id} | X={tag.TagHeadPosition.X:F3}, Y={tag.TagHeadPosition.Y:F3}");
                        }

                        // Etiketleri YENİ KONUMLARA taşı
                        for (int i = 0; i < sortedTags.Count; i++)
                        {
                            var tag = sortedTags[i];

                            // Yeni konumu hesapla (Y ekseninde alt alta)
                            XYZ newPosition = new XYZ(
                                startPoint.X,                    // X sabit
                                startPoint.Y - (i * spacingFeet), // Y ekseninde aşağı doğru
                                startPoint.Z                     // Z sabit
                            );

                            try
                            {
                                // Etiketin pozisyonunu DEĞİŞTİR
                                tag.TagHeadPosition = newPosition;
                                Logger.LogSuccess($"ID {tag.Id}: Yeni konum X={newPosition.X:F3}");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"ID {tag.Id}: Konum değiştirilemedi - {ex.Message}");
                                
                                // Alternatif yöntem dene
                                try
                                {
                                    ElementTransformUtils.MoveElement(_doc, tag.Id, newPosition - tag.TagHeadPosition);
                                    Logger.LogSuccess($"ID {tag.Id}: Alternatif yöntemle taşındı");
                                }
                                catch (Exception moveEx)
                                {
                                    Logger.LogError($"ID {tag.Id}: Alternatif yöntem de başarısız - {moveEx.Message}");
                                }
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
