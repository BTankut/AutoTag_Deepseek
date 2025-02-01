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

        // Yatay düzenleme için mesafe
        private const double HORIZONTAL_SPACING_MM = 80.0;  
        private const double HORIZONTAL_MARGIN_MM = 20.0;   

        // Dikey düzenleme için mesafe
        private const double VERTICAL_SPACING_MM = 150.0;   
        private const double VERTICAL_MARGIN_MM = 30.0;     
        private const double VERTICAL_SPACING_MULTIPLIER = 3.0;  // Dikey aralık çarpanı

        // Birim dönüşümü
        private const double MM_TO_FEET = 304.8;     

        public AutoSortWithManualPlacement(Document doc, UIDocument uiDoc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _uiDoc = uiDoc ?? throw new ArgumentNullException(nameof(uiDoc));
        }

        /// <summary>
        /// Etiketleri yatay veya dikey olarak yerleştirir.
        /// </summary>
        public bool PlaceSortedTags(List<IndependentTag> sortedTags, XYZ startPoint, TagSortDirection direction)
        {
            if (sortedTags == null || !sortedTags.Any())
            {
                Logger.LogError("Etiket listesi boş veya null");
                return false;
            }

            if (startPoint == null)
            {
                Logger.LogError("Başlangıç noktası null");
                return false;
            }

            using (Transaction trans = new Transaction(_doc, "Etiketleri Konumlandır"))
            {
                try
                {
                    trans.Start();
                    Logger.LogInfo($"Etiketler {direction} yönünde yerleştiriliyor...");

                    if (direction == TagSortDirection.Horizontal)
                    {
                        double horizontalSpacingFeet = (HORIZONTAL_SPACING_MM + HORIZONTAL_MARGIN_MM) / MM_TO_FEET;
                        PlaceTagsHorizontally(sortedTags, startPoint, horizontalSpacingFeet);
                    }
                    else
                    {
                        double verticalSpacingFeet = (VERTICAL_SPACING_MM + VERTICAL_MARGIN_MM) / MM_TO_FEET;
                        PlaceTagsVertically(sortedTags, startPoint, verticalSpacingFeet);
                    }

                    trans.Commit();
                    Logger.LogInfo("Etiketler başarıyla yerleştirildi");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Etiketler yerleştirilirken hata oluştu", ex);
                    if (trans.HasStarted())
                        trans.RollBack();
                    return false;
                }
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
            double baselineX = startPoint.X;
            double currentY = startPoint.Y;

            foreach (var tag in tags)
            {
                var (minX, maxX, minY, maxY) = GetTagBounds(tag);

                // Etiketin mevcut konumu ile hedef konum arasındaki farkı hesapla
                double deltaX = baselineX - tag.TagHeadPosition.X;
                double deltaY = currentY - tag.TagHeadPosition.Y;

                // Etiketi yeni konumuna taşı
                tag.TagHeadPosition = new XYZ(
                    tag.TagHeadPosition.X + deltaX,
                    tag.TagHeadPosition.Y + deltaY,
                    startPoint.Z
                );

                // Bir sonraki etiket için Y konumunu güncelle
                currentY -= spacingFeet * VERTICAL_SPACING_MULTIPLIER;
                UpdateTagLeader(tag);
            }
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
