# Jira To Rea Portal

Windows Forms uygulaması Jira Cloud üzerinde seçilen tarih aralığındaki worklog kayıtlarını çekerek Rea Portal zaman çizelgesi servisine aktarır.

## Özellikler

- Rea Portal ve Jira için ayrı giriş alanları
- Tarih aralığına göre Jira worklog kayıtlarını listeleme
- Worklog bilgilerini düzenleyebilme
- Seçilen kayıtları Rea Portal `TimeSheet/Create` servisine gönderme

## Çalıştırma

1. `.sln` dosyasını Visual Studio 2022 veya üzeri ile açın.
2. `JiraToRea.App` projesini başlangıç projesi olarak ayarlayın.
3. Uygulamayı `net6.0-windows` hedefi ile derleyip çalıştırın.

> Not: Uygulama, Rea Portal ve Jira servislerine erişmek için internet bağlantısı ve geçerli kimlik bilgileri gerektirir.

## Yapılandırma

- Rea Portal sekmesinde kullanıcı adı/şifre ile giriş yaptıktan sonra kullanıcı ve proje ID değerlerini girmeniz gerekir.
- Jira sekmesinde e-posta ve Atlassian API token bilgilerini kullanarak giriş yapabilirsiniz.

## Uyarı

Bu depo içinde .NET SDK bulunmadığı için CI ortamında derleme yapılmamıştır. Geliştirme sırasında yerel makinenizde `dotnet build` veya Visual Studio derleme adımlarını uygulayabilirsiniz.
