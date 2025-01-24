using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace TagsOrderingPlugin
{
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
        /// Etiketleri X ve Y koordinatlarına göre sıralar
        /// </summary>
        /// <param name="tagIds">Sıralanacak etiket ID'leri</param>
        /// <returns>Sıralanmış etiket listesi</returns>
        public List<IndependentTag> SortByCoordinates(List<ElementId> tagIds)
        {
            try
            {
                if (tagIds == null || !tagIds.Any())
                    return new List<IndependentTag>();

                // Etiketleri X ve Y koordinatlarına göre sırala
                var sortedTags = tagIds
                    .Select(id => _doc.GetElement(id) as IndependentTag)
                    .Where(tag => tag != null && tag.IsValidObject)
                    .OrderBy(tag => tag.TagHeadPosition.X)
                    .ThenBy(tag => tag.TagHeadPosition.Y)
                    .ToList();

                Logger.LogDebug($"Sıralanan etiket sayısı: {sortedTags.Count}");
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
