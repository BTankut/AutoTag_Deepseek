using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace TagsOrderingPlugin
{
    /// <summary>
    /// Etiketleri koordinatlarına göre sıralayan sınıf.
    /// </summary>
    public class TagSorter
    {
        private readonly Document _doc;

        // Loglama mesajları
        private const string LOG_HORIZONTAL_SORT = "Yatay sıralama tamamlandı (elementler soldan sağa sıralandı).";
        private const string LOG_VERTICAL_SORT = "Dikey sıralama tamamlandı (mesafe ve Y koordinatına göre sıralandı).";
        private const string LOG_SIDE_INFO = "Etiketler {0} tarafta.";
        private const string LOG_SORT_SUMMARY = "Toplam sıralanan etiket sayısı: {0}, Yön: {1}";

        /// <summary>
        /// TagSorter sınıfının yapıcı metodu.
        /// </summary>
        public TagSorter(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        /// <summary>
        /// Etiketleri belirtilen yönde sıralar.
        /// </summary>
        public List<IndependentTag> SortByCoordinates(List<ElementId> tagIds, TagSortDirection direction, XYZ startPoint)
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

                var sortedTags = direction == TagSortDirection.Horizontal
                    ? SortHorizontally(validTags)
                    : SortVertically(validTags, startPoint);

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
        /// Etiketleri dikey olarak sıralar (yukarıdan aşağıya).
        /// </summary>
        private List<IndependentTag> SortVertically(List<IndependentTag> tags, XYZ startPoint)
        {
            var tagsWithLocations = tags.Select(tag =>
            {
                var element = GetTaggedElement(tag);
                var elementLocation = GetElementLocation(element);
                var location = elementLocation ?? tag.TagHeadPosition;

                return new
                {
                    Tag = tag,
                    Location = location,
                    Distance = Math.Abs(location.X - startPoint.X)
                };
            }).ToList();

            bool isLeftSide = tagsWithLocations.Average(t => t.Location.X) < startPoint.X;
            Logger.LogInfo(string.Format(LOG_SIDE_INFO, isLeftSide ? "sol" : "sağ"));

            var sortedTags = tagsWithLocations
                .OrderByDescending(t => t.Distance)
                .ThenByDescending(t => t.Location.Y)
                .Select(t => t.Tag)
                .ToList();

            Logger.LogInfo(LOG_VERTICAL_SORT);
            return sortedTags;
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
