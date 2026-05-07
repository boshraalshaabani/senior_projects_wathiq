# Wathiq — Intelligent Digital Archiving System

**Wathiq** is an AI-powered document management system built for enterprises and government institutions. It replaces traditional paper-based workflows with a secure, intelligent digital archive that supports multiple institutions and departments.

The system is made up of two services that work together:

- **eArchiveSystem** — the main backend that handles everything: users, documents, search, workflows, and reports.
- **eArchive.OcrService** — a separate AI service that reads and extracts text from uploaded documents using a Vision AI model (Meta LLaMA-4 Scout via Groq).

---


## Project Structure

```text
senior_projects_wathiq
├── eArchiveSystem          ## Main backend API
└── eArchive.OcrService     ## Separate OCR microservice
```

---
## Key Features

- Upload and manage documents with automatic duplicate detection
- AI-powered text extraction (OCR) supporting Arabic and English
- Full-text search across all documents
- Approval workflow: documents go from Draft → Review → Published → Archived
- Role-based access: System Admin, Institution Admin, Manager, Employee
- Two-factor authentication and account security
- Audit log for every action in the system
- Automatic watermarking for sensitive documents
- Reports exported as PDF or Excel

---

## Team

| Name |
|------|
| Salam Almasri |
| Najat Bostaty |
| Bushra Alshaabani |

---

## Prerequisites

Before running the project, make sure you have the following installed:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [MongoDB](https://www.mongodb.com/try/download/community) — running on `localhost:27017`
- [Elasticsearch](https://www.elastic.co/downloads/elasticsearch) — running on `localhost:9200`
- A [Groq API Key](https://console.groq.com) 
- Windows OS (required for PDF rendering)

---

## How to Run

### 1. Clone the repository

```bash
git clone https://github.com/boshraalshaabani/senior_projects_wathiq.git
cd senior_projects_wathiq
```

---

### 2. Configure the Main API

Go into the `eArchiveSystem` folder and create a local config file:

```bash
cd eArchiveSystem
cp appsettings.json appsettings.Development.local.json
```

Do not commit `appsettings.Development.local.json` because it contains local secrets.

Open `appsettings.Development.local.json` and fill in the following:

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "eArchiveDB"
  },
  "Elasticsearch": {
    "Url": "https://localhost:9200",
    "Username": "your_username",
    "Password": "your_password"
  },
  "Jwt": {
    "Key": "your_secret_key_here"
  },
  "EmailSettings": {
    "From": "your@email.com",
    "Password": "your_email_password"
  },
  "BootstrapAdmin": {
    "Name": "Super Admin",
    "Email": "admin@example.com",
    "Password": "StrongPassword123!"
  }
}
```

---

### 3. Configure the OCR Service

Go into the `eArchive.OcrService` folder and create a local config file:

```bash
cd ../eArchive.OcrService
cp appsettings.Development.local.example.json appsettings.Development.local.json
```

Open `appsettings.Development.local.json` and add your Groq API key:

```json
{
  "Groq": {
    "ApiKey": "your_groq_api_key_here"
  }
}
```

---

### 4. Run the services

Open **two terminals** and run each service separately.

**Terminal 1 — Main API:**
```bash
cd eArchiveSystem
dotnet run
```
Runs on: `http://localhost:5281`  
Swagger UI: `http://localhost:5281/swagger`

**Terminal 2 — OCR Service:**
```bash
cd eArchive.OcrService
dotnet run
```
Runs on: `http://localhost:5271`  
Swagger UI: `http://localhost:5271/swagger`

---

### 5. First login

When the main API starts for the first time, it automatically creates a **Super Admin** account using the credentials you set in `BootstrapAdmin`. Use those credentials to log in and start setting up institutions, departments, and users.
