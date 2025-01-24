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
                // IndependentTag filtresi oluştur
                ISelectionFilter tagFilter = new TagFilter();

                // Kullanıcıya seçim yaptır
                IList<Reference> selectedRefs = _uiDoc.Selection.PickObjects(
                    ObjectType.Element,
                    tagFilter,
                    "Düzenlenecek etiketleri seçin");

                // Seçilen referansları ElementId'ye dönüştür
                var tagIds = selectedRefs.Select(r => r.ElementId).ToList();

                // Seçim validasyonu
                if (!ValidateSelection(tagIds))
                {
                    TaskDialog.Show("Hata", "Seçilen elementlerden bazıları geçerli etiket değil.");
                    return null;
                }

                return tagIds;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Kullanıcı seçimi iptal etti
                return null;
            }
            catch (Exception ex)
            {
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
