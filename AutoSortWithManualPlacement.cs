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
        private LeaderStyle CurrentLeaderStyle;

        // Yatay düzenleme sabitleri
        private const double HORIZONTAL_SPACING_MM = 80;
        private const double HORIZONTAL_MARGIN_MM = 20;
        private const double HORIZONTAL_SPACING_MULTIPLIER = 1.0;

        // Dikey düzenleme sabitleri
        private const double VERTICAL_SPACING_MM = 150;
        private const double VERTICAL_MARGIN_MM = 30;
        private const double VERTICAL_SPACING_MULTIPLIER = 3.0;

        // Birim dönüşümü
        private const double MM_TO_FEET = 304.8;     

        // Spacing hesaplamaları
        private const double HORIZONTAL_SPACING_FEET = (HORIZONTAL_SPACING_MM + HORIZONTAL_MARGIN_MM) / MM_TO_FEET;
        private const double VERTICAL_SPACING_FEET = ((VERTICAL_SPACING_MM + VERTICAL_MARGIN_MM) / MM_TO_FEET) * VERTICAL_SPACING_MULTIPLIER;

        public AutoSortWithManualPlacement(Document doc, UIDocument uiDoc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _uiDoc = uiDoc ?? throw new ArgumentNullException(nameof(uiDoc));
        }

        /// <summary>
        /// Sıralanmış etiketleri yerleştirir
        /// </summary>
        public bool PlaceSortedTags(List<IndependentTag> tags, XYZ startPoint, string direction, string leaderStyle = "Straight")
        {
            try
            {
                using (Transaction trans = new Transaction(_doc, "Etiketleri Yerleştir"))
                {
                    trans.Start();

                    if (direction == "Horizontal")
                    {
                        PlaceTagsHorizontally(tags, startPoint, HORIZONTAL_SPACING_FEET);
                    }
                    else
                    {
                        PlaceTagsVertically(tags, startPoint, VERTICAL_SPACING_FEET, leaderStyle);
                    }

                    trans.Commit();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Etiketler yerleştirilirken hata oluştu", ex);
                return false;
            }
        }

        /// <summary>
        /// Etiketleri yatay olarak yerleştirir
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
        /// Etiketleri dikey olarak yerleştirir
        /// </summary>
        private void PlaceTagsVertically(List<IndependentTag> tags, XYZ startPoint, double spacing, string leaderStyle)
        {
            // Etiketlerin ortalama Y koordinatını hesapla
            double avgTagY = tags.Average(t => t.TagHeadPosition.Y);
            bool isLowerRegion = avgTagY < startPoint.Y;

            Logger.LogInfo($"Etiketler {(isLowerRegion ? "alt (-y)" : "üst (+y)")} bölgede");
            Logger.LogInfo($"Başlangıç noktası Y: {startPoint.Y}, X: {startPoint.X}");

            // Element lokasyonlarını al
            var tagLocations = new List<(IndependentTag tag, XYZ location)>();
            foreach (var tag in tags)
            {
                var taggedElementIds = tag.GetTaggedLocalElementIds();
                var taggedElement = taggedElementIds.Count > 0 ? _doc.GetElement(taggedElementIds.First()) : null;
                var elementLocation = GetElementLocation(taggedElement);

                if (elementLocation != null)
                {
                    tagLocations.Add((tag, elementLocation));
                }
            }

            // Listenin yönünü belirle (startPoint'e göre elementlerin çoğunluğu hangi yönde)
            bool isListOnNegativeX = tagLocations.Average(t => t.location.X) < startPoint.X;
            Logger.LogInfo($"Liste {(isListOnNegativeX ? "negatif (-x)" : "pozitif (+x)")} yönde");

            // X koordinatına göre sırala
            var sortedTags = isLowerRegion
                ? (isListOnNegativeX 
                    ? tagLocations.OrderBy(t => t.location.X).Select(t => t.tag).ToList()           // Alt bölge, -x: Yakından uzağa
                    : tagLocations.OrderByDescending(t => t.location.X).Select(t => t.tag).ToList()) // Alt bölge, +x: Uzaktan yakına
                : (isListOnNegativeX 
                    ? tagLocations.OrderByDescending(t => t.location.X).Select(t => t.tag).ToList()  // Üst bölge, -x: Uzaktan yakına
                    : tagLocations.OrderBy(t => t.location.X).Select(t => t.tag).ToList());         // Üst bölge, +x: Yakından uzağa

            // İlk etiketin Y koordinatını belirle
            double currentY = startPoint.Y;

            foreach (var tag in sortedTags)
            {
                var taggedElementIds = tag.GetTaggedLocalElementIds();
                var taggedElement = taggedElementIds.Count > 0 ? _doc.GetElement(taggedElementIds.First()) : null;
                var elementLocation = GetElementLocation(taggedElement);

                if (elementLocation != null)
                {
                    var newTagLocation = new XYZ(startPoint.X, currentY, startPoint.Z);

                    if (CurrentLeaderStyle == LeaderStyle.LShape)
                    {
                        // Tam 90 derece L şeklinde leader için ara nokta oluştur
                        var intermediatePoint = new XYZ(elementLocation.X, currentY, elementLocation.Z);
                        tag.LeaderEndCondition = LeaderEndCondition.Free;
                        
                        // Element referansını ve leader elbow'u ayarla
                        var reference = new Reference(taggedElement);
                        tag.SetLeaderElbow(reference, intermediatePoint);

                        // Leader başlangıç noktasını element üzerinde ayarla
                        tag.SetLeaderEnd(reference, elementLocation);
                    }
                    else
                    {
                        // Düz leader çizgisi için direkt bağlantı
                        var reference = new Reference(taggedElement);
                        tag.LeaderEndCondition = LeaderEndCondition.Free;
                        tag.SetLeaderEnd(reference, elementLocation);
                    }

                    tag.TagHeadPosition = newTagLocation;
                    currentY -= spacing; // Her zaman yukarıdan aşağıya doğru yerleştir
                }
            }

            Logger.LogInfo("Etiketler başarıyla yerleştirildi");
        }

        /// <summary>
        /// Element'in lokasyonunu alır
        /// </summary>
        private XYZ GetElementLocation(Element element)
        {
            if (element == null) return null;

            var locationPoint = element.Location as LocationPoint;
            if (locationPoint != null)
                return locationPoint.Point;

            var locationCurve = element.Location as LocationCurve;
            if (locationCurve != null)
                return locationCurve.Curve.GetEndPoint(0);

            return null;
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
                var startPoint = _uiDoc.Selection.PickPoint("Etiketlerin başlayacağı noktayı seçin");

                // Geçici olarak, dikey yerleştirme durumu true kabul ediliyor
                bool isVertical = true;
                if (isVertical)
                {
                    // Kullanıcıya leader çizgi stilini seçtirmek için bir diyalog gösteriyoruz
                    TaskDialog td = new TaskDialog("Leader Style Seçimi");
                    td.MainInstruction = "Leader çizgi stilini seçiniz:";
                    td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Düz Leader (Mevcut)");
                    td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "L Leader (90° kırılımlı)");
                    TaskDialogResult res = td.Show();
                    
                    // Kullanıcı seçimine göre leader stilini belirliyoruz
                    LeaderStyle leaderStyle = (res == TaskDialogResult.CommandLink2) ? LeaderStyle.LShape : LeaderStyle.Straight;
                    this.CurrentLeaderStyle = leaderStyle;
                }
                
                return startPoint;
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

        /// <summary>
        /// L Leader çizimini gerçekleştirir
        /// </summary>
        private void DrawLLeader(IndependentTag tag, XYZ elementLocation, XYZ tagLocation)
        {
            // İlk segment: element konumundan, etiketin X koordinatına sahip bir ara nokta oluşturuyoruz
            XYZ intermediatePoint = new XYZ(tagLocation.X, elementLocation.Y, elementLocation.Z);
            
            // Revit API kullanarak iki segmentli leader çizgisini oluşturun
            // Örnek kod: 
            // LeaderSegment segment1 = doc.CreateLeaderSegment(tag, elementLocation, intermediatePoint);
            // LeaderSegment segment2 = doc.CreateLeaderSegment(tag, intermediatePoint, tagLocation);
            
            // Not: Yukarıdaki API çağrıları örnek olup, gerçek Revit API yöntemlerini kullanarak uygun şekilde implemente edilmelidir.
        }
    }
}
