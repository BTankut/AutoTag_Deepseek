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
        private const double SPACING_MM = 100.0; // 100mm sabit aralık
        private const double SAFETY_MARGIN = 100.0; // 100mm güvenlik marjı
        private const double OFFSET_MM = 100.0; // 100mm offset mesafesi

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

        private Element GetFirstTaggedElement(IndependentTag tag)
        {
            var elements = tag.GetTaggedLocalElements();
            return elements?.FirstOrDefault();
        }

        private BoundingBoxXYZ GetTagBoundingBox(IndependentTag tag, XYZ position)
        {
            var element = GetFirstTaggedElement(tag);
            var boundingBox = element?.get_BoundingBox(null);
            if (boundingBox == null) return null;

            XYZ translation = position - tag.TagHeadPosition;
            boundingBox.Min += translation;
            boundingBox.Max += translation;

            return boundingBox;
        }

        private bool DoBoxesOverlap(BoundingBoxXYZ box1, BoundingBoxXYZ box2)
        {
            if (box1 == null || box2 == null) return false;

            bool xOverlap = !(box1.Max.X <= box2.Min.X || box1.Min.X >= box2.Max.X);
            bool yOverlap = !(box1.Max.Y <= box2.Min.Y || box1.Min.Y >= box2.Max.Y);

            bool overlaps = xOverlap && yOverlap;
            if (overlaps)
            {
                Logger.LogDebug($"Çakışma detayları: X-Overlap: {xOverlap}, Y-Overlap: {yOverlap}");
                Logger.LogDebug($"Box1: Min({box1.Min.X},{box1.Min.Y}) Max({box1.Max.X},{box1.Max.Y})");
                Logger.LogDebug($"Box2: Min({box2.Min.X},{box2.Min.Y}) Max({box2.Max.X},{box2.Max.Y})");
            }

            return overlaps;
        }

        private bool HasOverlap(IndependentTag currentTag, XYZ newPosition, List<IndependentTag> existingTags)
        {
            var currentBox = GetTagBoundingBox(currentTag, newPosition);
            if (currentBox == null) return false;

            // Çakışma kontrolü için güvenlik marjı ekle
            double margin = UnitUtils.ConvertToInternalUnits(SAFETY_MARGIN, UnitTypeId.Millimeters);
            var expandedBox = new BoundingBoxXYZ
            {
                Min = new XYZ(currentBox.Min.X - margin, currentBox.Min.Y - margin, currentBox.Min.Z),
                Max = new XYZ(currentBox.Max.X + margin, currentBox.Max.Y + margin, currentBox.Max.Z)
            };

            foreach (var existingTag in existingTags)
            {
                var existingBox = GetTagBoundingBox(existingTag, existingTag.TagHeadPosition);
                if (existingBox == null) continue;

                if (DoBoxesOverlap(expandedBox, existingBox))
                {
                    Logger.LogDebug($"Çakışma tespit edildi: Mevcut etiket: {currentTag.Id}, Çakışan etiket: {existingTag.Id}");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Etiketin gerçek sınırlarını hesaplar
        /// </summary>
        private (double minX, double maxX, double minY, double maxY) GetTagBounds(IndependentTag tag)
        {
            var boundingBox = tag.get_BoundingBox(_doc.ActiveView);
            if (boundingBox == null) return (0, 0, 0, 0);

            // TagHeadPosition'a göre düzeltme yap
            var tagHead = tag.TagHeadPosition;
            var centerX = (boundingBox.Max.X + boundingBox.Min.X) / 2;
            var centerY = (boundingBox.Max.Y + boundingBox.Min.Y) / 2;

            // Merkez noktası ile TagHeadPosition arasındaki farkı hesapla
            var offsetX = tagHead.X - centerX;
            var offsetY = tagHead.Y - centerY;

            // Sınırları düzelt
            return (
                boundingBox.Min.X + offsetX,
                boundingBox.Max.X + offsetX,
                boundingBox.Min.Y + offsetY,
                boundingBox.Max.Y + offsetY
            );
        }

        /// <summary>
        /// Sıralanmış etiketleri belirtilen noktadan başlayarak konumlandırır
        /// </summary>
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

                        XYZ currentPosition = startPoint;
                        double previousEdge = 0.0; // Önceki etiketin kenarı (yatayda sağ, dikeyde alt)
                        bool isFirstTag = true;

                        foreach (var tag in sortedTags)
                        {
                            try
                            {
                                // Bounding box bilgisini al
                                var boundingBox = tag.get_BoundingBox(_doc.ActiveView);
                                if (boundingBox == null)
                                {
                                    Logger.LogWarning($"ID {tag.Id}: Bounding box alınamadı, atlanıyor.");
                                    continue;
                                }

                                if (direction == TagSortDirection.Horizontal)
                                {
                                    // Yatay offsetleri hesapla
                                    double offsetToLeft = tag.TagHeadPosition.X - boundingBox.Min.X;
                                    double offsetToRight = boundingBox.Max.X - tag.TagHeadPosition.X;

                                    if (isFirstTag)
                                    {
                                        tag.TagHeadPosition = currentPosition;
                                        previousEdge = currentPosition.X + offsetToRight;
                                        isFirstTag = false;
                                    }
                                    else
                                    {
                                        // Yeni etiketin sol kenarı = önceki etiketin sağ kenarı + boşluk
                                        double newTagLeft = previousEdge + spacingFeet;
                                        // TagHeadPosition.X = yeni sol kenar + sol offset
                                        double newTagHeadX = newTagLeft + offsetToLeft;
                                        currentPosition = new XYZ(newTagHeadX, currentPosition.Y, currentPosition.Z);
                                        tag.TagHeadPosition = currentPosition;

                                        // Bu etiketin sağ kenarını güncelle
                                        previousEdge = newTagHeadX + offsetToRight;
                                    }
                                }
                                else // Dikey sıralama
                                {
                                    // Dikey offsetleri hesapla
                                    double offsetToTop = boundingBox.Max.Y - tag.TagHeadPosition.Y;
                                    double offsetToBottom = tag.TagHeadPosition.Y - boundingBox.Min.Y;

                                    if (isFirstTag)
                                    {
                                        tag.TagHeadPosition = currentPosition;
                                        previousEdge = currentPosition.Y - offsetToBottom;
                                        isFirstTag = false;
                                    }
                                    else
                                    {
                                        // Yeni etiketin üst kenarı = önceki etiketin alt kenarı - boşluk
                                        double newTagTop = previousEdge - spacingFeet;
                                        // TagHeadPosition.Y = yeni üst kenar - üst offset
                                        double newTagHeadY = newTagTop - offsetToTop;
                                        currentPosition = new XYZ(currentPosition.X, newTagHeadY, currentPosition.Z);
                                        tag.TagHeadPosition = currentPosition;

                                        // Bu etiketin alt kenarını güncelle
                                        previousEdge = newTagHeadY - offsetToBottom;
                                    }
                                }

                                Logger.LogSuccess($"ID {tag.Id}: Yeni konum X={currentPosition.X:F3}, Y={currentPosition.Y:F3}");

                                // Leader ayarlarını güncelle
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
