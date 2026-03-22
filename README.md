<div align="center">
  <img src="BANNER.png" alt="KickDownloader Pro Logo" width="800" />
</div>

# 🟢 KickDownloader Pro

KickDownloader Pro, Kick.com üzerindeki canlı yayın tekrarlarını (VOD) ve klipleri (Clip) kolay, hızlı ve istediğiniz kalite/zaman aralığında indirmenizi sağlayan modern bir Windows masaüstü uygulamasıdır. 

## ✨ Özellikler

- **Hızlı ve Kolay İndirme:** Klipleri ve yayın tekrarlarını `yt-dlp` ve `ffmpeg` gücüyle orijinal kalitede, kayıpsız olarak direkt `.mp4` şeklinde indirme.
- **Zaman Ayarlı Kırpma:** Koca bir yayını indirmek yerine, sadece istediğiniz belirli bir zaman aralığını (Örn: 00:15:30 - 01:25:00) seçerek indirebilme imkanı.
- **Kalite Seçimi:** Kaynak videonun sunduğu "En İyi", 1080p, 720p gibi çeşitli kalite seçeneklerini algılar ve seçiminize sunar.
- **Otomatik Pano Algılama (Auto-Paste):** Kopyaladığınız Kick linkini, uygulamanın bağlantı kutusuna tıkladığınız an otomatik olarak yapıştırır.
- **Geçmiş Sistemi (History):** İndirdiğiniz içeriklerin tarihini, linkini ve başlık bilgisini ayrı bir "İndirme Geçmişi" sekmesinde özenle listeler.
- **Modern ve Karanlık Tema (Dark Mode):** Şık, göz yormayan, oyuncu dostu (Gamer) karanlık arayüz deneyimi.
- **Araç Güncelleyici:** Tek tıkla uygulama içerisinden (arka planda) `yt-dlp` güncellemesi yapabilme.

---

## 🎨 Logo - Yapay Zeka Tasarım Yönergeleri

**Kullanılacak Prompt Önerisi:**
> A modern, minimalist, and sleek gamer-centric flat logo for a Windows desktop application named "KickDownloader Pro". The logo should feature a creative "K" letter combined with a download arrow symbol. The primary color must be vibrant neon green (#53FC18) on a deep dark charcoal/black background (#1A1C20). No 3D effects, keep it completely 2D, sharp, and highly legible at small sizes like an app icon.

**Ölçüler:**
- **Oran:** 1:1 (Kare)
- **Minimum Boyut:** 512x512 piksel

Elde ettiğiniz görüntüyü `.ico` formatına çevirerek uygulamada kullanabilirsiniz.

---

## 🛠️ Gereksinimler

- **İşletim Sistemi:** Windows 10 veya Windows 11
- **Çalışma Zamanı:** [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- **Harici Bileşenler:** Uygulamanın çalışabilmesi için `yt-dlp.exe` ve `ffmpeg.exe` dosyalarının `.exe` ile aynı dizinde bulunması gerekir (Projeyi derlerken `PreserveNewest` komutu ile kopyalanması sağlanmıştır).

## 🚀 Kurulum

1. Repoyu bilgisayarınıza indirin / klonlayın.
2. `KickVOD.sln` veya `.slnx` çözümünü **Visual Studio 2022** ile açın.
3. Proje klasörünüzün kök dizininde veya çıktı dosya yolunda `ffmpeg.exe` ve `yt-dlp.exe` nin mevcut olduğundan emin olun.
4. F5'e basarak (Veya Rebuild ederek) projeyi çalıştırın.

## 📄 Lisans
© ® Vibe Coding
