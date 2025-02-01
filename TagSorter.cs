using System;
using System.Collections.Generic;
using System.Linq; // LINQ yöntemlerini kullanabilmek için eklendi.
using Autodesk.Revit.DB;

namespace TagsOrderingPlugin
{
    /// <summary>
    /// Etiketleri koordinatlarına göre sıralayan sınıf.
    /// </summary>
    public class TagSorter
    {
        private readonly Document _doc;

        /// <summary>
        /// TagSorter sınıfının yapıcı metodu.
        /// </summary>
        /// <param name="doc">Aktif Revit dökümanı.</param>
        public TagSorter(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        /// <summary>
        /// Etiketleri belirtilen yönde sıralar.
        /// </summary>
        /// <param name="tagIds">Sıralanacak etiket element ID'leri.</param>
        /// <param name="direction">Sıralama yönü.</param>
        /// <param name="startPoint">Başlangıç noktası (dikey sıralama için gereklidir).</param>
        /// <returns>Sıralanmış etiket listesi.</returns>
        public List<IndependentTag> SortByCoordinates(List<ElementId> tagIds, TagSortDirection direction, XYZ startPoint)
        {
            try
            {
                if (tagIds == null || !tagIds.Any()) // LINQ yöntemi
                    return new List<IndependentTag>();

                // Geçerli etiketleri filtrele.
                var validTags = tagIds
                    .Select(id => _doc.GetElement(id) as IndependentTag)
                    .Where(tag => tag != null && tag.IsValidObject) // LINQ yöntemi
                    .ToList();

                List<IndependentTag> sortedTags;

                if (direction == TagSortDirection.Horizontal)
                {
                    // Etiketleri soldan sağa sırala.
                    var tagsWithElements = validTags.Select(tag =>
                    {
                        var element = _doc.GetElement(tag.GetTaggedLocalElementIds().First()); // LINQ yöntemi
                        var elementLocation = (element?.Location as LocationPoint)?.Point;

                        return new
                        {
                            Tag = tag,
                            ElementX = elementLocation?.X ?? tag.TagHeadPosition.X
                        };
                    });

                    // Soldan sağa sıralama.
                    sortedTags = tagsWithElements
                        .OrderBy(t => t.ElementX) // LINQ yöntemi
                        .Select(t => t.Tag)
                        .ToList();

                    Logger.LogInfo("Yatay sıralama tamamlandı (elementler soldan sağa sıralandı).");
                }
                else
                {
                    // Dikey sıralama (yukarıdan aşağıya).
                    var tagsWithLocations = validTags.Select(tag =>
                    {
                        var element = _doc.GetElement(tag.GetTaggedLocalElementIds().First()); // LINQ yöntemi
                        var elementLocation = (element?.Location as LocationPoint)?.Point;
                        var location = elementLocation ?? tag.TagHeadPosition;

                        return new
                        {
                            Tag = tag,
                            Location = location,
                            Distance = Math.Abs(location.X - startPoint.X)
                        };
                    }).ToList();

                    // Başlangıç noktasına göre etiketlerin hangi tarafta olduğunu belirle.
                    bool isLeftSide = tagsWithLocations.Average(t => t.Location.X) < startPoint.X;
                    Logger.LogInfo($"Etiketler {(isLeftSide ? "sol" : "sağ")} tarafta.");

                    // Etiketleri önce mesafeye (azalan), sonra Y koordinatına (azalan) göre sırala.
                    sortedTags = tagsWithLocations
                        .OrderByDescending(t => t.Distance) // LINQ yöntemi
                        .ThenByDescending(t => t.Location.Y) // LINQ yöntemi
                        .Select(t => t.Tag)
                        .ToList();

                    Logger.LogInfo("Dikey sıralama tamamlandı (mesafe ve Y koordinatına göre sıralandı).");
                }

                Logger.LogDebug($"Toplam sıralanan etiket sayısı: {sortedTags.Count}, Yön: {direction}");
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
