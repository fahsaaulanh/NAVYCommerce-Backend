# NAVYCommerce — Panduan Instalasi dan Pengujian Lokal untuk QA

Panduan ini ditujukan untuk QA yang belum memiliki Git, Node.js, .NET SDK, atau PostgreSQL. Jalur instalasi utama menggunakan Windows 10/11, PowerShell, dan dua repository terpisah.

Backend: .NET 8 Web API, EF Core 8, PostgreSQL. Frontend: Next.js App Router, TypeScript, Tailwind CSS 4.

> Sebelum membagikan panduan ini, pemilik project perlu mengganti `USERNAME` pada URL repository dan menyelesaikan checklist pada bagian 1. URL repository belum diberikan saat dokumen dibuat. Perintah disusun berdasarkan source backend yang dikirim dan perubahan frontend yang disepakati; instalasi pada komputer QA belum diuji langsung.

## Daftar isi

1. [Persiapan oleh pemilik project](#1-persiapan-oleh-pemilik-project)
2. [Instal software](#2-instal-software)
3. [Download source dari GitHub](#3-download-source-dari-github)
4. [Buat database PostgreSQL](#4-buat-database-postgresql)
5. [Konfigurasi dan migration backend](#5-konfigurasi-dan-migration-backend)
6. [Jalankan backend](#6-jalankan-backend)
7. [Konfigurasi dan jalankan frontend](#7-konfigurasi-dan-jalankan-frontend)
8. [Pengujian pertama](#8-pengujian-pertama)
9. [Menjalankan aplikasi pada hari berikutnya](#9-menjalankan-aplikasi-pada-hari-berikutnya)
10. [Update versi pengujian](#10-update-versi-pengujian)
11. [Troubleshooting](#11-troubleshooting)
12. [Format laporan bug](#12-format-laporan-bug)
13. [Referensi resmi](#13-referensi-resmi)

## 1. Persiapan oleh pemilik project

Bagian ini dikerjakan developer sebelum QA mengikuti instalasi.

### 1.1 Repository dan akses

- Repository frontend: `https://github.com/USERNAME/navycommerce-frontend`.
- Repository backend: `https://github.com/USERNAME/navycommerce-backend`.
- Undang QA sebagai collaborator jika repository private; QA harus menerima undangannya.
- Commit source, lockfile frontend, solution, semua project `.csproj`, dan migration.
- Sertakan `frontend/.env.example` dan contoh konfigurasi backend tanpa secret asli.
- Catat pasangan commit FE dan BE yang menjadi target pengujian. Dua repository bisa memiliki versi berbeda; jangan hanya menyebut "versi terbaru".

### 1.2 Temuan dari ZIP backend yang perlu dicek

| Temuan | Tindakan pemilik project |
| --- | --- |
| `NAVYCommerce.sln`, project API, dan Infrastructure merujuk `NAVYCommerce.Application.csproj`, tetapi file tersebut tidak ada di ZIP | Pastikan folder/project Application lengkap di repository. Jika referensinya memang tidak dipakai, developer yang merapikannya, bukan QA. |
| Migration yang tersedia membuat products, users, product_variants, dan user_otps; belum terlihat migration tabel categories dan perubahan katalog terbaru | Pastikan migration yang sesuai model terkini ikut di-commit dan database kosong bisa dibangun dari migration tersebut. |
| `Program.cs` pada ZIP belum mengonfigurasi CORS | Register dan OTP saat ini dipanggil langsung oleh browser ke backend. Pastikan origin frontend `http://localhost:3000` diizinkan sebelum QA menguji melalui UI. |
| ZIP tidak memiliki launch profile | Panduan menggunakan perintah run dengan environment dan port eksplisit. |

Untuk memeriksa perubahan model yang belum dimigrasikan, developer dapat menjalankan ini dari root backend setelah konfigurasi tersedia:

```powershell
dotnet ef migrations has-pending-model-changes --project src/NAVYCommerce.Infrastructure --startup-project src/NAVYCommerce.API
```

Jika ada perubahan, developer membuat dan meninjau migration, lalu mengujinya pada database lokal kosong sebelum commit. QA cukup menjalankan `database update`, tidak membuat migration sendiri.

Jika CORS belum tersedia, developer dapat menambahkan policy berikut sebelum `builder.Build()` di `Program.cs`:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalFrontend", policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});
```

Panggil policy setelah routing dan sebelum authorization pada pipeline aplikasi:

```csharp
app.UseRouting();
app.UseCors("LocalFrontend");
// Pertahankan konfigurasi authentication/authorization project.
app.UseAuthorization();
app.MapControllers();
```

Gabungkan dengan pipeline yang sudah ada; jangan menduplikasi `MapControllers()` atau mengganti keseluruhan `Program.cs`. Ini kebutuhan koneksi UI lokal, bukan perubahan validasi bisnis yang disediakan untuk latihan QA.

### 1.3 Kontrak versi pengujian

Panduan ini mengharapkan perubahan berikut sudah di-push:

- Respons auth memiliki JSON `message`; login juga mengembalikan `token` dan `role`.
- Route frontend berada di `app/api/auth/login/route.ts` atau `src/app/api/auth/login/route.ts`.
- Login menyimpan cookie `navy_session` melalui server Next.js.
- Register dan verifikasi OTP masih langsung menuju backend .NET.
- Role Customer = 0, Seller = 1. Merchant pada label UI berarti Seller pada backend.

Sebagian validasi dan celah backend memang sengaja disediakan untuk latihan QA. QA mencatat temuannya, tidak memperbaiki source atau schema secara sepihak. Semua pengujian menggunakan database lokal dan data dummy.

### 1.4 Uji instalasi bersih

Developer perlu melakukan satu kali clone bersih dan mengikuti panduan ini memakai database baru. Aplikasi yang berjalan pada database developer lama belum membuktikan migration dapat membangun database QA dari nol.

## 2. Instal software

Gunakan installer resmi. Untuk komputer Windows x64 pilih installer x64; pilih ARM64 hanya bila perangkat menggunakan ARM. Tutup lalu buka kembali terminal/VS Code setelah instalasi agar PATH diperbarui.

| Software | Pilihan | Link resmi | Kegunaan |
| --- | --- | --- | --- |
| Git | Git for Windows | https://git-scm.com/install/windows | Clone dan update source |
| Node.js | Node.js 24 LTS, selama sesuai `engines`/`.nvmrc` repository | https://nodejs.org/en/download | Menjalankan frontend dan npm |
| .NET | **SDK 8.0**, bukan hanya Runtime | https://dotnet.microsoft.com/en-us/download/dotnet/8.0 | Restore, build, migration, menjalankan backend |
| PostgreSQL | PostgreSQL 16, patch terbaru pada seri 16; samakan dengan developer jika versinya telah ditetapkan | https://www.postgresql.org/download/windows/ | Database lokal |
| Editor | VS Code, opsional | https://code.visualstudio.com/ | Mengedit konfigurasi dan membuka terminal |

Tidak perlu menginstal Go, Redis, RabbitMQ, Docker, atau layanan payment/shipping untuk pengujian auth saat ini; komponen tersebut masih dalam roadmap yang diberikan.

### 2.1 Git

1. Download dan jalankan installer Git for Windows.
2. Gunakan opsi yang membuat Git tersedia dari command line; pengaturan default installer biasanya sudah mencakupnya.
3. Buka PowerShell baru dan jalankan:

```powershell
git --version
```

Hasil yang diharapkan: nomor versi Git, bukan pesan "not recognized".

### 2.2 Node.js

1. Periksa versi Node yang ditentukan developer dalam `.nvmrc`, `package.json` (`engines`), atau README.
2. Jika belum ditentukan, gunakan Node.js 24 LTS sebagai pilihan awal panduan ini. Versi dependency frontend lengkap belum tersedia saat panduan disusun; developer perlu mengonfirmasi kompatibilitas.
3. Jalankan installer Windows dengan npm dan PATH aktif.
4. Periksa:

```powershell
node --version
npm.cmd --version
```

Perintah `npm.cmd` digunakan di Windows untuk menghindari masalah execution policy pada `npm.ps1`. Tidak perlu mengubah execution policy komputer.

### 2.3 .NET SDK 8

1. Pilih **SDK 8.0 → Windows → installer**.
2. Instal; SDK mencakup alat build dan runtime untuk pengembangan.
3. Periksa:

```powershell
dotnet --list-sdks
```

Pastikan terdapat SDK `8.0.xxx`. Jika juga ada SDK versi lain, itu tidak otomatis merupakan masalah. Bila repository memiliki `global.json`, versi yang dipilih harus mengikuti file tersebut.

### 2.4 PostgreSQL dan pgAdmin

1. Buka link PostgreSQL Windows dan ikuti link installer EDB.
2. Pilih PostgreSQL 16 dengan patch terbaru yang tersedia.
3. Saat memilih komponen, sertakan **PostgreSQL Server**, **pgAdmin 4**, dan **Command Line Tools**.
4. Buat dan catat password user database `postgres`. Password ini dibuat oleh QA sendiri, bukan password GitHub atau password developer.
5. Gunakan port `5432` jika belum dipakai.
6. Pengaturan locale dapat menggunakan default.
7. Stack Builder tidak diperlukan untuk project ini.

PostgreSQL menyimpan data; pgAdmin adalah aplikasi untuk mengelolanya. Menutup pgAdmin tidak sama dengan mematikan layanan PostgreSQL.

## 3. Download source dari GitHub

Contoh lokasi kerja: `C:\Projects\NAVYCommerce`. QA tidak harus memiliki drive D.

Buka PowerShell:

```powershell
New-Item -ItemType Directory -Force C:\Projects\NAVYCommerce
Set-Location C:\Projects\NAVYCommerce
```

Ganti `USERNAME` sesuai URL yang diberikan developer:

```powershell
git clone https://github.com/USERNAME/navycommerce-backend.git backend-core
git clone https://github.com/USERNAME/navycommerce-frontend.git frontend-store
```

Untuk repository private, ikuti autentikasi GitHub yang ditampilkan Git Credential Manager. Password akun GitHub bukan password untuk autentikasi Git melalui HTTPS. Jangan menaruh access token di URL clone atau laporan bug.

Folder hasilnya:

```text
C:\Projects\NAVYCommerce\backend-core
C:\Projects\NAVYCommerce\frontend-store
```

Tidak perlu menjalankan `git init`; `git clone` sudah membuat repository lokal. Catat versi awal:

```powershell
git -C backend-core rev-parse --short HEAD
git -C frontend-store rev-parse --short HEAD
```

Jika developer memberikan pasangan commit/tag khusus, gunakan versi tersebut sebelum melanjutkan pengujian. Jangan melakukan `pull` di tengah satu sesi pengujian tanpa mencatat perubahan versinya.

## 4. Buat database PostgreSQL

1. Buka **pgAdmin 4**.
2. Buka **Servers → PostgreSQL** dan masukkan password `postgres` yang dibuat saat instalasi.
3. Jika server belum terdaftar: klik kanan Servers → Register → Server.
   - General → Name: `Local PostgreSQL`.
   - Connection → Host: `localhost`.
   - Port: `5432`.
   - Maintenance database: `postgres`.
   - Username: `postgres`.
   - Password: password instalasi PostgreSQL.
4. Klik kanan **Databases → Create → Database**.
5. Database: **`navycommerce_qa`**.
6. Owner: `postgres` untuk setup lokal sederhana ini.
7. Klik Save.

Database masih kosong. Tabel akan dibuat melalui migration, bukan satu per satu di pgAdmin. Penggunaan user `postgres` dalam panduan ini khusus lingkungan QA lokal.

## 5. Konfigurasi dan migration backend

### 5.1 Pastikan folder project lengkap

```powershell
Set-Location C:\Projects\NAVYCommerce\backend-core
Test-Path .\NAVYCommerce.sln
Test-Path .\src\NAVYCommerce.API\NAVYCommerce.API.csproj
Test-Path .\src\NAVYCommerce.Application\NAVYCommerce.Application.csproj
Test-Path .\src\NAVYCommerce.Infrastructure\NAVYCommerce.Infrastructure.csproj
Test-Path .\src\NAVYCommerce.Domain\NAVYCommerce.Domain.csproj
```

Untuk struktur backend yang diperiksa, semua hasil harus `True`. Jika ada `False`, minta developer memastikan repository lengkap. Jangan membuat project kosong untuk mengakali dependency yang hilang.

### 5.2 Buat konfigurasi Development

Buat file berikut menggunakan VS Code atau editor teks:

```text
backend-core/src/NAVYCommerce.API/appsettings.Development.json
```

Isi:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=navycommerce_qa;Username=postgres;Password=REPLACE_WITH_YOUR_POSTGRES_PASSWORD"
  },
  "JwtSettings": {
    "Secret": "QA_LOCAL_ONLY_0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_0123456789abcdef",
    "Issuer": "NAVYCommerce.API",
    "Audience": "NAVYCommerce.Frontend"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

Ganti `REPLACE_WITH_YOUR_POSTGRES_PASSWORD` dengan password PostgreSQL lokal. Jika port instalasi berbeda, ubah `Port`. Contoh secret di atas hanya untuk setup QA lokal, bukan secret produksi. Jika developer memberikan konfigurasi issuer/audience khusus, gunakan pasangan nilai yang diberikan.

File ini memang tidak di-commit karena berisi konfigurasi lokal. ASP.NET Core membaca file Development ketika environment-nya Development; `Program.cs` saat ini memakai `GetConnectionString("DefaultConnection")` dan `JwtSettings` tersebut. .NET tidak otomatis membaca `.env.local` frontend.

### 5.3 Set environment untuk terminal backend

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
```

Variabel ini berlaku untuk terminal tersebut. Jika membuka terminal baru, jalankan kembali sebelum migration/run.

### 5.4 Restore dan build

Dari root `backend-core`:

```powershell
dotnet restore NAVYCommerce.sln
dotnet build NAVYCommerce.sln
```

Hasil yang diharapkan: `Build succeeded`. Pertama kali restore memerlukan internet untuk mengunduh package NuGet. Jika gagal, selesaikan error sebelum migration.

### 5.5 Instal alat migration EF Core

Pada source yang diperiksa, EF Core memakai versi 8.0.8. Untuk menyamakan tool dengan versi itu:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.8
dotnet ef --version
```

Jika tool sudah terpasang pada versi berbeda, minta developer menentukan penyesuaian, terutama jika komputer dipakai project lain. Jangan memasang versi mayor terbaru secara acak. Jika repository nantinya menyediakan manifest tool lokal, ikuti `dotnet tool restore` dan versi manifest tersebut.

### 5.6 Jalankan migration

```powershell
dotnet ef migrations list --project src/NAVYCommerce.Infrastructure --startup-project src/NAVYCommerce.API
dotnet ef database update --project src/NAVYCommerce.Infrastructure --startup-project src/NAVYCommerce.API
```

- `--project` menunjuk tempat DbContext dan migration berada.
- `--startup-project` menunjuk API yang menyediakan konfigurasi dan dependency saat migration dijalankan.
- `database update` menerapkan migration yang telah ditulis developer.

Setelah berhasil, refresh database `navycommerce_qa` di pgAdmin. Periksa **Schemas → public → Tables**. Berdasarkan model terkini, tabel aplikasi yang diharapkan meliputi `users`, `user_otps`, `categories`, `products`, dan `product_variants`, ditambah `__EFMigrationsHistory`.

Jika migration selesai tetapi tabel/model tidak sesuai, laporkan kepada developer. Jangan menjalankan `EnsureCreated`, membuat tabel manual, atau membuat migration baru sebagai bagian setup QA.

## 6. Jalankan backend

Tetap di terminal backend, dengan environment Development aktif:

```powershell
dotnet run --project src/NAVYCommerce.API --no-launch-profile --urls http://localhost:5000
```

Tunggu log seperti:

```text
Now listening on: http://localhost:5000
```

Buka:

```text
http://localhost:5000/swagger
```

Swagger tersedia dalam environment Development pada source yang diperiksa. Terminal ini harus tetap berjalan selama pengujian. Ctrl+C menghentikan backend, bukan menghapus database.

Panduan memakai HTTP lokal agar QA tidak perlu menyiapkan sertifikat. Jika aplikasi mengalihkan request ke HTTPS, developer perlu menyediakan konfigurasi lokal yang sesuai atau alamat HTTPS beserta langkah trust certificate. Jangan mematikan pemeriksaan sertifikat Node.js untuk mengatasi error.

## 7. Konfigurasi dan jalankan frontend

### 7.1 Buka terminal kedua

Biarkan terminal backend berjalan. Di PowerShell baru:

```powershell
Set-Location C:\Projects\NAVYCommerce\frontend-store
```

### 7.2 Instal dependency

Jika repository memiliki `package-lock.json` dan memakai npm:

```powershell
npm.cmd ci
```

`npm ci` memakai lockfile agar dependency pengujian konsisten. Jika gagal karena package.json dan lockfile tidak sinkron, laporkan kepada developer; jangan otomatis mengganti lockfile. Jika developer belum menyediakan lockfile, minta developer menyertakannya. Untuk project yang ditetapkan memakai pnpm/yarn, ikuti package manager serta lockfile repository, bukan npm.

### 7.3 Buat `.env.local`

Lokasinya di root frontend, sejajar `package.json`, bukan di folder app:

```text
frontend-store/.env.local
```

Isi:

```env
NEXT_PUBLIC_AUTH_API_URL=http://localhost:5000/api/Auth
AUTH_API_URL=http://localhost:5000/api/Auth
```

| Variabel | Pengguna | Proses saat ini |
| --- | --- | --- |
| NEXT_PUBLIC_AUTH_API_URL | Browser | Register dan verifikasi OTP langsung ke .NET |
| AUTH_API_URL | Server Next.js | Meneruskan login ke .NET |

Jika `.env.example` sudah tersedia dan nilainya cocok, boleh menyalinnya:

```powershell
Copy-Item .env.example .env.local
```

Lakukan penyalinan hanya jika `.env.local` belum ada agar konfigurasi lokal tidak tertimpa. Restart frontend setelah mengubah environment.

### 7.4 Jalankan frontend

```powershell
npm.cmd run dev -- --port 3000
```

Buka:

```text
http://localhost:3000/auth
```

Gunakan `localhost` secara konsisten. Jangan berganti ke `127.0.0.1` tanpa menyesuaikan origin CORS dan cookie. Jika port 3000 dipakai, hentikan aplikasi lain yang kamu kenali atau koordinasikan perubahan port dengan developer; pindah port juga memerlukan penyesuaian CORS.

### 7.5 Kenali alamat layanan

| Layanan | Alamat |
| --- | --- |
| PostgreSQL | localhost:5432 |
| Backend .NET | http://localhost:5000 |
| Swagger | http://localhost:5000/swagger |
| Frontend Next.js | http://localhost:3000 |
| Halaman auth | http://localhost:3000/auth |
| Endpoint login frontend | http://localhost:3000/api/auth/login |
| Endpoint login backend | http://localhost:5000/api/Auth/login |

Port 3000 pada request login browser memang benar. Request selanjutnya dari server Next.js ke port 5000 tidak muncul di Network browser.

## 8. Pengujian pertama

### 8.1 Buat akun dummy

1. Buka /auth → Daftar.
2. Isi nama, email dummy unik seperti `qa.customer.001@example.com`, dan password pengujian yang kamu catat sendiri.
3. Pilih Customer.
4. Submit, lalu periksa respons register pada DevTools → Network.
5. Backend saat ini mengembalikan kode melalui `mockEmailInbox`, misalnya `Your OTP code is: 123456`. Email belum dikirim sungguhan.
6. Masukkan OTP. Kode berlaku 5 menit dan hanya dapat digunakan sekali.
7. Setelah verifikasi berhasil, masuk dengan email/password yang sama.

Gunakan email baru untuk pendaftaran berikutnya bila akun sebelumnya sudah dibuat. Jangan mengandalkan email nyata masuk ke inbox pada implementasi mock saat ini.

### 8.2 Periksa login dan cookie

1. Login dengan akun yang sudah diverifikasi.
2. Request browser menuju POST `/api/auth/login` di port 3000.
3. Untuk versi yang sudah memasang sesi, respons sukses tidak mengembalikan token di JSON browser.
4. Buka DevTools → Application → Cookies → `http://localhost:3000`.
5. Cari cookie `navy_session`; HttpOnly harus aktif.
6. Pada development HTTP, Secure tidak diaktifkan oleh kode sesi saat ini.
7. Cookie dan JWT mengikuti masa berlaku sekitar 2 jam pada implementasi ini.
8. UI diarahkan ke `/`; halaman beranda harus tersedia pada versi frontend yang dibagikan.

Cookie yang ada belum berarti seluruh proteksi route sudah selesai. Pada tahap terakhir percakapan, navbar yang mengenali sesi, logout, dan verifikasi token untuk proteksi route belum diimplementasikan. Developer perlu memperbarui bagian ini jika fitur tersebut sudah ditambahkan sebelum handoff.

### 8.3 Smoke test minimum

| Skenario | Yang diperiksa |
| --- | --- |
| Buka auth desktop dan mobile | Navbar, footer, form, tidak ada konten penting terpotong |
| Hover tombol di desktop | Perubahan warna gradient dan bayangan terlihat |
| Fokus dan isi input | Label mengambang; teks tetap terbaca |
| Register | Akun dibuat dan form OTP muncul |
| OTP benar | Verifikasi berhasil dan kembali ke login |
| OTP salah/kedaluwarsa | Pesan dari backend tampil |
| Login password salah | Pesan ditolak, tombol dapat digunakan kembali |
| Login benar | Redirect dan cookie HttpOnly |
| Backend dihentikan | Error koneksi ditampilkan tanpa loading permanen |

Ini smoke test setup, bukan daftar lengkap test case keamanan. QA bebas mengeksplorasi bug/celah sesuai ruang lingkup latihan dan mencatat hasil aktual terhadap ekspektasi.

## 9. Menjalankan aplikasi pada hari berikutnya

Software dan dependency tidak perlu diinstal ulang setiap hari.

1. Pastikan layanan PostgreSQL aktif.
2. Buka terminal backend:

```powershell
Set-Location C:\Projects\NAVYCommerce\backend-core
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --project src/NAVYCommerce.API --no-launch-profile --urls http://localhost:5000
```

3. Buka terminal frontend:

```powershell
Set-Location C:\Projects\NAVYCommerce\frontend-store
npm.cmd run dev -- --port 3000
```

4. Buka http://localhost:3000/auth.

Migration cukup dijalankan kembali jika versi backend membawa migration baru. Membuka pgAdmin tidak wajib setiap kali aplikasi dijalankan.

## 10. Update versi pengujian

Selesaikan atau catat sesi pengujian lama. Hentikan kedua server dengan Ctrl+C.

Di masing-masing repository:

```powershell
git status
git pull --ff-only
```

Jika ada perubahan lokal pada source atau pull ditolak, koordinasikan dengan developer. Jangan menggunakan reset/clean untuk menghilangkan perubahan secara otomatis.

- Jika dependency frontend berubah: jalankan kembali `npm.cmd ci`.
- Jika package backend berubah: jalankan `dotnet restore NAVYCommerce.sln`.
- Jika ada migration baru: set environment Development, lalu jalankan `dotnet ef database update` dengan project/startup-project yang sama.
- Jalankan kembali kedua server dan catat commit baru FE/BE.

Database pengujian tersimpan antar-run. Reset data bukan bagian update otomatis; koordinasikan terlebih dahulu bila membutuhkan pengujian dari database kosong.

## 11. Troubleshooting

| Gejala | Pemeriksaan / tindakan |
| --- | --- |
| git/node/dotnet tidak dikenali | Pastikan instalasi selesai dan PATH aktif. Buka terminal baru. Untuk .NET, periksa `dotnet --list-sdks`. |
| npm.ps1 tidak dapat dijalankan | Gunakan `npm.cmd`; tidak perlu mengubah execution policy. |
| Repository not found / akses ditolak | Periksa URL, akun GitHub, dan undangan repository private. |
| Restore gagal karena project Application hilang | Repository tidak lengkap atau referensi belum dirapikan. Laporkan kepada developer. |
| dotnet ef tidak dikenali | Periksa instalasi tool dan PATH, lalu buka terminal baru. |
| Build gagal | Kirim error build pertama, bukan hanya baris "Build failed". Jangan lanjut migration dulu. |
| Connection refused pada PostgreSQL | Periksa layanan melalui `services.msc`, host localhost, dan port instalasi. |
| Password authentication failed | Cocokkan password user postgres dengan connection string lokal. |
| Database does not exist | Buat navycommerce_qa dan pastikan nama pada konfigurasi sama. |
| Relation/table does not exist | Periksa migration dan database yang dipakai. Jika migration repository belum lengkap, developer yang memperbaikinya. |
| Swagger 404 | Pastikan backend berjalan di port 5000 dan environment Development aktif. |
| Login `/api/auth/login` 404 | File route harus di dalam app/api/auth/login/route.ts atau src/app/api/auth/login/route.ts; bukan folder api sejajar app. Restart Next.js. |
| Login 500 | Periksa terminal Next.js dan AUTH_API_URL. |
| Login 502 | Pastikan backend aktif, URL/protokol sesuai, dan respons backend JSON. Periksa terminal backend. |
| Login 401 | Periksa email/password dan status verifikasi. Ini bisa merupakan hasil pengujian yang diharapkan. |
| Register/OTP berhasil di Swagger tetapi gagal di UI | Periksa Console browser untuk CORS. Origin localhost:3000 perlu diizinkan backend. Login server-to-server tidak membutuhkan CORS browser ke .NET. |
| Gagal karena sertifikat HTTPS | Samakan protokol konfigurasi dengan backend. Ikuti pengaturan sertifikat lokal developer; jangan menonaktifkan validasi TLS global. |
| Port 3000/5000/5432 digunakan | Identifikasi aplikasi pemilik port. Koordinasikan perubahan port dan konfigurasi terkait. |
| Beranda 404 setelah login | Periksa app/page.tsx pada frontend. Catat bahwa login dan cookie bisa berhasil walaupun halaman tujuan belum tersedia. |
| Cookie tidak terlihat | Pastikan melihat origin localhost:3000, respons login sukses, dan menjalankan mode dev HTTP. Periksa header Set-Cookie. |
| Hover tidak bergerak | Coba tampilan desktop dan periksa preferensi reduced motion; gerakan memang dinonaktifkan jika reduced motion aktif. |

Saat melaporkan error konfigurasi, kirim status dan pesan error, bukan isi password, token, cookie, atau secret.

## 12. Format laporan bug

```text
Judul:
Versi/commit frontend:
Versi/commit backend:
OS dan browser:
Jenis pengujian: setup / fungsional / eksplorasi keamanan
Prasyarat:
Langkah reproduksi:
1.
2.
3.
Hasil yang diharapkan:
Hasil aktual:
Request URL dan method:
Status HTTP:
Respons (hapus data rahasia):
Bukti screenshot/log:
Frekuensi reproduksi:
```

Contoh: "Register tidak dapat dipanggil dari UI karena CORS, tetapi berhasil melalui Swagger". Sertakan Console browser dan kedua commit agar developer dapat mereproduksinya.

## 13. Referensi resmi

- Git Windows: https://git-scm.com/install/windows
- Node.js installer/LTS: https://nodejs.org/en/download
- .NET SDK 8: https://dotnet.microsoft.com/en-us/download/dotnet/8.0
- PostgreSQL Windows installer dan komponen: https://www.postgresql.org/download/windows/
- EF Core CLI, pilihan versi tool, project, startup-project, dan database update: https://learn.microsoft.com/en-us/ef/core/cli/dotnet
- ASP.NET Core CORS dan urutan middleware: https://learn.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-8.0
- ASP.NET Core konfigurasi dan appsettings environment: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration?view=aspnetcore-8.0
- Next.js environment variables: https://nextjs.org/docs/app/guides/environment-variables
- Next.js authentication dan cookie sesi: https://nextjs.org/docs/app/guides/authentication
