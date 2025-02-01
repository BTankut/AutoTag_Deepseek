using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace TagsOrderingPlugin
{
    /// <summary>
    /// IndependentTag filtresi için ISelectionFilter implementasyonu
    /// </summary>
    public class TagFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is IndependentTag;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    /// <summary>
    /// Etiket seçim işlemlerini yöneten sınıf
    /// </summary>
    public class TagSelection
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;

        /// <summary>
        /// TagSelection sınıfının yapıcı metodu
        /// </summary>
        /// <param name="uiDoc">Aktif UI döküman</param>
        public TagSelection(UIDocument uiDoc)
        {
            _uiDoc = uiDoc ?? throw new ArgumentNullException(nameof(uiDoc));
            _doc = uiDoc.Document;
        }

        /// <summary>
        /// Kullanıcının birden fazla etiket seçmesini sağlar
        /// </summary>
        /// <returns>Seçilen etiketlerin ElementId listesi</returns>
        public List<ElementId> GetSelectedTags()
        {
            try
            {
                Logger.LogInfo("GetSelectedTags metodu başlatıldı");
                
                // IndependentTag filtresi oluştur
                ISelectionFilter tagFilter = new TagFilter();
                Logger.LogInfo("Tag filtresi oluşturuldu");

                // Kullanıcıya seçim yaptır
                Logger.LogInfo("Kullanıcı seçimi bekleniyor...");
                IList<Reference> selectedRefs = _uiDoc.Selection.PickObjects(
                    ObjectType.Element,
                    tagFilter,
                    "Düzenlenecek etiketleri seçin");

                Logger.LogInfo($"Seçilen referans sayısı: {selectedRefs?.Count ?? 0}");

                // Seçilen referansları ElementId'ye dönüştür
                var tagIds = selectedRefs?.Select(r => r.ElementId).ToList();
                Logger.LogInfo($"Dönüştürülen ElementId sayısı: {tagIds?.Count ?? 0}");

                // Seçim validasyonu
                if (!ValidateSelection(tagIds))
                {
                    Logger.LogInfo("Seçim validasyonu başarısız");
                    TaskDialog.Show("Hata", "Seçilen elementlerden bazıları geçerli etiket değil.");
                    return null;
                }

                Logger.LogInfo($"Seçim validasyonu başarılı. Toplam {tagIds.Count} etiket seçildi.");
                return tagIds;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                Logger.LogInfo("Kullanıcı seçimi iptal etti");
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError("GetSelectedTags metodunda beklenmeyen hata", ex);
                TaskDialog.Show("Hata", $"Etiket seçimi sırasında hata oluştu: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Seçilen elementlerin geçerli etiketler olduğunu kontrol eder
        /// </summary>
        /// <param name="elementIds">Kontrol edilecek element ID'leri</param>
        /// <returns>Tüm elementler geçerli etiket ise true</returns>
        private bool ValidateSelection(IList<ElementId> elementIds)
        {
            if (elementIds == null || !elementIds.Any())
                return false;

            foreach (ElementId id in elementIds)
            {
                Element element = _doc.GetElement(id);
                
                // Element geçerliliğini kontrol et
                if (!element.IsValidObject)
                    return false;

                // Element tipini kontrol et
                if (!(element is IndependentTag tag))
                    return false;

                // Etiketin host elementini kontrol et
                var taggedIds = tag.GetTaggedLocalElementIds();
                if (!taggedIds.Any())
                    return false;
            }

            return true;
        }
    }
}
