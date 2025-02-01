#define USE_BOUNDING_BOX

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

        // Dikey düzenleme için mesafe (daha büyük değerler)
        private const double VERTICAL_SPACING_MM = 150.0;   
        private const double VERTICAL_MARGIN_MM = 30.0;     

        // Birim dönüşümü
        private const double MM_TO_FEET = 304.8;     

        public AutoSortWithManualPlacement(Document doc, UIDocument uiDoc)
        {
            _doc = doc;
            _uiDoc = uiDoc;
        }

        /// <summary>
        /// Etiketleri yatay veya dikey olarak yerleştirir.
        /// </summary>
        public bool PlaceSortedTags(List<IndependentTag> sortedTags, XYZ startPoint, TagSortDirection direction)
        {
            if (sortedTags == null || !sortedTags.Any() || startPoint == null)
                return false;

            using (Transaction trans = new Transaction(_doc, "Etiketleri Konumlandır"))
            {
                try
                {
                    trans.Start();

                    if (direction == TagSortDirection.Horizontal)
                    {
                        // Yatay mesafe hesaplama
                        double horizontalSpacingFeet = (HORIZONTAL_SPACING_MM + HORIZONTAL_MARGIN_MM) / MM_TO_FEET;
                        PlaceTagsHorizontally(sortedTags, startPoint, horizontalSpacingFeet);
                    }
                    else
                    {
                        // Dikey mesafe hesaplama
                        double verticalSpacingFeet = (VERTICAL_SPACING_MM + VERTICAL_MARGIN_MM) / MM_TO_FEET;
                        PlaceTagsVertically(sortedTags, startPoint, verticalSpacingFeet);
                    }

                    trans.Commit();
                    return true;
                }
                catch (Exception)
                {
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
                currentY -= spacingFeet * 3; 
                UpdateTagLeader(tag);
            }
        }

        /// <summary>
        /// Etiketin bounding box boyutlarını alır.
        /// </summary>
        private (double minX, double maxX, double minY, double maxY) GetTagBounds(IndependentTag tag)
        {
            var boundingBox = tag.get_BoundingBox(_doc.ActiveView);
            if (boundingBox == null) return (0, 0, 0, 0);

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

        /// <summary>
        /// Kullanıcıdan başlangıç noktası seçmesini ister.
        /// </summary>
        /// <returns>Seçilen nokta veya null.</returns>
        public XYZ GetStartPoint()
        {
            try
            {
                return _uiDoc.Selection.PickPoint("Etiketlerin başlayacağı noktayı seçin");
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
