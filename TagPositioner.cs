using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace TagsOrderingPlugin
{
    /// <summary>
    /// Etiketleri konumlandıran sınıf
    /// </summary>
    public class TagPositioner
    {
        private readonly Document _doc;
        private const double OFFSET_MM = 2.0;
        private const double MM_TO_FEET = 0.00328084;
        private readonly double _offset;

        /// <summary>
        /// TagPositioner sınıfının yapıcı metodu
        /// </summary>
        /// <param name="doc">Aktif Revit dökümanı</param>
        public TagPositioner(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _offset = OFFSET_MM * MM_TO_FEET; // mm'yi feet'e çevir
        }

        /// <summary>
        /// Etiketleri yeni konumlarına taşır
        /// </summary>
        /// <param name="sortedTags">Sıralanmış etiket listesi</param>
        /// <returns>Başarı durumu</returns>
        public bool PositionTags(List<IndependentTag> sortedTags)
        {
            try
            {
                if (!sortedTags.Any()) return false;

                double currentX = sortedTags.First().TagHeadPosition.X;
                
                foreach (var tag in sortedTags)
                {
                    // Yeni konum hesapla
                    XYZ newPosition = CalculateNewPosition(tag, currentX);
                    
                    // Çakışma kontrolü
                    while (HasOverlap(tag, newPosition, sortedTags))
                    {
                        newPosition = new XYZ(newPosition.X + _offset, newPosition.Y, newPosition.Z);
                    }

                    // Etiketi taşı
                    MoveTag(tag, newPosition);

                    // X pozisyonunu güncelle
                    currentX = newPosition.X + _offset;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Etiket için yeni konum hesaplar
        /// </summary>
        private XYZ CalculateNewPosition(IndependentTag tag, double targetX)
        {
            // Host element tipine göre konum hesapla
            var taggedIds = tag.GetTaggedLocalElementIds();
            if (!taggedIds.Any()) return new XYZ(targetX, tag.TagHeadPosition.Y, tag.TagHeadPosition.Z);

            Element host = _doc.GetElement(taggedIds.First());
            if (host is Wall || host is Floor)
            {
                // Duvar ve zemin etiketleri için Y pozisyonunu koru
                return new XYZ(targetX, tag.TagHeadPosition.Y, tag.TagHeadPosition.Z);
            }

            return new XYZ(targetX, tag.TagHeadPosition.Y, tag.TagHeadPosition.Z);
        }

        /// <summary>
        /// Etiketi yeni konumuna taşır
        /// </summary>
        private void MoveTag(IndependentTag tag, XYZ newPosition)
        {
            XYZ translation = newPosition - tag.TagHeadPosition;
            ElementTransformUtils.MoveElement(_doc, tag.Id, translation);
        }

        /// <summary>
        /// Etiketin diğer etiketlerle çakışıp çakışmadığını kontrol eder
        /// </summary>
        private bool HasOverlap(IndependentTag currentTag, XYZ newPosition, List<IndependentTag> allTags)
        {
            // Mevcut etiketin sınırlayıcı kutusunu al
            BoundingBoxXYZ currentBox = currentTag.get_BoundingBox(null);
            if (currentBox == null) return false;

            // Yeni konuma göre sınırlayıcı kutuyu güncelle
            XYZ translation = newPosition - currentTag.TagHeadPosition;
            currentBox.Min += translation;
            currentBox.Max += translation;

            // Diğer etiketlerle çakışma kontrolü
            foreach (var otherTag in allTags)
            {
                if (otherTag.Id.Equals(currentTag.Id)) continue;

                BoundingBoxXYZ otherBox = otherTag.get_BoundingBox(null);
                if (otherBox == null) continue;

                // Sınırlayıcı kutular çakışıyor mu kontrol et
                if (DoBoxesOverlap(currentBox, otherBox))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// İki sınırlayıcı kutunun çakışıp çakışmadığını kontrol eder
        /// </summary>
        private bool DoBoxesOverlap(BoundingBoxXYZ box1, BoundingBoxXYZ box2)
        {
            return !(box1.Max.X < box2.Min.X || box1.Min.X > box2.Max.X ||
                    box1.Max.Y < box2.Min.Y || box1.Min.Y > box2.Max.Y);
        }
    }
}
