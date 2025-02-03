using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace TagsOrderingPlugin
{
    public class AutoSortWithManualPlacement
    {
        private readonly Document _doc;
        private readonly UIDocument _uiDoc;

        // Yatay düzenleme sabitleri
        private const double HORIZONTAL_SPACING_MM = 80;
        private const double HORIZONTAL_MARGIN_MM = 20;
        private const double HORIZONTAL_SPACING_MULTIPLIER = 1.0;

        // Dikey düzenleme sabitleri
        private const double VERTICAL_SPACING_MM = 150;
        private const double VERTICAL_MARGIN_MM = 30;
        private const double VERTICAL_SPACING_MULTIPLIER = 3.0;

        // Birim dönüşümü
        private const double MM_TO_FEET = 304.8;     

        // Spacing hesaplamaları
        private const double HORIZONTAL_SPACING_FEET = (HORIZONTAL_SPACING_MM + HORIZONTAL_MARGIN_MM) / MM_TO_FEET;
        private const double VERTICAL_SPACING_FEET = ((VERTICAL_SPACING_MM + VERTICAL_MARGIN_MM) / MM_TO_FEET) * VERTICAL_SPACING_MULTIPLIER;

        public AutoSortWithManualPlacement(Document doc, UIDocument uiDoc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _uiDoc = uiDoc ?? throw new ArgumentNullException(nameof(uiDoc));
        }

        /// <summary>
        /// Sıralanmış etiketleri yerleştirir
        /// </summary>
        public bool PlaceSortedTags(List<IndependentTag> tags, XYZ startPoint, string direction)
        {
            try
            {
                using (Transaction trans = new Transaction(_doc, "Etiketleri Yerleştir"))
                {
                    trans.Start();

                    if (direction == "Horizontal")
                    {
                        PlaceTagsHorizontally(tags, startPoint, HORIZONTAL_SPACING_FEET);
                    }
                    else
                    {
                        PlaceTagsVertically(tags, startPoint, VERTICAL_SPACING_FEET);
                    }

                    trans.Commit();
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Etiketleri yerleştirme sırasında hata", ex);
                return false;
            }
        }

        /// <summary>
        /// Etiketleri yatay olarak yerleştirir.
        /// </summary>
        private void PlaceTagsHorizontally(List<IndependentTag> tags, XYZ startPoint, double spacingFeet)
        {
            double currentX = startPoint.X;
            double baselineY = startPoint.Y;

            foreach (var tag in tags)
            {
                var (minX, maxX, minY, maxY) = GetTagBounds(tag);
                double width = maxX - minX;

                // Etiketin mevcut konumu ile hedef konum arasındaki farkı hesapla
                double deltaX = currentX - tag.TagHeadPosition.X;
                double deltaY = baselineY - tag.TagHeadPosition.Y;

                // Etiketi yeni konumuna taşı
                tag.TagHeadPosition = new XYZ(
                    tag.TagHeadPosition.X + deltaX,
                    tag.TagHeadPosition.Y + deltaY,
                    startPoint.Z
                );

                // Bir sonraki etiket için X konumunu güncelle
                currentX += width + spacingFeet;
                UpdateTagLeader(tag);
            }
        }

        /// <summary>
        /// Etiketleri dikey olarak yerleştirir.
        /// </summary>
        private void PlaceTagsVertically(List<IndependentTag> tags, XYZ startPoint, double spacingFeet)
        {
            // Etiketlerin ortalama Y koordinatını hesapla
            double avgTagY = tags.Average(t => t.TagHeadPosition.Y);
            bool tagsAreBelow = avgTagY < startPoint.Y;

            Logger.LogInfo($"Etiketler {(tagsAreBelow ? "alt (-y)" : "üst (+y)")} bölgede");
            Logger.LogInfo($"Başlangıç noktası Y: {startPoint.Y}");
            Logger.LogInfo($"Dikey spacing: {spacingFeet} feet");

            // İlk etiketin Y koordinatını belirle
            double currentY = startPoint.Y;

            // Etiketleri yerleştir
            foreach (var tag in tags)
            {
                Logger.LogInfo($"Etiket yerleştiriliyor - Mevcut Y: {currentY}");

                // Etiketi yeni konumuna taşı
                tag.TagHeadPosition = new XYZ(
                    startPoint.X,           // X koordinatı başlangıç noktasından
                    currentY,               // Hesaplanan Y koordinatı
                    tag.TagHeadPosition.Z   // Z koordinatı değişmiyor
                );

                // Bir sonraki etiket için Y konumunu güncelle
                if (tagsAreBelow)
                {
                    // Alt bölgede (-y) aşağıdan yukarıya git
                    currentY += spacingFeet;
                    Logger.LogInfo($"Alt bölge - Sonraki Y: {currentY} (yukarı)");
                }
                else
                {
                    // Üst bölgede (+y) yukarıdan aşağıya git
                    currentY -= spacingFeet;
                    Logger.LogInfo($"Üst bölge - Sonraki Y: {currentY} (aşağı)");
                }

                // Leader'ı güncelle
                UpdateTagLeader(tag);
            }

            Logger.LogInfo("Etiketler başarıyla yerleştirildi");
        }

        /// <summary>
        /// Etiketin bounding box boyutlarını alır.
        /// </summary>
        private (double minX, double maxX, double minY, double maxY) GetTagBounds(IndependentTag tag)
        {
            var boundingBox = tag.get_BoundingBox(_doc.ActiveView);
            if (boundingBox == null)
            {
                Logger.LogWarning($"Tag {tag.Id.IntegerValue} için bounding box alınamadı");
                return (0, 0, 0, 0);
            }

            return (
                boundingBox.Min.X,
                boundingBox.Max.X,
                boundingBox.Min.Y,
                boundingBox.Max.Y
            );
        }

        /// <summary>
        /// Etiketin lider çizgisini yeniden bağlar.
        /// </summary>
        private void UpdateTagLeader(IndependentTag tag)
        {
            try
            {
                if (tag.HasLeader && tag.GetTaggedLocalElementIds().Any())
                {
                    var elementId = tag.GetTaggedLocalElementIds().First();
                    var element = _doc.GetElement(elementId);

                    if (element != null && element.IsValidObject)
                    {
                        var reference = tag.GetTaggedReferences().FirstOrDefault();
                        if (reference != null && element.Location is LocationPoint locPoint)
                        {
                            tag.LeaderEndCondition = LeaderEndCondition.Attached;
                            tag.SetLeaderEnd(reference, locPoint.Point);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Tag {tag.Id.IntegerValue} için leader güncellenemedi", ex);
            }
        }

        /// <summary>
        /// Kullanıcıdan başlangıç noktası seçmesini ister.
        /// </summary>
        public XYZ GetStartPoint()
        {
            try
            {
                Logger.LogInfo("Kullanıcıdan başlangıç noktası seçimi bekleniyor...");
                return _uiDoc.Selection.PickPoint("Etiketlerin başlayacağı noktayı seçin");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                Logger.LogInfo("Kullanıcı nokta seçimini iptal etti");
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError("Nokta seçimi sırasında hata oluştu", ex);
                return null;
            }
        }
    }
}
