# SecureShop GitHub Actions

## CI

`.github/workflows/ci.yml`:

- `main` dalına açılan pull request'lerde çalışır.
- `main` dışındaki dallara yapılan push'larda çalışır.
- Çözümü restore eder, Release modunda derler ve tüm testleri çalıştırır.
- Test sonuçlarını ve kod kapsamı dosyalarını 14 gün boyunca artifact olarak saklar.

## CD

`.github/workflows/cd.yml`, `main` dalına yapılan push'ta veya elle
çalıştırıldığında önce CI doğrulamasını yapar. Ardından API ve MVC projelerinin
çalıştırılabilir yayın paketlerini üretip GitHub Actions artifact'ı olarak 7 gün
saklar.

`v` ile başlayan bir Git etiketi gönderildiğinde (örneğin `v1.0.0`) aynı paketler
otomatik olarak `.tar.gz` arşivlerine dönüştürülür ve GitHub Release'e eklenir.

Bu akış için repository secret, variable veya ücretli bir bulut servisi gerekmez.
GitHub Release işlemi, workflow'a GitHub tarafından otomatik verilen
`GITHUB_TOKEN` ile yapılır.

Örnek sürüm yayınlama:

```bash
git tag v1.0.0
git push origin v1.0.0
```
