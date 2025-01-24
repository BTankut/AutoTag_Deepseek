using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace TagsOrderingPlugin
{
    /// <summary>
    /// Etiket sıralama yönünü belirleyen enum
    /// </summary>
    public enum TagSortDirection
    {
        /// <summary>
        /// Yatay sıralama (soldan sağa)
        /// </summary>
        Horizontal,

        /// <summary>
        /// Dikey sıralama (yukarıdan aşağıya)
        /// </summary>
        Vertical
    }

    /// <summary>
    /// Etiketleri koordinatlarına göre sıralayan sınıf
    /// </summary>
    public class TagSorter
    {
        private readonly Document _doc;

        /// <summary>
        /// TagSorter sınıfının yapıcı metodu
        /// </summary>
        /// <param name="doc">Aktif Revit dökümanı</param>
        public TagSorter(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        /// <summary>
        /// Etiketleri seçilen yöne göre sıralar
        /// </summary>
        /// <param name="tagIds">Sıralanacak etiket ID'leri</param>
        /// <param name="direction">Sıralama yönü</param>
        /// <param name="startPoint">Başlangıç noktası (dikey sıralama için gerekli)</param>
        /// <returns>Sıralanmış etiket listesi</returns>
        public List<IndependentTag> SortByCoordinates(List<ElementId> tagIds, TagSortDirection direction, XYZ startPoint)
        {
            try
            {
                if (tagIds == null || !tagIds.Any())
                    return new List<IndependentTag>();

                // Etiketleri filtreleme
                var validTags = tagIds
                    .Select(id => _doc.GetElement(id) as IndependentTag)
                    .Where(tag => tag != null && tag.IsValidObject)
                    .ToList();

                List<IndependentTag> sortedTags;

                if (direction == TagSortDirection.Horizontal)
                {
                    // Elementleri soldan sağa sırala
                    var tagsWithElements = validTags.Select(tag =>
                    {
                        var element = _doc.GetElement(tag.GetTaggedLocalElementIds().First());
                        var elementLocation = (element?.Location as LocationPoint)?.Point;
                        
                        return new { 
                            Tag = tag,
                            ElementX = elementLocation?.X ?? tag.TagHeadPosition.X
                        };
                    });

                    // Her zaman soldan sağa sırala
                    sortedTags = tagsWithElements
                        .OrderBy(t => t.ElementX)  // Elementlerin X koordinatına göre sırala
                        .Select(t => t.Tag)
                        .ToList();

                    Logger.LogInfo("Yatay sıralama yapıldı (Elementler soldan sağa)");
                }
                else
                {
                    // Dikey sıralama (yukarıdan aşağıya)
                    var tagsWithLocations = validTags.Select(tag =>
                    {
                        var element = _doc.GetElement(tag.GetTaggedLocalElementIds().First());
                        var elementLocation = (element?.Location as LocationPoint)?.Point;
                        var location = elementLocation ?? tag.TagHeadPosition;
                        
                        return new { 
                            Tag = tag,
                            Location = location,
                            Distance = Math.Abs(location.X - startPoint.X)
                        };
                    }).ToList();

                    // Başlangıç noktasına göre hangi tarafta olduğumuzu belirle
                    bool isLeftSide = tagsWithLocations.Average(t => t.Location.X) < startPoint.X;
                    Logger.LogInfo($"Etiketler {(isLeftSide ? "sol" : "sağ")} tarafta");

                    // Sıralama yap
                    sortedTags = tagsWithLocations
                        .OrderByDescending(t => t.Distance) // En uzak elementler üstte
                        .ThenByDescending(t => t.Location.Y) // Yukarıdan aşağıya
                        .Select(t => t.Tag)
                        .ToList();

                    Logger.LogInfo($"Dikey sıralama yapıldı (Uzaklık ve Y koordinatına göre)");
                }

                Logger.LogDebug($"Sıralanan etiket sayısı: {sortedTags.Count}, Yön: {direction}");
                return sortedTags;
            }
            catch (Exception ex)
            {
                Logger.LogError("Etiketler sıralanamadı", ex);
                return new List<IndependentTag>();
            }
        }
    }
}
