# SmartIQC — Web Application

Versi web dari aplikasi **SMRT QC** (Smart Quality Control).  
Dibangun dengan **ASP.NET Core 8 MVC + MySQL**.  
Dapat diakses dari Windows, Mac, tablet, maupun HP — tidak perlu install apapun di client.

---

## Prasyarat

| Tool | Versi | Download |
|------|-------|----------|
| .NET SDK | 8.0+ | https://dotnet.microsoft.com/download |
| MySQL | 8.0+ | Via XAMPP atau instalasi langsung |
| phpMyAdmin | — | Bawaan XAMPP |
| IDE (opsional) | — | Visual Studio 2022 / VS Code / Rider |

---

## Langkah Setup (5 menit)

### 1. Install .NET 8 SDK

```bash
# Cek apakah sudah terinstall
dotnet --version
# Harus menampilkan 8.x.x
```

Download dari: https://dotnet.microsoft.com/download/dotnet/8.0

---

### 2. Siapkan MySQL + Database

**Via XAMPP** (paling mudah):
1. Download XAMPP dari https://www.apachefriends.org/
2. Start **Apache** dan **MySQL** di XAMPP Control Panel
3. Buka **phpMyAdmin** di http://localhost/phpmyadmin
4. Buat database baru:
   - Klik **New** di panel kiri
   - Nama database: `smartiqc_db`
   - Collation: `utf8mb4_unicode_ci`
   - Klik **Create**

---

### 3. Clone & Konfigurasi

```bash
# Masuk ke folder project
cd /path/to/SMRT_QC_Web

# Edit koneksi database di appsettings.json
# Sesuaikan password MySQL Anda (default XAMPP = kosong)
```

Buka `appsettings.json` dan sesuaikan:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=smartiqc_db;User=root;Password=;CharSet=utf8mb4;"
  }
}
```

> **Password MySQL kosong** = default XAMPP (tidak perlu diubah)  
> **Jika punya password**, isi di bagian `Password=yourpassword`

---

### 4. Restore & Jalankan

```bash
# Dari folder SMRT_QC_Web/
dotnet restore          # Download semua package NuGet
dotnet run              # Jalankan aplikasi
```

Aplikasi akan berjalan di:
- http://localhost:5000
- https://localhost:5001

> **Tabel database dibuat otomatis** saat pertama kali aplikasi dijalankan  
> (menggunakan `EnsureCreated` di Program.cs)

---

### 5. Buat User Admin Pertama

Setelah aplikasi jalan, buka phpMyAdmin dan jalankan SQL ini:

```sql
-- Hash password "admin123" dengan BCrypt
-- Gunakan tool online: https://bcrypt.online/ untuk hash password lain

INSERT INTO users (user_name, password, position, created_at, updated_at)
VALUES (
  'ADMIN',
  '$2a$11$K6K3r6tJxUNk2nUFVh7D5OoJfWa1X3K4L7M8N9O0P1Q2R3S4T5U6V',
  'ADMIN',
  NOW(),
  NOW()
);
```

> Atau gunakan endpoint seed jika tersedia di `/Auth/Seed`

**Default login:**
- Username: `ADMIN`
- Password: `admin123`

---

### 6. Tambah SignalR Client Library

```bash
# Install libman CLI
dotnet tool install -g Microsoft.Web.LibraryManager.Cli

# Dari folder project, install SignalR JS client
libman install @microsoft/signalr@latest -p unpkg -d wwwroot/lib/signalr
```

Atau download manual dari:  
https://cdnjs.com/libraries/microsoft-signalr  
→ Simpan ke `wwwroot/lib/signalr/dist/browser/signalr.min.js`

---

## Struktur Project

```
SMRT_QC_Web/
├── Controllers/          ← Business logic per fitur
│   ├── AuthController.cs          Login, Logout
│   ├── DashboardController.cs     Dashboard + Notifikasi
│   ├── InspectionController.cs    Input & kelola inspeksi
│   ├── UserController.cs          CRUD user
│   ├── PartController.cs          CRUD master part
│   ├── NcnController.cs           NCN + foto defect
│   ├── ReportController.cs        Print / PDF
│   └── ImportExportController.cs  Excel in/out
│
├── Models/               ← Data model + validasi
│   ├── User.cs
│   ├── Part.cs
│   ├── InspectionRecord.cs
│   ├── NcnRecord.cs
│   ├── Notification.cs
│   └── ViewModels/
│
├── Data/
│   └── AppDbContext.cs   ← EF Core DbContext (MySQL)
│
├── Hubs/
│   └── NotificationHub.cs ← SignalR real-time
│
├── Views/                ← Halaman Razor (.cshtml)
│   ├── Shared/
│   │   └── _Layout.cshtml    ← Layout utama (sidebar navy)
│   ├── Auth/Login.cshtml     ← Halaman login
│   ├── Dashboard/            ← Dashboard & stats
│   ├── Inspection/           ← List, Create, Detail
│   ├── Users/                ← Manajemen user
│   ├── Parts/                ← Master part
│   ├── Ncn/                  ← NCN + kamera
│   ├── Reports/              ← Print laporan
│   └── ImportExport/         ← Excel in/out
│
├── wwwroot/              ← Static files
│   ├── css/app.css       ← Custom styles
│   ├── js/app.js         ← SignalR + Camera + utilities
│   └── uploads/          ← Foto NCN tersimpan di sini
│
├── Program.cs            ← Entry point + service config
├── appsettings.json      ← Konfigurasi & DB connection
└── SMRT_QC_Web.csproj    ← Package dependencies
```

---

## Role & Akses

| Role | User Mgmt | Master Part | Inspeksi | NCN | Quality Data | Import/Export |
|------|-----------|-------------|----------|-----|--------------|---------------|
| ADMIN | ✅ Full | ✅ | ✅ | ✅ | ✅ | ✅ |
| MANAGER | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ |
| SUPERVISOR | ❌ | ❌ | ✅ | ✅ | ❌ | ❌ |
| STAFF | ❌ | ❌ | ✅ Input | ✅ Buat | ❌ | ❌ |

---

## Tech Stack

| Komponen | Teknologi |
|----------|-----------|
| Backend | ASP.NET Core 8 MVC |
| Database | MySQL 8.0 via Pomelo EF Core |
| Real-time | SignalR |
| Frontend | Razor + TailwindCSS CDN + Alpine.js |
| Excel | ClosedXML |
| Auth | Cookie Authentication + BCrypt |
| Kamera | Browser MediaDevices API |
| Print/PDF | HTML Print (browser built-in) |

---

## Troubleshooting

**❌ "Connection refused" ke MySQL**
→ Pastikan XAMPP MySQL sudah Running (tombol hijau)

**❌ "Table doesn't exist"**
→ Hapus database `smartiqc_db` di phpMyAdmin, buat ulang, restart app

**❌ Kamera tidak muncul**
→ Browser meminta izin kamera — klik "Allow" / "Izinkan"  
→ Di Chrome: pastikan akses dari `https://` atau `localhost`

**❌ SignalR tidak connect**
→ Pastikan `wwwroot/lib/signalr/dist/browser/signalr.min.js` ada  
→ Jalankan `libman install @microsoft/signalr@latest -p unpkg -d wwwroot/lib/signalr`

---

## Deploy ke Hosting / Server

```bash
# Build production
dotnet publish -c Release -o ./publish

# Copy folder publish ke server
# Pastikan server punya .NET 8 Runtime
# Konfigurasi IIS atau nginx reverse proxy
```
