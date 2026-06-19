# TeamSync Quick Start Guide
**Team accountability, simplified**

## 🚀 Getting Started in 5 Minutes

### Step 1: Verify Installation
Ensure you have .NET 10 SDK:
```bash
dotnet --version
# Should show 10.0.x or higher
```

### Step 2: Clone & Open
```bash
git clone https://github.com/jomonibo90-blip/TeamSync.git
cd TeamSync
start TeamSync.sln
```

### Step 3: Build
- Press `Ctrl+Shift+B` in Visual Studio, or
- Run `dotnet build` in terminal

### Step 4: Run
- Press `F5` to start debugging, or
- Run `dotnet run`

### Step 5: Login
- Browser opens to `https://localhost:5001`
- Use any test account below
- Database is created automatically on first run

## 🔑 Test Accounts

All accounts have the password: `Student@123456`, `Professor@123456`, or `Admin@123456`

### Student Accounts (Best for testing regular features)
```
Email: student1@teamsync.com
Password: Student@123456
Role: Student

Email: student2@teamsync.com
Password: Student@123456
Role: Student

Email: student3@teamsync.com
Password: Student@123456
Role: Student
```

### Professor Account (For testing professor features - coming in Sprint 2)
```
Email: professor@teamsync.com
Password: Professor@123456
Role: Professor
```

### Admin Account (System administrator)
```
Email: admin@teamsync.com
Password: Admin@123456
Role: Admin
```

## 📁 Project Structure Quick Reference

```
TeamSync/
├── Models/              ← Data models (User, Group, Task, etc.)
├── Controllers/         ← Logic controllers (AccountController)
├── Views/               ← Razor templates for web pages
├── ViewModels/          ← Models for views (RegisterViewModel, etc.)
├── Services/            ← Business logic (DbInitializerService)
├── Data/                ← Database context (ApplicationDbContext)
├── wwwroot/             ← Static files (CSS, JS, images)
├── Migrations/          ← EF Core migration files (auto-generated)
├── appsettings.json     ← Configuration file
├── Program.cs           ← Application startup
└── TeamSync.csproj      ← Project file
```

## 🔧 Common Tasks

### View Database
1. In Visual Studio: Tools → SQL Server Object Explorer
2. Find (localdb)\mssqllocaldb → TeamSync
3. Expand to view tables

### Create a Migration (after model changes)
```bash
dotnet ef migrations add NameOfChange
```

### Update Database with Migration
```bash
dotnet ef database update
```

### Clear Database and Reseed
```bash
dotne
