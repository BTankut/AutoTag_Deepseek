using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace TagsOrderingPlugin
{
    /// <summary>
    /// Etiket sıralama işlemlerini yöneten sınıf
    /// </summary>
    public class TagSorter
    {
        private readonly Document _doc;

        // Loglama mesajları
        private const string LOG_VERTICAL_SORT = "Dikey sıralama tamamlandı (mesafe ve Y koordinatına göre sıralandı).";
        private const string LOG_HORIZONTAL_SORT = "Yatay sıralama tamamlandı (mesafe ve X koordinatına göre sıralandı).";
        private const string LOG_SORT_SUMMARY = "Toplam sıralanan etiket sayısı: {0}, Yön: {1}";

        /// <summary>
        /// TagSorter sınıfının yapıcı metodu.
        /// </summary>
        public TagSorter(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Etiketleri belirtilen yönde sıralar.
        /// </summary>
        public List<IndependentTag> SortByCoordinates(List<ElementId> tagIds, string direction, XYZ startPoint)
        {
            try
            {
                if (tagIds == null || !tagIds.Any())
                {
                    Logger.LogWarning("Sıralanacak etiket bulunamadı.");
                    return new List<IndependentTag>();
                }

                if (startPoint == null)
                {
                    Logger.LogError("Başlangıç noktası null olamaz.");
                    return new List<IndependentTag>();
                }

                var validTags = GetValidTags(tagIds);
                if (!validTags.Any())
                {
                    Logger.LogWarning("Geçerli etiket bulunamadı.");
                    return new List<IndependentTag>();
                }

                var sortedTags = direction == "Horizontal"
                    ? SortHorizontally(validTags)
                    : SortVertically(validTags, startPoint, DetermineTagPlacementDirection(validTags, startPoint));

                Logger.LogDebug(string.Format(LOG_SORT_SUMMARY, sortedTags.Count, direction));
                return sortedTags;
            }
            catch (Exception ex)
            {
                Logger.LogError("Etiketler sıralanamadı", ex);
                return new List<IndependentTag>();
            }
        }

        /// <summary>
        /// Verilen ID'lerden geçerli etiketleri filtreler.
        /// </summary>
        private List<IndependentTag> GetValidTags(List<ElementId> tagIds)
        {
            return tagIds
                .Select(id => _doc.GetElement(id) as IndependentTag)
                .Where(tag => tag != null && tag.IsValidObject)
                .ToList();
        }

        /// <summary>
        /// Etiketleri yatay olarak sıralar (soldan sağa).
        /// </summary>
        private List<IndependentTag> SortHorizontally(List<IndependentTag> tags)
        {
            var tagsWithElements = tags.Select(tag =>
            {
                var element = GetTaggedElement(tag);
                var elementLocation = GetElementLocation(element);

                return new
                {
                    Tag = tag,
                    ElementX = elementLocation?.X ?? tag.TagHeadPosition.X
                };
            });

            var sortedTags = tagsWithElements
                .OrderBy(t => t.ElementX)
                .Select(t => t.Tag)
                .ToList();

            Logger.LogInfo(LOG_HORIZONTAL_SORT);
            return sortedTags;
        }

        /// <summary>
        /// Etiketlerin yerleşim yönünü belirler
        /// </summary>
        public static string DetermineTagPlacementDirection(List<IndependentTag> tags, XYZ startPoint)
        {
            double avgTagY = tags.Average(t => t.TagHeadPosition.Y);
            Logger.LogInfo($"Ortalama Tag Y: {avgTagY}, Başlangıç Y: {startPoint.Y}");

            // Etiketler ve başlangıç noktası aynı bölgede mi kontrol et
            bool tagsAreBelow = avgTagY < 0;
            bool startIsBelow = startPoint.Y < 0;

            // Eğer etiketler ve başlangıç noktası aynı bölgedeyse
            if (tagsAreBelow == startIsBelow)
            {
                return tagsAreBelow ? "BottomToTop" : "TopToBottom";
            }
            // Farklı bölgelerdeyse, etiketlerin bulunduğu bölgeye göre karar ver
            else
            {
                return tagsAreBelow ? "BottomToTop" : "TopToBottom";
            }
        }

        /// <summary>
        /// Etiketleri dikey olarak sıralar.
        /// </summary>
        private List<IndependentTag> SortVertically(List<IndependentTag> tags, XYZ startPoint, string placementDirection)
        {
            // Her etiket için başlangıç noktasına göre bölge tespiti yap
            var tagsWithRegion = tags.Select(tag => new
            {
                Tag = tag,
                IsAbove = tag.TagHeadPosition.Y > startPoint.Y  // Başlangıç noktasının üstünde mi?
            }).ToList();

            // Kaç etiket üstte kaç etiket altta, logla
            int aboveCount = tagsWithRegion.Count(t => t.IsAbove);
            int belowCount = tagsWithRegion.Count - aboveCount;
            Logger.LogInfo($"Üst bölgede {aboveCount} etiket, alt bölgede {belowCount} etiket var");
            Logger.LogInfo($"Başlangıç noktası Y: {startPoint.Y}");

            // Etiketleri X koordinatına göre grupla
            var groupedByX = tagsWithRegion
                .GroupBy(t => Math.Round(t.Tag.TagHeadPosition.X, 3))
                .OrderBy(g => Math.Abs(g.Key - startPoint.X));

            var result = new List<IndependentTag>();

            // Her X grubu için
            foreach (var group in groupedByX)
            {
                var sortedTags = group.ToList();

                // Y koordinatına göre sırala
                if (sortedTags.First().IsAbove)
                {
                    // Üst bölgede yukarıdan aşağıya sırala (yerleştirme aşağıdan yukarı olacak)
                    sortedTags.Sort((a, b) => b.Tag.TagHeadPosition.Y.CompareTo(a.Tag.TagHeadPosition.Y));
                    Logger.LogInfo($"Üst bölge etiketleri yukarıdan aşağıya sıralanıyor (yerleştirme aşağıdan yukarı olacak)");
                }
                else
                {
                    // Alt bölgede yukarıdan aşağıya sırala (yerleştirme yukarıdan aşağı olacak)
                    sortedTags.Sort((a, b) => b.Tag.TagHeadPosition.Y.CompareTo(a.Tag.TagHeadPosition.Y));
                    Logger.LogInfo($"Alt bölge etiketleri yukarıdan aşağıya sıralanıyor (yerleştirme yukarıdan aşağı olacak)");
                }

                result.AddRange(sortedTags.Select(t => t.Tag));
            }

            Logger.LogInfo(LOG_VERTICAL_SORT);
            return result;
        }

        /// <summary>
        /// Etiketin bağlı olduğu elementi getirir.
        /// </summary>
        private Element GetTaggedElement(IndependentTag tag)
        {
            try
            {
                var elementId = tag.GetTaggedLocalElementIds().FirstOrDefault();
                return elementId != null ? _doc.GetElement(elementId) : null;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Tag {tag.Id.IntegerValue} için element alınamadı", ex);
                return null;
            }
        }

        /// <summary>
        /// Elementin lokasyon noktasını getirir.
        /// </summary>
        private XYZ GetElementLocation(Element element)
        {
            if (element?.Location is LocationPoint locPoint)
            {
                return locPoint.Point;
            }
            return null;
        }
    }
}
