# AutoTag - Revit Etiket Düzenleme Eklentisi

Revit etiketlerini otomatik olarak düzenleyen ve konumlandıran bir Revit eklentisi.

## Özellikler

- Etiketleri Y ekseninde otomatik sıralama
- Tag listesi yönüne göre akıllı konumlandırma:
  - +X yönünde: Yukarıdan aşağıya normal sıralama
  - -X yönünde: Yukarıdan aşağıya ters sıralama (leader çakışmalarını önlemek için)
- 500mm sabit dikey aralık
- Host bağlı etiketler için otomatik kontrol
- Detaylı loglama sistemi

## Teknik Detaylar

- .NET Framework 4.8
- Revit 2022 API
- C# 9.0
- Transaction yönetimi
- Metrik sistem desteği (mm)

## Kullanım

1. Revit'te "BIRD testTask" sekmesini açın
2. "Auto Arrange" butonuna tıklayın
3. Düzenlemek istediğiniz etiketleri seçin
4. Başlangıç noktasını belirleyin

## Hata Ayıklama

Log dosyası konumu: `%AppData%/TagsOrdering.log`

## Geliştirici

- **BT**
- GitHub: BTankut
